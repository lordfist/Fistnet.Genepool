using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using Fistnet.Genepool.App;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Visualization;

namespace Fistnet.Genepool.Tests;

[SupportedOSPlatform("windows6.1")]
internal static class UiDiagnosticsTests
{
    internal static object LatestMeasurement { get; private set; }
    private static List<object> rows = new();
    private static object slowWorker;
    public static IEnumerable<TestCase> Cases()
    {
        yield return new("detached UI paint and input measurements preserve state at empty, default and full occupancy", "diagnostics", () =>
        {
            rows = new List<object>();
            foreach (int density in new[] { 0, 10, 100 })
            {
                var frame = RenderingTests.FixtureFrame(density, false);
                string state = SimulationSnapshot.Capture().Sha256; string random = Common.RandomSource.State; long draws = Common.RandomSource.DrawCount;
                using var form = new MainForm(frame); RenderingTests.ShowOffscreen(form);
                var board = Check.Field<BoardView>(form, "BoardVisualizer");
                using var boardImage = new Bitmap(board.Width, board.Height); using var formImage = new Bitmap(form.Width, form.Height);
                var paint = new List<double>(); var input = new List<double>(); var formPaint = new List<double>(); var update = new List<double>();
                foreach (bool overlays in new[] { false, true })
                {
                    board.ShowMarkers = overlays; board.ShowTrail = overlays;
                    var visualFrame = overlays ? frame with
                    {
                        Selected = new ViewOrganism(999999, 0, 0, 0, 50, 50, true, 10, 5, 3, 0, 0, 1, 0, "synthetic-overlay-only",
                            Array.Empty<ViewGene>(), Array.AsReadOnly(new[] { new ViewAction(1, 0, "Move", "MiddleRight", "synthetic overlay geometry", 0, 0, 0, 40, 40, 50, 50, "synthetic", "synthetic") })),
                        Markers = Array.AsReadOnly(new[] { new ViewMarker(50, 50, "death"), new ViewMarker(52, 50, "birth"), new ViewMarker(54, 50, "action") })
                    } : frame;
                    for (int i = 0; i < 12; i++)
                    {
                        long started = Stopwatch.GetTimestamp(); form.DisplayFrame(visualFrame with { AcknowledgedCommand = i, Season = i + 1 }, true);
                        double updateMs = Elapsed(started);
                        started = Stopwatch.GetTimestamp(); board.DrawToBitmap(boardImage, new Rectangle(Point.Empty, boardImage.Size));
                        double paintMs = Elapsed(started);
                        started = Stopwatch.GetTimestamp(); Check.Invoke(form, "SelectCell", new Point(3, 4));
                        double inputMs = Elapsed(started);
                        started = Stopwatch.GetTimestamp(); form.DrawToBitmap(formImage, new Rectangle(Point.Empty, formImage.Size));
                        double formMs = Elapsed(started);
                        if (i > 1) { paint.Add(paintMs); input.Add(inputMs); formPaint.Add(formMs); update.Add(updateMs); }
                    }
                }
                Check.Equal(state, SimulationSnapshot.Capture().Sha256, "Viewer changed the simulation");
                Check.Equal(random, Common.RandomSource.State, "Viewer changed random state"); Check.Equal(draws, Common.RandomSource.DrawCount, "Viewer consumed randomness");
                rows.Add(new { initialPopulationPercent = density, frame.Population, retainedSamples = paint.Count,
                    boardDrawToBitmapP95Milliseconds = Percentile(paint), completeFormDrawToBitmapP95Milliseconds = Percentile(formPaint),
                    detailedUiUpdateP95Milliseconds = Percentile(update), selectionHandlerP95Milliseconds = Percentile(input),
                    boardPaintTargetMet = Percentile(paint) < 33, inputTargetMet = Percentile(input) < 100 });
            }
            UpdateReport();
        });
        yield return new("viewer acknowledges pause immediately while a slow worker finishes its season", "diagnostics", () =>
        {
            using var form = new MainForm();
            var runner = Check.Field<SimulationRunner>(form, "simulation"); runner.Ready.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            RenderingTests.ShowOffscreen(form); form.DisplayFrame(runner.LatestFrame, true);
            using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
            void Hold(int season, int population) { entered.Set(); Check.True(release.Wait(3000), "Slow-worker fixture was not released"); }
            Board.SeasonCompleted += Hold;
            try
            {
                Check.Invoke(form, "StartButton_Click", form, EventArgs.Empty); Check.True(entered.Wait(3000), "Worker never reached fixture barrier");
                long started = Stopwatch.GetTimestamp(); Check.Invoke(form, "StopButton_Click", form, EventArgs.Empty); double acknowledgment = Elapsed(started);
                Check.True(acknowledgment < 100, "Pause UI handler exceeded 100 ms: " + acknowledgment);
                Check.True(Check.Field<Label>(form, "PlaybackLabel").Text.Contains("Pause requested"), "Pause not immediately shown");
                long command = Check.Field<long>(form, "pendingCommand");
                Check.True(runner.LatestFrame.AcknowledgedCommand < command, "Pause settled before the held season ended");
                Thread.Sleep(150); release.Set();
                var wait = Stopwatch.StartNew();
                while (runner.LatestFrame.AcknowledgedCommand < command || runner.LatestFrame.Status != "Paused")
                { Check.True(wait.ElapsedMilliseconds < 5000, "Pause failed to settle"); Application.DoEvents(); Thread.Sleep(2); }
                form.DisplayFrame(runner.LatestFrame, true);
                Check.True(Check.Field<Label>(form, "PlaybackLabel").Text.Contains("Pause settled"), "Settlement latency was not shown");
                Check.True(Check.Field<double>(form, "lastSettlementMilliseconds") >= 150, "Displayed settlement omitted the held boundary interval");
                slowWorker = new { pauseHandlerAcknowledgmentMilliseconds = acknowledgment, pauseClickToSettlementMilliseconds = Elapsed(started),
                    displayedSettlementMilliseconds = Check.Field<double>(form, "lastSettlementMilliseconds"),
                    artificialBoundaryHoldAfterClickMilliseconds = 150, acknowledgmentTargetMet = acknowledgment < 100,
                    coverage = "Real MainForm pause handler while its single simulation worker was held at a completed-season callback; no physical input latency claim." };
                UpdateReport();
            }
            finally { release.Set(); Board.SeasonCompleted -= Hold; }
        });
    }
    private static double Elapsed(long started) => (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
    private static double Percentile(List<double> values) { var sorted = values.OrderBy(v => v).ToArray(); return sorted[(int)Math.Ceiling(sorted.Length * .95) - 1]; }
    private static void UpdateReport() => LatestMeasurement = new
    {
        coverage = "Actual offscreen WinForms controls over detached true 0%, random 10%, and 100% start frames. Overlays off/on use explicitly synthetic selected movement plus birth/death/action marker geometry so empty-board overlays are exercised without altering world occupancy. Marker display lifetimes restart per sample. Two warm-up samples discarded per condition. Board/complete-form DrawToBitmap, forced detailed refresh, and programmatic selection measured separately; no physical input-to-display or owner visual acceptance claim.",
        measurements = rows, slowWorker
    };
}
