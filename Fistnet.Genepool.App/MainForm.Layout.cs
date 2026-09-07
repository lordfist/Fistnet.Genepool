using System;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using Fistnet.Genepool.Visualization;

namespace Fistnet.Genepool.App
{
    public partial class MainForm
    {
        private static readonly Color Surface = Color.FromArgb(19, 28, 40);
        private static readonly Color Muted = Color.FromArgb(150, 169, 190);
        private static readonly Color Accent = Color.FromArgb(98, 217, 186);
        private Button StartButton, StopButton, StepButton, CycleButton, ResetButton, NewButton, FitButton;
        private Label StatisticsLabel, ConfigurationLabel, PlaybackLabel, PaintLabel, BoardHintLabel, TopRatedLabel, ZoomLabel;
        private RichTextBox BoardItemLabel, ActivityLabel;
        private BoardView BoardVisualizer;
        private SimulationHistoryView PopulationGraph;
        private System.Windows.Forms.Timer BoardRefreshTimer;
        private FlowLayoutPanel PatternList;
        private ComboBox SpeedBox, LayerBox;
        private CheckBox TrailCheck, MarkersCheck;
        private CheckBox HistoryCheck;
        private TableLayoutPanel RootLayout;
        private bool adaptingLayout;
        private bool? historyPreference;
        private TabControl DetailTabs;
        private ToolTip Tips;

