using System.Collections;
using System.Diagnostics;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Elements;

namespace Fistnet.Genepool.Tests;

internal static class ViewerBackendTests
{
    public static IEnumerable<TestCase> Cases()
    {
        yield return new("viewer: detached frames cannot change the board or prior frames", "viewer-backend", DetachedFrames);
        yield return new("viewer: observing cells and one trace leaves reference state and RNG unchanged", "viewer-backend", PassiveObservation);
        yield return new("viewer: new-run food settings apply exactly and alter reference state", "viewer-backend", RunSettings);
        yield return new("viewer: invalid settings fail before changing the board", "viewer-backend", InvalidSettings);
        yield return new("viewer: selected identity follows movement and retains only sixteen actions", "viewer-backend", FollowMovement);
        yield return new("viewer: selected death retains a detached final state and action", "viewer-backend", FollowDeath);
        yield return new("viewer: births, deaths and actual actions have separate counters", "viewer-backend", ActionCounters);
        yield return new("viewer: neighbor gathering reports actual transfer separately from actor reserve change", "viewer-backend", NeighborGatherTrace);
        yield return new("viewer: offspring observations include the selected second parent", "viewer-backend", SecondParentOffspring);
        yield return new("viewer: chart history is bounded at 240 sampled completed seasons", "viewer-backend", HistoryBound);
        yield return new("worker: initializes paused, steps exactly one or eight and resets at a boundary", "viewer-backend", WorkerSteps);
        yield return new("worker: rejects simultaneous owners and releases ownership after stopping", "viewer-backend", SingleOwner);
        yield return new("worker: pause is nonblocking and acknowledges only at the completed boundary", "viewer-backend", BoundaryPause);
        yield return new("worker: observation commands do not bypass the target speed deadline", "viewer-backend", PacingDeadline);
        yield return new("worker: a full step mailbox rejects overflow instead of silently dropping work", "viewer-backend", StepQueueBound);
    }
    private static SimulationRunOptions Empty(int seed = 79) => new()
        { Mode = SimulationMode.DeterministicReference, Seed = seed, InitialPopulationPercent = 0 };
    private static ViewFrame Frame(ViewCollector collector) => collector.Capture(1, 0, "Paused", false, null, 0, 0, 20);
    private static void DetachedFrames()
    {
        Board.Reset(Empty());
        using var collector = new ViewCollector();
        var before = Frame(collector);
        bool refused = false;
        try { ((IList<ViewCell>)before.Cells)[0] = new ViewCell(0, 0, 10, null, 0, null); }
        catch (NotSupportedException) { refused = true; }
        Check.True(refused, "Cell collection exposed writable storage");
        Check.True(before.Options.RandomSource == null, "Frame exposed engine random source");
        Board.BoardElement[0, 0].SetFoodToMax();
        var after = Frame(collector);
        Check.Equal(3, before.Cells[0].Food); Check.Equal(10, after.Cells[0].Food);
        Check.True(!ReferenceEquals(before.Cells, after.Cells), "Frames reused mutable cell storage");
    }
    private static void PassiveObservation()
    {
        (string Hash, string Random, long Draws) Run(bool observe)
        {
            Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 29, InitialPopulationPercent = 2 });
            using var collector = observe ? new ViewCollector() : null;
            collector?.Select(Board.BoardElement.Cast<BoardSquare>().First(s => s.IsOccupied).Occupant.Id);
            for (int i = 0; i < 16; i++)
            { collector?.BeginSeason(); Board.ExecuteSingleSeason(true); if (collector != null) Frame(collector); }
            return (SimulationSnapshot.Capture().Sha256, Common.RandomSource.State, Common.RandomSource.DrawCount);
        }
        Check.Equal(Run(false), Run(true), "View collection changed simulation or RNG");
    }
    private static void RunSettings()
    {
        Board.Reset(Empty()); string baseline = SimulationSnapshot.Capture().Sha256;
        Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 79,
            InitialPopulationPercent = 0, InitialCellFood = 7, FoodRegrowthPerAge = 2, InitialOrganismFood = 2 });
        Check.True(baseline != SimulationSnapshot.Capture().Sha256, "Nondefault settings missing from execution snapshot");
        Check.Equal((byte)7, Board.BoardElement[0, 0].FoodRemaining);
        for (int i = 0; i < 8; i++) Board.ExecuteSingleSeason(false);
        Check.Equal((byte)9, Board.BoardElement[0, 0].FoodRemaining);
        for (int i = 0; i < 8; i++) Board.ExecuteSingleSeason(false);
        Check.Equal((byte)10, Board.BoardElement[0, 0].FoodRemaining);
        Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 79,
            InitialPopulationPercent = 1, InitialOrganismFood = 2 });
        var population = Board.BoardElement.Cast<BoardSquare>().Where(s => s.IsOccupied).ToArray();
        Check.True(population.Length > 0, "Settings fixture has no organisms");
        Check.True(population.All(s => s.Occupant.FoodBalance == 2), "Initial reserves not applied");
    }
    private static void InvalidSettings()
    {
        Board.Reset(Empty()); string original = SimulationSnapshot.Capture().Sha256;
        foreach (var invalid in new[] {
            new SimulationRunOptions { InitialCellFood = 11 }, new SimulationRunOptions { FoodRegrowthPerAge = -1 },
            new SimulationRunOptions { InitialOrganismFood = 11 }, new SimulationRunOptions { InitialPopulationPercent = -1 },
            new SimulationRunOptions { CellExecution = (CellExecutionMode)999 } })
        {
            bool refused = false;
            try { Board.Reset(invalid); } catch (ArgumentException) { refused = true; }
            Check.True(refused, "Invalid setup was accepted");
            Check.Equal(original, SimulationSnapshot.Capture().Sha256, "Invalid setup changed existing board");
        }
    }
    private static FixtureOrganism Actor(DnaTypes type, TargetTypes target)
    {
        var actor = new FixtureOrganism(); actor.SetState(age: 1);
        actor.SetGenes((me, slot) =>
        {
            IDnaElement gene = type switch
            {
                DnaTypes.Kill => new KillDnaElement(me, slot),
                DnaTypes.CombineDna => new CombineDnaElement(me, slot),
                DnaTypes.GenerateFood => new CreateFoodDnaElement(me, slot),
                _ => new MoveDnaElement(me, slot)
            };
            Check.Property(gene, "Target", target);
            if (type == DnaTypes.Move) Check.Property(gene, "DnaCode", 0); // Never changes direction during this fixture.
            return gene;
        });
        return actor;
    }
    private static void FollowMovement()
    {
        Board.Reset(Empty()); var actor = Actor(DnaTypes.Move, TargetTypes.MiddleRight);
        Board.BoardElement[10, 10].AddOccupant(actor);
        using var collector = new ViewCollector(); collector.Select(actor.Id);
        var initial = Frame(collector);
        for (int i = 0; i < 20; i++) { collector.BeginSeason(); Board.ExecuteSingleSeason(false); }
        var frame = Frame(collector);
        Check.Equal(actor.Id, frame.Selected.Id); Check.Equal(28, frame.Selected.X); Check.Equal(10, frame.Selected.Y);
        Check.Equal(16, frame.Selected.Trace.Count); Check.Equal(5, frame.Selected.Trace[0].Season);
        Check.Equal(10, initial.Selected.X); Check.Equal(0, initial.Selected.Trace.Count);
        Check.True(frame.Selected.Trace.All(t => !string.IsNullOrWhiteSpace(t.ChoiceLabel)), "Selection reason missing");
        collector.Select(null); Check.True(Frame(collector).Selected == null, "Clear selection retained old organism");
    }
    private static void FollowDeath()
    {
        Board.Reset(Empty()); var actor = Actor(DnaTypes.Kill, TargetTypes.Self);
        Board.BoardElement[10, 10].AddOccupant(actor);
        using var collector = new ViewCollector(); collector.Select(actor.Id); Frame(collector);
        collector.BeginSeason(); Board.ExecuteSingleSeason(false);
        var frame = Frame(collector);
        Check.Equal(0, frame.Population); Check.Equal(1L, frame.Deaths);
        Check.True(!frame.Selected.IsAlive, "Dead selected organism appeared alive");
        Check.Equal(actor.Id, frame.Selected.Id); Check.Equal(1, frame.Selected.Trace.Count);
        Check.True(frame.Selected.Trace[0].Result.Contains("died"), "Death trace missing");
        Check.True(frame.Markers.Any(m => m.Kind == "death"), "Death marker missing");
        collector.BeginSeason(); Board.ExecuteSingleSeason(false);
        Check.Equal(1, Frame(collector).Selected.Trace.Count, "Dead organism gained invented actions");
    }
    private static void ActionCounters()
    {
        Board.Reset(Empty()); var actor = Actor(DnaTypes.CombineDna, TargetTypes.Self);
        Board.BoardElement[10, 10].AddOccupant(actor);
        using var collector = new ViewCollector(); collector.Select(actor.Id);
        collector.BeginSeason(); Board.ExecuteSingleSeason(false);
        var frame = Frame(collector);
        Check.Equal(1L, frame.Births); Check.Equal(0L, frame.Deaths); Check.Equal(2, frame.Population);
        Check.Equal(1L, frame.ActionCounts.Single(c => c.Name == "Reproduce").Count);
        Check.Equal(1, frame.Selected.ChildrenObserved); Check.Equal(1L, frame.History[^1].ActionCount);
        Check.True(frame.Markers.Any(m => m.Kind == "birth"), "Placed birth marker missing");
        Check.Equal(16L, frame.GeneCounts.Sum(c => c.Count), "Gene census is not separate from performed actions");
    }
    private static void HistoryBound()
    {
        Board.Reset(Empty()); using var collector = new ViewCollector();
        for (int i = 0; i < 245; i++)
        { collector.BeginSeason(); Board.ExecuteSingleSeason(false); Frame(collector); }
        var frame = Frame(collector); Check.Equal(240, frame.History.Count); Check.Equal(6, frame.History[0].Season);
        Check.True(frame.Markers.Count <= 64, "Marker bound exceeded");
    }
    private static void NeighborGatherTrace()
    {
        Board.Reset(Empty());
        var actor = Actor(DnaTypes.GenerateFood, TargetTypes.MiddleRight);
        var neighbor = Actor(DnaTypes.Move, TargetTypes.Self);
        Board.BoardElement[10, 10].AddOccupant(actor); Board.BoardElement[11, 10].AddOccupant(neighbor);
        using var collector = new ViewCollector(); collector.Select(actor.Id); Frame(collector);
        collector.BeginSeason(); Board.ExecuteSingleSeason(false);
        var action = Frame(collector).Selected.Trace.Single();
        Check.Equal(3, action.FoodGathered); Check.Equal(0, action.FoodChange);
        Check.Equal(11, action.TargetX); Check.Equal(10, action.TargetY);
        Check.Equal(8, neighbor.FoodBalance); Check.Equal(5, actor.FoodBalance);
    }
    private static void SecondParentOffspring()
    {
        Board.Reset(Empty());
        var initiator = Actor(DnaTypes.CombineDna, TargetTypes.MiddleRight);
        var otherParent = Actor(DnaTypes.Move, TargetTypes.Self);
        Board.BoardElement[10, 10].AddOccupant(initiator); Board.BoardElement[11, 10].AddOccupant(otherParent);
        using var collector = new ViewCollector(); collector.Select(otherParent.Id); Frame(collector);
        collector.BeginSeason(); Board.ExecuteSingleSeason(false);
        var frame = Frame(collector);
        Check.Equal(1L, frame.Births); Check.Equal(1, frame.Selected.ChildrenObserved);
        Check.True(!frame.Selected.Trace.Single().BirthPlaced, "Selected second parent was incorrectly the initiator");
    }
    private static ViewFrame Wait(SimulationRunner runner, Func<ViewFrame, bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(10))
        {
            var frame = runner.LatestFrame;
            if (frame?.Fault != null) throw new InvalidOperationException(frame.Fault);
            if (frame != null && condition(frame)) return frame;
            Thread.Sleep(2);
        }
        throw new TimeoutException("Worker did not reach the expected completed boundary.");
    }
    private static void WorkerSteps()
    {
        using var runner = new SimulationRunner(Empty());
        runner.Ready.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        var initial = runner.LatestFrame; Check.Equal(0, initial.Season); Check.Equal("Paused", initial.Status);
        long one = runner.Step(); Wait(runner, f => f.AcknowledgedCommand >= one && f.Season == 1 && f.Status == "Paused");
        long age = runner.Step(8); Wait(runner, f => f.AcknowledgedCommand >= age && f.Season == 9 && f.Status == "Paused");
        long reset = runner.Reset(Empty(83));
        var fresh = Wait(runner, f => f.AcknowledgedCommand >= reset && f.RunId != initial.RunId);
        Check.Equal(0, fresh.Season); Check.Equal(83, fresh.Options.Seed); Check.Equal("Paused", fresh.Status);
        Check.Equal(0L, fresh.Births); Check.Equal(1, fresh.History.Count);
        Check.Equal(0, initial.Season, "Published initial frame mutated");
    }
    private static void SingleOwner()
    {
        using (var runner = new SimulationRunner(Empty()))
        {
            runner.Ready.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            bool refused = false;
            try { using var other = new SimulationRunner(Empty()); }
            catch (InvalidOperationException) { refused = true; }
            Check.True(refused, "Two workers can mutate the static board concurrently");
        }
        using var successor = new SimulationRunner(Empty());
        successor.Ready.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        Check.Equal(0, successor.LatestFrame.Season);
    }
    private static void BoundaryPause()
    {
        using var runner = new SimulationRunner(Empty());
        runner.Ready.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        void Observer(int season, int population) { entered.Set(); Check.True(release.Wait(3000), "Boundary fixture was not released"); }
        Board.SeasonCompleted += Observer;
        try
        {
            runner.Run(); Check.True(entered.Wait(3000), "Worker never began the boundary fixture");
            var timer = Stopwatch.StartNew(); long pause = runner.Pause();
            Check.True(timer.ElapsedMilliseconds < 500, "Pause blocked on running simulation");
            Check.True(runner.LatestFrame.AcknowledgedCommand < pause, "Pause acknowledged before worker boundary");
            release.Set();
            var frame = Wait(runner, f => f.AcknowledgedCommand >= pause && f.Status == "Paused");
            int season = frame.Season; Thread.Sleep(30);
            Check.Equal(season, runner.LatestFrame.Season, "Simulation continued after pause acknowledgment");
        }
        finally { release.Set(); Board.SeasonCompleted -= Observer; }
    }
    private static void PacingDeadline()
    {
        using var runner = new SimulationRunner(Empty());
        runner.Ready.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        runner.SetSpeed(1); runner.Run();
        Wait(runner, f => f.Season == 1);
        var timer = Stopwatch.StartNew();
        while (timer.ElapsedMilliseconds < 200)
        { runner.Select(null); Thread.Sleep(5); }
        long pause = runner.Pause();
        var frame = Wait(runner, f => f.AcknowledgedCommand >= pause && f.Status == "Paused");
        Check.Equal(1, frame.Season, "Observation wakes bypassed the one-second target deadline");
    }
    private static void StepQueueBound()
    {
        using var runner = new SimulationRunner(Empty());
        runner.Ready.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
        void Observer(int season, int population) { entered.Set(); Check.True(release.Wait(3000), "Queue fixture was not released"); }
        Board.SeasonCompleted += Observer;
        try
        {
            runner.Run(); Check.True(entered.Wait(3000), "Worker did not enter queue fixture");
            for (int i = 0; i < 32; i++) runner.Step(8);
            bool rejected = false;
            try { runner.Step(); } catch (InvalidOperationException) { rejected = true; }
            Check.True(rejected, "Overflow was silently accepted");
            long pause = runner.Pause(); release.Set();
            var frame = Wait(runner, f => f.AcknowledgedCommand >= pause && f.Status == "Paused");
            Check.Equal(1, frame.Season, "Cancel did not clear queued steps before another season");
        }
        finally { release.Set(); Board.SeasonCompleted -= Observer; }
    }
}
