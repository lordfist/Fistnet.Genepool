using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Fistnet.Genepool.Control.Gameboard;

namespace Fistnet.Genepool.Control
{
    // The application owns exactly one worker. Commands occupy a bounded mailbox;
    // frames replace one latest slot, so a slow UI never queues obsolete frames.
    public sealed class SimulationRunner : IDisposable
    {
        private static int owner;
        private readonly object gate = new();
        private readonly AutoResetEvent wake;
        private readonly Thread worker;
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private SimulationRunOptions pendingReset;
        private bool desiredRun, dirty = true, stopping;
        private int steps;
        private long command, acknowledged, runId;
        private long? selection;
        private double? target = 20;
        private ViewFrame latest;
        public ViewFrame LatestFrame => Volatile.Read(ref latest);
        public Task Ready => ready.Task;

        public SimulationRunner(SimulationRunOptions options = null)
        {
            options ??= new SimulationRunOptions(); Validate(options);
            if (Interlocked.CompareExchange(ref owner, 1, 0) != 0)
                throw new InvalidOperationException("Only one simulation worker can own the board at a time.");
            try
            {
                wake = new AutoResetEvent(false);
                pendingReset = options;
                worker = new Thread(Work) { IsBackground = true, Name = "Genepool simulation" };
                worker.Start();
            }
            catch { Interlocked.Exchange(ref owner, 0); wake?.Dispose(); throw; }
        }
        private static void Validate(SimulationRunOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.Validate();
            if (options.RandomSource != null)
                throw new ArgumentException("The live worker requires its own seeded random source.", nameof(options));
        }
        private long Change(Action change)
        {
            lock (gate)
            {
                if (stopping || stopped.Task.IsCompleted) throw new ObjectDisposedException(nameof(SimulationRunner));
                change(); dirty = true; command++; wake.Set(); return command;
            }
        }
        public long Run() => Change(() => { desiredRun = true; steps = 0; });
        public long Pause() => Change(() => { desiredRun = false; steps = 0; });
        public long Step(int seasons = 1)
        {
            if (seasons != 1 && seasons != 8) throw new ArgumentOutOfRangeException(nameof(seasons));
            return Change(() =>
            {
                if (steps > 256 - seasons) throw new InvalidOperationException("At most 256 pending seasons can be queued. Pause to cancel them.");
                desiredRun = false; steps += seasons;
            });
        }
        public long Reset(SimulationRunOptions options)
        {
            Validate(options);
            return Change(() => { pendingReset = options; desiredRun = false; steps = 0; selection = null; });
        }
        public long Select(long? organismId)
        {
            if (organismId <= 0) throw new ArgumentOutOfRangeException(nameof(organismId));
            return Change(() => selection = organismId);
        }
        public long SetSpeed(double? seasonsPerSecond)
        {
            if (seasonsPerSecond.HasValue && (!double.IsFinite(seasonsPerSecond.Value)
                || seasonsPerSecond < 0.1 || seasonsPerSecond > 10000)) throw new ArgumentOutOfRangeException(nameof(seasonsPerSecond));
            return Change(() => target = seasonsPerSecond);
        }
        public Task StopAsync()
        {
            lock (gate)
            {
                if (!stopping) { stopping = true; wake.Set(); }
                return stopped.Task;
            }
        }
        public void Dispose() => StopAsync().GetAwaiter().GetResult();

        private void Work()
        {
            ViewCollector collector = null;
            bool running = false;
            double? speed = target;
            double lastMilliseconds = 0, rate = 0;
            long lastPublish = 0, previousSeasonEnd = 0;
            long nextSeasonDue = 0, lastSeasonStarted = 0;
            var intervals = new Queue<double>();
            var clock = Stopwatch.StartNew();
            try
            {
                collector = new ViewCollector();
                while (true)
                {
                    SimulationRunOptions reset;
                    bool changed, step;
                    long? selected;
                    lock (gate)
                    {
                        if (stopping) break;
                        reset = pendingReset; pendingReset = null;
                        changed = dirty; dirty = false;
                        if (desiredRun != running)
                        { previousSeasonEnd = 0; intervals.Clear(); rate = 0; nextSeasonDue = 0; lastSeasonStarted = 0; }
                        if (speed != target && lastSeasonStarted != 0)
                            nextSeasonDue = target.HasValue ? lastSeasonStarted + (long)(Stopwatch.Frequency / target.Value) : 0;
                        running = desiredRun; speed = target; selected = selection;
                        step = !running && steps > 0;
                        if (step) steps--;
                        acknowledged = command;
                    }
                    if (reset != null)
                    {
                        Board.Reset(reset); collector.Reset(); runId++;
                        previousSeasonEnd = nextSeasonDue = lastSeasonStarted = 0; intervals.Clear(); rate = lastMilliseconds = 0;
                    }
                    collector.Select(selected);
                    void Publish(string status)
                    {
                        Volatile.Write(ref latest, collector.Capture(runId, acknowledged, status, running, null,
                            rate, lastMilliseconds, speed));
                        lastPublish = clock.ElapsedMilliseconds;
                    }
                    if (changed || reset != null)
                    {
                        Publish(running ? "Running" : step ? "Stepping" : "Paused");
                        ready.TrySetResult();
                    }
                    if (!running && !step) { wake.WaitOne(); continue; }
                    if (running && speed.HasValue && nextSeasonDue > Stopwatch.GetTimestamp())
                    {
                        // Selection/status commands can wake the worker without buying
                        // an extra season or bypassing the requested speed limit.
                        double remaining = (nextSeasonDue - Stopwatch.GetTimestamp()) * 1000.0 / Stopwatch.Frequency;
                        if (remaining > 0) wake.WaitOne((int)Math.Ceiling(remaining));
                        continue;
                    }
                    long started = Stopwatch.GetTimestamp();
                    lastSeasonStarted = started;
                    collector.BeginSeason();
                    Board.ExecuteSingleSeason(false);
                    long ended = Stopwatch.GetTimestamp();
                    lastMilliseconds = (ended - started) * 1000.0 / Stopwatch.Frequency;
                    if (previousSeasonEnd != 0)
                    {
                        intervals.Enqueue((ended - previousSeasonEnd) / (double)Stopwatch.Frequency);
                        if (intervals.Count > 32) intervals.Dequeue();
                        double total = 0; foreach (double interval in intervals) total += interval;
                        rate = total > 0 ? intervals.Count / total : 0;
                    }
                    previousSeasonEnd = ended;
                    bool finishedSteps;
                    lock (gate) finishedSteps = !running && steps == 0;
                    if (finishedSteps || speed <= 10 || clock.ElapsedMilliseconds - lastPublish >= 100)
                        Publish(running ? "Running" : finishedSteps ? "Paused" : "Stepping");
                    nextSeasonDue = running && speed.HasValue ? started + (long)(Stopwatch.Frequency / speed.Value) : 0;
                }
            }
            catch (Exception ex)
            {
                ready.TrySetException(ex);
                ViewFrame old = LatestFrame;
                if (old != null) Volatile.Write(ref latest, old with { Status = "Faulted", IsRunning = false, Fault = ex.Message });
            }
            finally
            {
                collector?.Dispose();
                ready.TrySetCanceled();
                Interlocked.Exchange(ref owner, 0);
                lock (gate) { stopping = true; wake.Dispose(); }
                stopped.TrySetResult();
            }
        }
    }
}