        private void BuildLayout()
        {
            SuspendLayout();
            Tips = new ToolTip(components) { AutoPopDelay = 18000, InitialDelay = 350, ReshowDelay = 100 };
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16, 10, 16, 10), ColumnCount = 1, RowCount = 6, BackColor = BackColor };
            RootLayout = root;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 136));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            Controls.Add(root);
            var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
            heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 440));
            var title = MakeLabel("GENEPOOL", 20, true, Accent); title.Dock = DockStyle.Fill;
            heading.Controls.Add(title, 0, 0);
            ConfigurationLabel = MakeLabel("Random world · preparing…", 8.5f, false, Muted); ConfigurationLabel.Dock = DockStyle.Fill; ConfigurationLabel.AutoEllipsis = true;
            heading.Controls.Add(ConfigurationLabel, 0, 1); heading.SetColumnSpan(ConfigurationLabel, 2);
            PlaybackLabel = MakeLabel("PAUSED · ready when you are", 10, true); PlaybackLabel.TextAlign = ContentAlignment.MiddleRight; PlaybackLabel.Dock = DockStyle.Fill;
            heading.Controls.Add(PlaybackLabel, 1, 0); root.Controls.Add(heading, 0, 0);

            var playback = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = Padding.Empty };
            StartButton = MakeButton("▶  Run", 86, StartButton_Click, true); StopButton = MakeButton("Ⅱ  Pause", 88, StopButton_Click);
            StepButton = MakeButton("Step season", 110, (s, e) => Send("Step requested", () => simulation.Step()));
            CycleButton = MakeButton("Cycle · 8", 91, (s, e) => Send("Cycle requested · 8 seasons", () => simulation.Step(8)));
            ResetButton = MakeButton("Restart", 83, button1_Click);
            NewButton = MakeButton("New simulation…", 144, NewSimulation_Click);
            playback.Controls.AddRange(new System.Windows.Forms.Control[] { StartButton, StopButton, StepButton, CycleButton, ResetButton, NewButton });
            playback.Controls.Add(MakeLabel("   Target speed", 9, false, Muted));
            SpeedBox = MakeCombo(new[] { "1 season/s", "5 seasons/s", "15 seasons/s", "30 seasons/s", "60 seasons/s", "Fastest" }, 127);
            SpeedBox.SelectedIndex = 2;
            SpeedBox.SelectedIndexChanged += (s, e) => ChangeSpeed();
            playback.Controls.Add(SpeedBox); root.Controls.Add(playback, 0, 1);
            Tips.SetToolTip(CycleButton, "Execute eight seasons, then pause. This is a cycle batch, not an organism's biological age.");
            Tips.SetToolTip(StopButton, "Acknowledges your click immediately. The worker finishes the current season before pausing.");
            Tips.SetToolTip(NewButton, "Choose optional starting settings. They apply only when a new run starts.");
            Tips.SetToolTip(SpeedBox, "A target, not a guaranteed rate. Actual completed seasons per second are shown at the top.");

            var viewTools = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = Padding.Empty };
            viewTools.Controls.Add(MakeLabel("VIEW", 8.5f, true, Muted));
            LayerBox = MakeCombo(new[] { "Combined", "Organisms", "Food" }, 112); LayerBox.SelectedIndex = 0;
            LayerBox.SelectedIndexChanged += (s, e) => BoardVisualizer.Layer = LayerBox.SelectedIndex == 0 ? BoardLayer.Combined : LayerBox.SelectedIndex == 1 ? BoardLayer.Organisms : BoardLayer.Food;
            viewTools.Controls.Add(LayerBox);
            FitButton = MakeButton("Fit", 48, (s, e) => BoardVisualizer.Fit()); FitButton.Height = 28;
            var less = MakeButton("−", 34, (s, e) => BoardVisualizer.ZoomBy(.8f, new Point(BoardVisualizer.Width / 2, BoardVisualizer.Height / 2))); less.Height = 28;
            var more = MakeButton("+", 34, (s, e) => BoardVisualizer.ZoomBy(1.25f, new Point(BoardVisualizer.Width / 2, BoardVisualizer.Height / 2))); more.Height = 28;
            ZoomLabel = MakeLabel("100%", 9, false, Muted); ZoomLabel.Width = 48;
            TrailCheck = MakeCheckBox("Selected trail"); MarkersCheck = MakeCheckBox("Event markers");
            TrailCheck.CheckedChanged += (s, e) => { BoardVisualizer.ShowTrail = TrailCheck.Checked; BoardVisualizer.Invalidate(); };
            MarkersCheck.CheckedChanged += (s, e) => { BoardVisualizer.ShowMarkers = MarkersCheck.Checked; BoardVisualizer.Invalidate(); };
            viewTools.Controls.AddRange(new System.Windows.Forms.Control[] { FitButton, less, more, ZoomLabel, TrailCheck, MarkersCheck });
            HistoryCheck = MakeCheckBox("History"); HistoryCheck.Checked = true;
            HistoryCheck.CheckedChanged += (s, e) => { if (!adaptingLayout) { historyPreference = HistoryCheck.Checked; AdaptHistory(); } };
            viewTools.Controls.Add(HistoryCheck);
            Tips.SetToolTip(HistoryCheck, "Show recent charts below the board. They collapse automatically in short windows to keep the board usable; the data stays available.");
            Tips.SetToolTip(MarkersCheck, "Births and deaths, plus the selected organism's committed action. Up to 64 events from the latest published season; fast playback may skip events.");
            viewTools.Controls.Add(new FoodLegend { Width = 195, Height = 31, Margin = new Padding(16, 0, 0, 0), Font = Font });
            root.Controls.Add(viewTools, 0, 2);

            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 5, 0, 9) };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); main.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 385));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var boardPanel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Margin = new Padding(0, 0, 12, 0), BackColor = Surface };
            boardPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            boardPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); boardPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            StatisticsLabel = MakeLabel("A fresh population is being prepared…", 10); StatisticsLabel.Dock = DockStyle.Fill; StatisticsLabel.Padding = new Padding(12, 3, 12, 3);
            boardPanel.Controls.Add(StatisticsLabel, 0, 0);
            BoardVisualizer = new BoardView { Dock = DockStyle.Fill, Margin = Padding.Empty };
            BoardVisualizer.CellPicked += SelectCell; BoardVisualizer.ViewChanged += () => ZoomLabel.Text = (BoardVisualizer.Zoom * 100).ToString("0") + "%";
            boardPanel.Controls.Add(BoardVisualizer, 0, 1); main.Controls.Add(boardPanel, 0, 0);
            DetailTabs = new TabControl { Dock = DockStyle.Fill, Font = Font, Padding = new Point(15, 8), Margin = Padding.Empty };
            var inspector = MakePage("Organism"); BoardItemLabel = MakeTextPanel(); inspector.Controls.Add(BoardItemLabel); DetailTabs.TabPages.Add(inspector);
            var patterns = MakePage("Patterns");
            var patternLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(8) };
            patternLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82)); patternLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            TopRatedLabel = MakeLabel("Most common action patterns\r\nGroups ordered action types only · not fitness.\r\nDirections and preferences can differ.\r\nClick a row to highlight; again to clear.", 8.5f, false, Muted); TopRatedLabel.Dock = DockStyle.Fill;
            PatternList = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
            patternLayout.Controls.Add(TopRatedLabel, 0, 0); patternLayout.Controls.Add(PatternList, 0, 1); patterns.Controls.Add(patternLayout); DetailTabs.TabPages.Add(patterns);
            var activity = MakePage("Activity"); ActivityLabel = MakeTextPanel(); activity.Controls.Add(ActivityLabel); DetailTabs.TabPages.Add(activity);
            main.Controls.Add(DetailTabs, 1, 0); root.Controls.Add(main, 0, 3);
            PopulationGraph = new SimulationHistoryView { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 8.5f), Margin = Padding.Empty };
            root.Controls.Add(PopulationGraph, 0, 4);
            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = Padding.Empty };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
            BoardHintLabel = MakeLabel("Click to inspect · Wheel to zoom · Right-drag to pan · Board 100 × 100", 8.5f, false, Muted); BoardHintLabel.Dock = DockStyle.Fill;
            PaintLabel = MakeLabel("Display scheduled ≤30 Hz", 8.5f, false, Muted); PaintLabel.Dock = DockStyle.Fill; PaintLabel.TextAlign = ContentAlignment.MiddleRight;
            footer.Controls.Add(BoardHintLabel, 0, 0); footer.Controls.Add(PaintLabel, 1, 0); root.Controls.Add(footer, 0, 5);
            BoardRefreshTimer = new System.Windows.Forms.Timer(components) { Interval = 34 };
            BoardRefreshTimer.Tick += timer1_Tick;
            ShowInspector(); ResumeLayout(true);
        }

        protected override void OnLayout(LayoutEventArgs e) { base.OnLayout(e); AdaptHistory(); }
        private void AdaptHistory()
        {
            if (adaptingLayout || PopulationGraph == null || RootLayout == null || HistoryCheck == null) return;
            float scale = Math.Max(DeviceDpi / 96f, Font.Size / 9f);
            bool visible = historyPreference ?? (ClientSize.Height >= 760 * scale);
            float height = visible ? 136 * scale : 0;
            if (PopulationGraph.Visible == visible && Math.Abs(RootLayout.RowStyles[4].Height - height) < 1) return;
            adaptingLayout = true;
            try
            {
                PopulationGraph.Visible = visible; HistoryCheck.Checked = visible;
                RootLayout.RowStyles[4].Height = height;
            }
            finally { adaptingLayout = false; }
        }

        private RichTextBox MakeTextPanel() => new RichTextBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, ReadOnly = true, BackColor = Surface, ForeColor = ForeColor, Font = new Font("Segoe UI", 9.5f), ScrollBars = RichTextBoxScrollBars.Vertical, DetectUrls = false, Margin = new Padding(10), WordWrap = true };
        private TabPage MakePage(string title) => new TabPage(title) { BackColor = Surface, ForeColor = ForeColor, Padding = new Padding(13) };
        private Label MakeLabel(string text, float size = 9, bool bold = false, Color? color = null)
        {
            var font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
            return new Label { Text = text, Font = font, AutoSize = false, Width = TextRenderer.MeasureText(text, font).Width + 12, Height = 31, TextAlign = ContentAlignment.MiddleLeft, ForeColor = color ?? ForeColor, Margin = new Padding(0, 1, 4, 1) };
        }
        private Button MakeButton(string text, int width, EventHandler handler, bool accent = false)
        {
            var button = new Button { Text = text, Width = width, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = accent ? Color.FromArgb(37, 96, 85) : Color.FromArgb(29, 42, 59), ForeColor = ForeColor, Margin = new Padding(0, 2, 7, 2), Cursor = Cursors.Hand, Font = Font };
            button.FlatAppearance.BorderColor = accent ? Color.FromArgb(74, 169, 144) : Color.FromArgb(53, 72, 93); button.Click += handler; return button;
        }
        private ComboBox MakeCombo(string[] values, int width)
        {
            var combo = new ComboBox { Width = width, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(29, 42, 59), ForeColor = ForeColor, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 3, 8, 2), Font = Font };
            combo.Items.AddRange(values); return combo;
        }
        private CheckBox MakeCheckBox(string text) => new CheckBox { Text = text, AutoSize = true, ForeColor = Muted, Margin = new Padding(9, 6, 7, 0), Font = Font };
    }

    [SupportedOSPlatform("windows6.1")]
    internal sealed class FoodLegend : System.Windows.Forms.Control
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            float scale = Math.Max(1, Font.Height / 15f);
            var saved = e.Graphics.Save(); e.Graphics.ScaleTransform(scale, scale);
            using var text = new SolidBrush(Color.FromArgb(150, 169, 190));
            using var font = new Font(Font.FontFamily, Font.Size / scale);
            e.Graphics.DrawString("Food", font, text, 0, 4);
            for (int i = 0; i <= 10; i++) { using var brush = new SolidBrush(BoardView.FoodColor(i)); e.Graphics.FillRectangle(brush, 43 + i * 10, 3, 10, 11); }
            e.Graphics.DrawString("0", font, text, 40, 14); e.Graphics.DrawString("10", font, text, 139, 14);
            e.Graphics.Restore(saved);
        }
    }

    [SupportedOSPlatform("windows6.1")]
    internal sealed class PatternCard : System.Windows.Forms.Control
    {
        private readonly string description;
        private readonly int count, population;
        private readonly bool selected;
        public PatternCard(string description, int count, int population, bool selected)
        {
            this.description = description; this.count = count; this.population = population; this.selected = selected;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            AccessibleName = description + "; " + count + " organisms";
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); var g = e.Graphics;
            g.Clear(selected ? Color.FromArgb(35, 64, 61) : Color.FromArgb(26, 38, 53));
            using var border = new Pen(selected ? Color.FromArgb(98, 217, 186) : Color.FromArgb(44, 61, 80));
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            TextRenderer.DrawText(g, count.ToString("N0") + " organisms   ·   " + (population == 0 ? 0 : count * 100.0 / population).ToString("0.0") + "%", Font, new Point(9, 7), Color.FromArgb(225, 235, 245));
            string[] genes = description.Split(new[] { " → ", " | ", ", ", " · " }, StringSplitOptions.RemoveEmptyEntries);
            if (genes.Length != 8) { TextRenderer.DrawText(g, description, Font, new Rectangle(9, Font.Height + 14, Width - 18, Height - Font.Height - 18), Color.FromArgb(159, 185, 211), TextFormatFlags.WordBreak); return; }
            float chipWidth = (Width - 23) / 4f;
            for (int i = 0; i < genes.Length; i++)
            {
                Rectangle chip = new Rectangle(9 + (int)(i % 4 * chipWidth), Font.Height + 15 + i / 4 * (Font.Height + 8), (int)chipWidth - 4, Font.Height + 4);
                using var brush = new SolidBrush(Color.FromArgb(36, 53, 71)); g.FillRectangle(brush, chip);
                string label = genes[i] == "Eat reserves" ? "Eat" : genes[i] == "Gather local food" ? "Gather" : genes[i];
                TextRenderer.DrawText(g, label, Font, chip, Color.FromArgb(174, 205, 228), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
