using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Fistnet.Genepool.App;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Visualization;

namespace Fistnet.Genepool.Tests;

internal static class UiDiagnosticsTests
{
    internal static object LatestMeasurement { get; private set; }

    public static IEnumerable<TestCase> Cases()
    {
        yield return new("UI diagnostics measure existing refresh paths without changing reference state or pixels", "diagnostics", () =>
        {
            Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 29 });
            using var form = new MainForm();
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-32000, -32000);
            form.Show();
            Application.DoEvents();
            Board.ExecuteSingleSeason(true);
            Check.Invoke(form, "BoardWorker_RunWorkerCompleted", form, new RunWorkerCompletedEventArgs(null, null, false));
            var renderer = Check.Field<GameboardBitmap>(form, "gameVisualizer");
            using var plain = new Bitmap(renderer.Picture);
            using var formImage = new Bitmap(form.Width, form.Height);
            string state = SimulationSnapshot.Capture().Sha256;
            long draws = Common.RandomSource.DrawCount;
            var session = new DiagnosticSession();
            SimulationDiagnostics.Attach(session);
            try
            {
                for (int i = 0; i < 8; i++)
                {
                    Check.Invoke(form, "BoardWorker_RunWorkerCompleted", form, new RunWorkerCompletedEventArgs(null, null, false));
                    Check.Invoke(form, "ButtonComplexStats_Click", form, EventArgs.Empty);
                    Application.DoEvents();
                    long paintStarted = Stopwatch.GetTimestamp();
                    form.DrawToBitmap(formImage, new Rectangle(Point.Empty, formImage.Size));
                    session.Timing("ui-form-draw-to-bitmap", Stopwatch.GetTimestamp() - paintStarted);
                }
                Check.Equal(state, SimulationSnapshot.Capture().Sha256, "UI observer changed simulation state");
                Check.Equal(draws, Common.RandomSource.DrawCount, "UI observer consumed simulation randomness");
                for (int x = 0; x < plain.Width; x += 6)
                    for (int y = 0; y < plain.Height; y += 6)
                        Check.Equal(plain.GetPixel(x, y), renderer.Picture.GetPixel(x, y), "diagnostics changed board pixels");
                var report = session.Export();
                foreach (string key in new[] { "frame-construction", "ui-board-refresh", "ui-controls", "ui-completion", "ui-pattern-list", "ui-form-draw-to-bitmap" })
                    Check.True(report.TimingsTicks.ContainsKey(key), "missing UI timing: " + key);
                LatestMeasurement = new
                {
                    coverage = "Eight existing completion/refresh paths with message pumping and complete-form DrawToBitmap on an offscreen WinForms window. Offscreen refresh can be clipped; DrawToBitmap measures separate form rendering. No user input latency or sustained interactive responsiveness claim.",
                    report
                };
            }
            finally { SimulationDiagnostics.Detach(); }
        });
    }
}
