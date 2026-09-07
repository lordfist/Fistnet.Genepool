using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using System.Windows.Forms;
using Fistnet.Genepool.App;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Dna;
using Fistnet.Genepool.Dna.Elements;
using Fistnet.Genepool.Visualization;

namespace Fistnet.Genepool.Tests;

[SupportedOSPlatform("windows6.1")]
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
        yield return new("detached board layers preserve food under occupants and pick zoomed cells", "rendering", () =>
        {
            var frame = FixtureFrame();
            using var view = new BoardView { Size = new Size(728, 728) }; view.SetFrame(frame);
            using var bitmap = new Bitmap(view.Width, view.Height);
            view.Layer = BoardLayer.Food; view.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            Point p = Pixel(view, 3, 4); Check.Equal(BoardView.FoodColor(3).ToArgb(), bitmap.GetPixel(p.X, p.Y).ToArgb());
            view.Layer = BoardLayer.Organisms; view.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            Check.True(bitmap.GetPixel(p.X, p.Y).ToArgb() != BoardView.FoodColor(3).ToArgb(), "Organism layer ignored occupant");
            view.Layer = BoardLayer.Combined; view.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            Check.True(bitmap.GetPixel(p.X, p.Y).ToArgb() != BoardView.FoodColor(3).ToArgb(), "Combined occupant center missing");
            Check.Equal(BoardView.FoodColor(3).ToArgb(), bitmap.GetPixel(p.X - 2, p.Y - 2).ToArgb(), "Combined layer hid food under occupant");
            Check.Equal(new Point(3, 4), view.CellAt(p).Value);
            Check.True(view.CellAt(new Point(-1, 0)) == null, "Outside board was accepted");
            view.ZoomBy(2, p); Check.Equal(new Point(3, 4), view.CellAt(Pixel(view, 3, 4)).Value);
            view.Fit(); Check.Equal(1f, view.Zoom);
        });
        yield return new("viewer uses descriptive detached inspector, patterns, and retained death state", "rendering", () =>
        {
            var frame = FixtureFrame(); using var form = new MainForm(frame); ShowOffscreen(form);
            string inspector = Check.Field<RichTextBox>(form, "BoardItemLabel").Text;
            foreach (string expected in new[] { "ORGANISM #", "Biological age", "Sequence age", "Offspring", "EIGHT ORDERED", "COMPLETED ACTIONS" }) Check.True(inspector.Contains(expected), "Missing inspector explanation: " + expected);
            Check.True(Check.Field<Label>(form, "TopRatedLabel").Text.Contains("not fitness"), "Pattern grouping presented as fitness");
            Check.Invoke(form, "SelectCell", new Point(0, 0));
            Check.True(Check.Field<RichTextBox>(form, "BoardItemLabel").Text.Contains("CELL 0, 0"), "Empty-cell click did not clear the previous selected detail");
            var dead = frame with { Selected = frame.Selected with { IsAlive = false } };
            form.DisplayFrame(dead, true); Check.True(Check.Field<RichTextBox>(form, "BoardItemLabel").Text.Contains("died"), "Dead observation not retained");
            var reset = frame with { RunId = frame.RunId + 1, Selected = null, History = Array.Empty<ViewHistory>(), Season = 0 };
            form.DisplayFrame(reset, true); Check.True(Check.Field<RichTextBox>(form, "BoardItemLabel").Text.Contains("FOLLOW A LIFE"), "Reset retained selected identity");
            Check.True(Check.Field<BoardView>(form, "BoardVisualizer").HighlightedPattern == null, "Reset retained pattern highlight");
        });
        yield return new("resizable viewer geometry remains separated at 100, 150 and 200 percent", "rendering", () =>
        {
            var frame = FixtureFrame();
            foreach (float scale in new[] { 1f, 1.5f, 2f })
            {
                using var form = new MainForm(frame); form.ClientSize = new Size(1200, 850); ScalePreview(form, scale); ShowOffscreen(form); form.DisplayFrame(frame, true);
                Check.Equal(FormBorderStyle.Sizable, form.FormBorderStyle); Check.True(form.MaximizeBox, "Maximize unavailable");
                var board = Check.Field<BoardView>(form, "BoardVisualizer"); var graph = Check.Field<SimulationHistoryView>(form, "PopulationGraph");
                var tabs = Check.Field<TabControl>(form, "DetailTabs"); var speed = Check.Field<ComboBox>(form, "SpeedBox");
                var boardRect = ScreenRect(board); var graphRect = ScreenRect(graph); var tabRect = ScreenRect(tabs);
                Check.True(!graph.Visible || boardRect.Bottom <= graphRect.Top, "History overlaps board at " + scale + " board=" + boardRect + " graph=" + graphRect + " form=" + form.Bounds);
                Check.True(boardRect.Right < tabRect.Left, "Inspector overlaps board at " + scale);
                Check.True(board.Width > tabs.Width, "Board is not the dominant pane at " + scale);
                Check.True(speed.Right <= speed.Parent.ClientSize.Width, "Playback toolbar clipped at " + scale);
                Check.True(board.WorldRectangle.Width > 250, "Board too small at " + scale);
                Check.True(boardRect.Bottom <= form.PointToScreen(new Point(0, form.ClientSize.Height)).Y, "Board extends outside actual client height");
                Check.True(tabRect.Bottom <= form.PointToScreen(new Point(0, form.ClientSize.Height)).Y, "Inspector extends outside actual client height");
                Check.True(board.ClientRectangle.Contains(Rectangle.Ceiling(board.WorldRectangle)), "Fitted board does not fit inside its control at " + scale);
                using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                Check.Equal(board.BackColor.ToArgb(), bitmap.GetPixel(boardRect.Left - form.Left + 1, boardRect.Top - form.Top + 1).ToArgb(), "Board missing from form capture");
                Check.Field<CheckBox>(form, "HistoryCheck").Checked = true; form.PerformLayout(); Application.DoEvents();
                Check.True(ScreenRect(board).Bottom <= ScreenRect(graph).Top, "Explicitly shown history overlaps the board at " + scale);
            }
        });
        yield return new("new simulation dialog preserves defaults, optional policies and explicit seeds", "rendering", () =>
        {
            using var dialog = new NewSimulationDialog();
            Check.Equal(10m, Check.Field<NumericUpDown>(dialog, "Density").Value); Check.Equal(3m, Check.Field<NumericUpDown>(dialog, "CellFood").Value);
            Check.Equal(1m, Check.Field<NumericUpDown>(dialog, "Regrowth").Value); Check.Equal(5m, Check.Field<NumericUpDown>(dialog, "Reserves").Value);
            Check.True(Check.Field<CheckBox>(dialog, "RandomSeed").Checked, "Random seed is not the default");
            Check.Field<ComboBox>(dialog, "Preset").SelectedIndex = 2;
            Check.Equal(1, Check.Field<ComboBox>(dialog, "Learning").SelectedIndex); Check.Equal(0, Check.Field<ComboBox>(dialog, "Predation").SelectedIndex);
            Check.Field<CheckBox>(dialog, "RandomSeed").Checked = false; Check.Field<NumericUpDown>(dialog, "SeedValue").Value = 1234;
            Check.Invoke(dialog, "Create_Click", dialog, EventArgs.Empty);
            Check.Equal(1234, dialog.Options.Seed); Check.Equal(LearningPolicy.BoundedExploratory, dialog.Options.Policy.Learning);
            Check.Equal(HealthPolicy.LegacyOverweight, dialog.Options.Policy.Health); Check.Equal(AttackPolicy.DamageOnly, dialog.Options.Policy.Attack);
        });
        yield return new("settings keep actions visible and all fields reachable at 100, 150 and 200 percent", "rendering", () =>
        {
            foreach (float scale in new[] { 1f, 1.5f, 2f })
            {
                using var dialog = new NewSimulationDialog(); ScalePreview(dialog, scale); ShowOffscreen(dialog);
                Rectangle client = dialog.RectangleToScreen(dialog.ClientRectangle);
                foreach (string name in new[] { "CreateButton", "CancelActionButton", "DefaultsButton" })
                {
                    var button = Check.Field<Button>(dialog, name);
                    Check.True(client.Contains(ScreenRect(button)), name + " clipped at " + scale + ": " + ScreenRect(button) + " client=" + client);
                }
                var body = Check.Field<Panel>(dialog, "SettingsBody"); var lastSetting = Check.Field<ComboBox>(dialog, "Predation");
                Check.True(body.AutoScroll, "Settings do not provide a scrollable body");
                body.ScrollControlIntoView(lastSetting); Application.DoEvents();
                Check.True(body.RectangleToScreen(body.ClientRectangle).Contains(ScreenRect(lastSetting)), "Last setting cannot be reached at " + scale);
                Check.True(ScreenRect(body).Bottom <= ScreenRect(Check.Field<Button>(dialog, "CreateButton")).Top, "Scrollable settings overlap fixed actions");
            }
        });
        yield return new("viewer rejects commands from reset, fault and stopped-worker frames", "rendering", () =>
        {
            using var form = new MainForm(); var runner = Check.Field<SimulationRunner>(form, "simulation");
            runner.Ready.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); form.DisplayFrame(runner.LatestFrame, true);
            Check.Invoke(form, "button1_Click", form, EventArgs.Empty);
            long reset = Check.Field<long>(runner, "command");
            Check.Invoke(form, "SelectCell", new Point(3, 4)); Check.Field<ComboBox>(form, "SpeedBox").SelectedIndex = 1;
            Check.Equal(reset, Check.Field<long>(runner, "command"), "Old frame issued selection/speed while reset was pending");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (runner.LatestFrame.AcknowledgedCommand < reset)
            { Check.True(timer.ElapsedMilliseconds < 5000, "Reset failed to settle"); Thread.Sleep(2); }
            form.DisplayFrame(runner.LatestFrame with { Fault = "declared UI fault fixture" }, true);
            long fault = Check.Field<long>(runner, "command");
            Check.Invoke(form, "SelectCell", new Point(3, 4)); Check.Field<ComboBox>(form, "SpeedBox").SelectedIndex = 2;
            Check.Equal(fault, Check.Field<long>(runner, "command"), "Faulted frame issued commands");
            form.DisplayFrame(runner.LatestFrame, true); runner.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Check.Invoke(form, "SelectCell", new Point(3, 4)); Check.Field<ComboBox>(form, "SpeedBox").SelectedIndex = 3;
            form.DisplayFrame(runner.LatestFrame, true);
            Check.True(!Check.Field<BoardView>(form, "BoardVisualizer").Enabled, "Stopped worker left selection enabled");
            Check.True(Check.Field<Label>(form, "PlaybackLabel").Text.Contains("stopped"), "Disposed race was not reported");
        });
    }

    internal static ViewFrame FixtureFrame(int density = 0, bool ensureSelected = true)
    {
        Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 29, InitialPopulationPercent = density });
        using var collector = new ViewCollector();
        if (ensureSelected)
        {
            var actor = Board.BoardElement[3, 4].Occupant;
            if (actor == null) { var created = new FixtureOrganism(); created.SetState(); created.SetGenes((me, i) => new EatDnaElement(me, i)); Board.BoardElement[3, 4].AddOccupant(created); actor = created; }
            collector.Select(actor.Id);
        }
        return collector.Capture(1, 0, "Paused", false, null, 0, 0, 15);
    }
    private static Point Pixel(BoardView board, int x, int y)
    { RectangleF world = board.WorldRectangle; return new Point((int)(world.Left + (x + .5f) * world.Width / 100), (int)(world.Top + (y + .5f) * world.Height / 100)); }
    internal static Rectangle ScreenRect(System.Windows.Forms.Control control) => new Rectangle(control.PointToScreen(Point.Empty), control.ClientSize);
    internal static void ShowOffscreen(Form form)
    {
        form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-32000, -32000);
        form.WindowState = FormWindowState.Normal;
        form.Show(); Application.DoEvents();
        form.PerformLayout(); foreach (System.Windows.Forms.Control child in Descendants(form)) child.PerformLayout(); Application.DoEvents();
    }
    private static IEnumerable<System.Windows.Forms.Control> Descendants(System.Windows.Forms.Control control)
    {
        foreach (System.Windows.Forms.Control child in control.Controls)
        { yield return child; foreach (var nested in Descendants(child)) yield return nested; }
    }
    internal static void ScalePreview(Form form, float scale)
    {
        // This is representative geometry/font scaling, not a physical monitor DPI change.
        var fonts = Descendants(form).Prepend(form).Select(c => (Control: c, Font: c.Font)).ToArray();
        form.AutoScaleMode = AutoScaleMode.None;
        form.Scale(new SizeF(scale, scale));
        foreach (var item in fonts) item.Control.Font = new Font(item.Font.FontFamily, item.Font.Size * scale, item.Font.Style);
        form.MinimumSize = Size.Empty;
        Rectangle work = Screen.PrimaryScreen.WorkingArea;
        form.ClientSize = new Size(Math.Min(form.ClientSize.Width, work.Width - 24), Math.Min(form.ClientSize.Height, work.Height - 70));
    }
    public static void ExportPreview(string path)
    {
        Board.Reset(new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 29, InitialPopulationPercent = 10 });
        using var collector = new ViewCollector();
        var actor = Board.BoardElement.Cast<BoardSquare>().First(s => s.IsOccupied).Occupant; collector.Select(actor.Id);
        for (int i = 0; i < 32; i++) { collector.BeginSeason(); Board.ExecuteSingleSeason(false); collector.Capture(1, 0, "Paused", false, null, 0, 0, 15); }
        var frame = collector.Capture(1, 0, "Paused", false, null, 0, 0, 15);
        string stem = Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
        foreach (float scale in new[] { 1f, 1.5f, 2f })
        {
            using var form = new MainForm(frame); ScalePreview(form, scale); ShowOffscreen(form); form.DisplayFrame(frame, true);
            using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(scale == 1 ? path : stem + "-" + (int)(scale * 100) + ".png", ImageFormat.Png);
            if (scale == 1)
            {
                Check.Field<TabControl>(form, "DetailTabs").SelectedIndex = 1; Application.DoEvents();
                form.DisplayFrame(frame, true); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(stem + "-patterns.png", ImageFormat.Png);
            }
        }
        foreach (float scale in new[] { 1f, 1.5f, 2f })
        {
            using var settings = new NewSimulationDialog(frame.Options); ScalePreview(settings, scale); ShowOffscreen(settings);
            using var settingsImage = new Bitmap(settings.Width, settings.Height); settings.DrawToBitmap(settingsImage, new Rectangle(Point.Empty, settingsImage.Size));
            settingsImage.Save(stem + "-settings" + (scale == 1 ? "" : "-" + (int)(scale * 100)) + ".png", ImageFormat.Png);
        }
    }
}
