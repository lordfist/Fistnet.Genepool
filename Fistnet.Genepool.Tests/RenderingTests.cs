using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using Fistnet.Genepool.App;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Elements;
using Fistnet.Genepool.Visualization;

namespace Fistnet.Genepool.Tests;

internal static class RenderingTests
{
    public static IEnumerable<TestCase> Cases()
    {
        yield return new("cell picking covers boundaries, corners and outside pixels", "rendering", () =>
        {
            Check.EmptyCells();
            foreach (int size in new[] { 100, 600, 637 })
            {
                using var renderer = new GameboardBitmap(size);
                foreach (int x in new[] { 0, 1, size / 2 - 1, size / 2, size - 1 })
                    foreach (int y in new[] { 0, 1, size / 2, size - 1 })
                        Check.Equal(new Point(x * 100 / size, y * 100 / size), renderer.GetSquareFromLocation(new Point(x, y)).Position);
                foreach (var p in new[] { new Point(-1, 0), new Point(0, -1), new Point(size, 0), new Point(0, size) })
                    Check.True(renderer.GetSquareFromLocation(p) == null, "outside pixel accepted");
            }
        });
        yield return new("empty and occupied pixels have correct positions and remain distinguishable", "rendering", () =>
        {
            Check.EmptyCells();
            var allEat = new FixtureOrganism(); allEat.SetGenes((me, i) => new EatDnaElement(me, i));
            Board.BoardElement[3, 4].AddOccupant(allEat);
            using var renderer = new GameboardBitmap(600); renderer.RefreshAndResize();
            Check.Equal(Color.Black.ToArgb(), renderer.Picture.GetPixel(0, 0).ToArgb());
            Check.True(renderer.Picture.GetPixel(21, 27).ToArgb() != Color.Black.ToArgb(), "legal all-Eat pattern is invisible");
            Check.Equal(Color.Black.ToArgb(), renderer.Picture.GetPixel(27, 21).ToArgb(), "board axes transposed");
            Check.Equal(600, renderer.Picture.Width);
        });
        yield return new("history retains ordered latest 256 seasons and resets", "rendering", () =>
        {
            var history = new PopulationHistory();
            for (int i = 1; i <= 300; i++) history.Add(i, i * 2);
            var values = history.Snapshot();
            Check.Equal(256, values.Length); Check.Equal(45, values[0].Season); Check.Equal(600, values[^1].Population);
            history.Add(300, 7); Check.Equal(256, history.Count); Check.Equal(7, history.Snapshot()[^1].Population);
            values[0] = default; Check.Equal(45, history.Snapshot()[0].Season, "snapshot aliases storage");
            history.Reset(); Check.Equal(0, history.Count);
        });
        yield return new("graph draws empty, single, flat and extreme sequences", "rendering", () =>
        {
            using var view = new PopulationHistoryView { Size = new Size(480, 160) };
            using var bitmap = new Bitmap(view.Width, view.Height);
            var history = new PopulationHistory(); view.History = history;
            foreach (int count in new[] { 0, 1, 20 })
            {
                history.Reset(); for (int i = 0; i < count; i++) history.Add(i, int.MaxValue);
                view.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                Check.Equal(view.BackColor.ToArgb(), bitmap.GetPixel(0, 0).ToArgb());
            }
        });
        yield return new("WinForms pause prevents new work and reset clears run history", "rendering", () =>
        {
            Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 11, InitialPopulationPercent = 0 });
            using var form = new MainForm();
            Board.ExecuteSingleSeason(true);
            var history = Check.Field<PopulationHistory>(form, "populationHistory"); Check.Equal(1, history.Count);
            Check.Invoke(form, "StartButton_Click", form, EventArgs.Empty);
            Check.Invoke(form, "StopButton_Click", form, EventArgs.Empty);
            int season = Board.Season;
            Check.Invoke(form, "timer1_Tick", form, EventArgs.Empty);
            Check.Equal(season, Board.Season);
            Check.True(!Check.Field<BackgroundWorker>(form, "BoardWorker").IsBusy, "paused timer started a worker");
            Check.Invoke(form, "button1_Click", form, EventArgs.Empty);
            Check.Equal(0, Board.Season); Check.Equal(0, history.Count);
            ShowOffscreen(form);
            Check.Invoke(form, "ButtonComplexStats_Click", form, EventArgs.Empty);
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            var picture = Check.Field<System.Windows.Forms.PictureBox>(form, "BoardVisualizer");
            Check.Equal(picture.ClientSize.Width, Check.Field<GameboardBitmap>(form, "gameVisualizer").Picture.Width, "Bitmap size differs from scaled board");
            Check.True(Check.Field<System.Windows.Forms.Label>(form, "TopRatedLabel").Bottom <= Check.Field<System.Windows.Forms.Button>(form, "StartButton").Top, "Pattern list overlaps controls");
            var graph = Check.Field<PopulationHistoryView>(form, "PopulationGraph");
            // DrawToBitmap includes the window frame, while control locations are client-relative.
            var graphOrigin = graph.PointToScreen(Point.Empty) - new Size(form.Location);
            Check.Equal(graph.BackColor.ToArgb(), bitmap.GetPixel(graphOrigin.X + 1, graphOrigin.Y + 1).ToArgb(), "Form capture omitted graph");
            Check.True(picture.Bottom < graph.Top, "Graph overlaps board");
        });
    }

    public static void ExportPreview(string path)
    {
        using var form = new MainForm();
        Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 29, InitialPopulationPercent = 10 });
        for (int i = 0; i < 32; i++) Board.ExecuteSingleSeason(true);
        Check.Invoke(form, "BoardWorker_RunWorkerCompleted", form, new RunWorkerCompletedEventArgs(null, null, false));
        Check.Invoke(form, "ButtonComplexStats_Click", form, EventArgs.Empty);
        ShowOffscreen(form);
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path, ImageFormat.Png);
    }

    private static void ShowOffscreen(MainForm form)
    {
        form.ShowInTaskbar = false;
        form.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
        form.Location = new Point(-32000, -32000);
        form.Show();
        System.Windows.Forms.Application.DoEvents();
    }
}
