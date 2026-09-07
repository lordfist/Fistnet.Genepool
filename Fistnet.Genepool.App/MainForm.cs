using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Windows.Forms;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Visualization;

namespace Fistnet.Genepool.App
{
    // WinForms sees detached frames. SimulationRunner owns every world mutation.
    [SupportedOSPlatform("windows6.1")]
    public partial class MainForm : Form
    {
        private readonly SimulationRunner simulation;
        private SimulationRunOptions activeOptions;
        private ViewFrame displayedFrame, detailedFrame;
        private readonly bool previewOnly;
        private bool closing, closed, resourcesReleased, commandsUnavailable;
        private long pendingCommand, lastDetailRefresh, selectionCommand;
        private long settlementStarted;
        private string settlementRequest, lastSettlement;
        private double lastSettlementMilliseconds;
        private string pendingText;
        private long? selectedId;
        private Point? selectedCell;
        private string patternKey;

        public MainForm() : this(null, false) { }
        // Passive preview constructor: no worker, no initialization, no live board access.
        public MainForm(ViewFrame previewFrame) : this(previewFrame ?? throw new ArgumentNullException(nameof(previewFrame)), true) { }
        private MainForm(ViewFrame previewFrame, bool preview)
        {
            previewOnly = preview;
            InitializeComponent();
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime) return;
            if (preview) DisplayFrame(previewFrame, true);
            else
            {
                Rectangle workArea = Screen.FromControl(this).WorkingArea;
                MinimumSize = new Size(Math.Min(MinimumSize.Width, workArea.Width), Math.Min(MinimumSize.Height, workArea.Height));
                WindowState = FormWindowState.Maximized;
                activeOptions = new SimulationRunOptions();
                simulation = new SimulationRunner(activeOptions);
                simulation.SetSpeed(15);
                BoardRefreshTimer.Start();
            }
        }

        private void StartButton_Click(object sender, EventArgs e) => Send("Run requested", () => simulation.Run());
        private void StopButton_Click(object sender, EventArgs e) => Send("Pause requested · finishing current season", () => simulation.Pause());
        private void button1_Click(object sender, EventArgs e)
        {
            if (activeOptions != null) Send("Restart requested · finishing current season", () => simulation.Reset(activeOptions));
        }
        private void NewSimulation_Click(object sender, EventArgs e)
        {
            if (simulation == null || closing) return;
            using var dialog = new NewSimulationDialog(activeOptions);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            activeOptions = dialog.Options;
            Send("New world requested · finishing current season", () => simulation.Reset(activeOptions));
        }
        private void Send(string text, Func<long> command)
        {
            if (!TryCommand(command, out long number)) return;
            pendingCommand = number; pendingText = text;
            settlementRequest = text.StartsWith("Pause", StringComparison.Ordinal) ? "Pause" : text.StartsWith("Restart", StringComparison.Ordinal) ? "Restart" : text.StartsWith("New world", StringComparison.Ordinal) ? "New world" : null;
            settlementStarted = Stopwatch.GetTimestamp();
            PlaybackLabel.Text = text; PlaybackLabel.ForeColor = Color.FromArgb(255, 219, 133);
            StartButton.Enabled = StepButton.Enabled = CycleButton.Enabled = ResetButton.Enabled = NewButton.Enabled = SpeedBox.Enabled = false;
            StopButton.Enabled = true;
        }
        private bool TryCommand(Func<long> command, out long number)
        {
            number = 0;
            if (simulation == null || closing || commandsUnavailable || displayedFrame?.Fault != null || simulation.Ready.IsFaulted) return false;
            try { number = command(); return true; }
            catch (ObjectDisposedException)
            {
                commandsUnavailable = true;
                PlaybackLabel.Text = "Simulation stopped · reopen the app to start a new world";
                PlaybackLabel.ForeColor = Color.FromArgb(255, 139, 149);
                DisableSimulationControls();
                return false;
            }
        }
        private void ChangeSpeed()
        {
            if (pendingCommand != 0) return;
            TryCommand(() => simulation.SetSpeed(SpeedBox.SelectedIndex == 5 ? null : new double[] { 1, 5, 15, 30, 60 }[SpeedBox.SelectedIndex]), out _);
        }
        private void DisableSimulationControls()
        {
            StartButton.Enabled = StopButton.Enabled = StepButton.Enabled = CycleButton.Enabled = ResetButton.Enabled = NewButton.Enabled = SpeedBox.Enabled = BoardVisualizer.Enabled = false;
        }
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (closing || simulation == null) return;
            ViewFrame next = simulation.LatestFrame;
            if (next != null) DisplayFrame(next);
            else if (simulation.Ready.IsFaulted)
            {
                PlaybackLabel.Text = "Could not start · " + simulation.Ready.Exception.GetBaseException().Message;
                PlaybackLabel.ForeColor = Color.FromArgb(255, 139, 149);
                StartButton.Enabled = StopButton.Enabled = StepButton.Enabled = CycleButton.Enabled = ResetButton.Enabled = NewButton.Enabled = SpeedBox.Enabled = false;
                BoardRefreshTimer.Stop();
            }
            BoardVisualizer.ExpireMarkers();
        }

        public void DisplayFrame(ViewFrame frame, bool forceDetails = false)
        {
            if (frame == null) return;
            long started = Stopwatch.GetTimestamp();
            bool newRun = displayedFrame == null || displayedFrame.RunId != frame.RunId;
            if (newRun)
            {
                selectedId = null; selectedCell = null; patternKey = null; selectionCommand = 0;
                BoardVisualizer.SelectedCell = null; BoardVisualizer.HighlightedPattern = null; BoardVisualizer.Fit();
            }
            bool changed = !ReferenceEquals(displayedFrame, frame);
            displayedFrame = frame;
            if (newRun || frame.AcknowledgedCommand >= pendingCommand) activeOptions = frame.Options;
            if (changed) BoardVisualizer.SetFrame(frame);
            BoardVisualizer.SelectedOrganismId = previewOnly ? frame.Selected?.Id : selectedId;
            if (pendingCommand != 0 && frame.AcknowledgedCommand >= pendingCommand)
            {
                if (settlementRequest != null)
                {
                    lastSettlementMilliseconds = (Stopwatch.GetTimestamp() - settlementStarted) * 1000.0 / Stopwatch.Frequency;
                    lastSettlement = settlementRequest + " settled in " + lastSettlementMilliseconds.ToString("0") + " ms";
                }
                pendingCommand = 0; pendingText = null; settlementRequest = null;
            }
            bool pending = pendingCommand != 0;
            bool stepping = frame.Status == "Stepping";
            PlaybackLabel.Text = commandsUnavailable ? "Simulation stopped · reopen the app to start a new world" : frame.Fault != null ? "STOPPED · " + frame.Fault : pending ? pendingText :
                (frame.IsRunning ? "RUNNING" : stepping ? "STEPPING" : "PAUSED") + "  ·  " +
                (!frame.IsRunning && !stepping && lastSettlement != null ? lastSettlement : frame.ActualSeasonsPerSecond.ToString("0.0") + (frame.IsRunning || stepping ? " actual seasons/s" : " last seasons/s"));
            PlaybackLabel.ForeColor = frame.Fault != null ? Color.FromArgb(255, 139, 149) : pending ? Color.FromArgb(255, 219, 133) : frame.IsRunning ? Accent : ForeColor;
            StartButton.Enabled = !previewOnly && !frame.IsRunning && !stepping && !pending && frame.Fault == null;
            StopButton.Enabled = !previewOnly && (frame.IsRunning || stepping || pending) && frame.Fault == null;
            StepButton.Enabled = CycleButton.Enabled = !previewOnly && !frame.IsRunning && !stepping && !pending && frame.Fault == null;
            NewButton.Enabled = ResetButton.Enabled = !previewOnly && !pending && frame.Fault == null;
            SpeedBox.Enabled = !previewOnly && !pending && frame.Fault == null;
            if (commandsUnavailable) DisableSimulationControls();
            if (forceDetails || newRun || (!ReferenceEquals(detailedFrame, frame) && Stopwatch.GetTimestamp() - lastDetailRefresh >= Stopwatch.Frequency / 2))
            {
                StatisticsLabel.Text = frame.Population.ToString("N0") + " living   ·   Season " + frame.Season.ToString("N0") + "   ·   Cycle " + frame.Age.ToString("N0") +
                    "\r\nLocal food " + frame.LocalFood.ToString("N0") + "   ·   Carried " + frame.StoredFood.ToString("N0") + "   ·   Births " + frame.Births.ToString("N0") + " / Deaths " + frame.Deaths.ToString("N0");
                ConfigurationLabel.Text = Configuration(frame.Options);
                Tips.SetToolTip(ConfigurationLabel, ConfigurationLabel.Text);
                PaintLabel.Text = "Display ≤30 Hz  ·  last board paint " + BoardVisualizer.LastPaintMilliseconds.ToString("0.0") + " ms";
                ShowInspector(); ShowComplexStatistics(); ShowActivity(); PopulationGraph.Frame = frame;
                lastDetailRefresh = Stopwatch.GetTimestamp();
                detailedFrame = frame;
            }
            LastUiUpdateMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        }

        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public double LastUiUpdateMilliseconds { get; private set; }

        private void SelectCell(Point cell)
        {
            if (displayedFrame == null || closing || displayedFrame.Fault != null || pendingCommand != 0) return;
            ViewCell value = displayedFrame.Cells[cell.Y * displayedFrame.Width + cell.X];
            long selection = 0;
            if (!previewOnly && !TryCommand(() => simulation.Select(value.OrganismId), out selection)) return;
            selectedCell = cell; BoardVisualizer.SelectedCell = cell;
            selectedId = value.OrganismId;
            BoardVisualizer.SelectedOrganismId = selectedId;
            if (previewOnly && displayedFrame.Selected?.Id != selectedId)
            {
                displayedFrame = displayedFrame with { Selected = null };
                BoardVisualizer.SetFrame(displayedFrame);
            }
            selectionCommand = selection;
            DetailTabs.SelectedIndex = 0;
            BoardItemLabel.SelectionStart = 0; BoardItemLabel.ScrollToCaret();
            ShowInspector(); BoardVisualizer.Invalidate();
        }

        private void ShowInspector()
        {
            if (BoardItemLabel == null) return;
            int firstVisibleLine = FirstVisibleLine(BoardItemLabel);
            BoardItemLabel.Clear();
            if (displayedFrame == null) { Welcome(); return; }
            var selected = displayedFrame.Selected;
            if (selected == null || (!previewOnly && selectedId != selected.Id))
            {
                if (selectedId.HasValue && selectionCommand != 0 && displayedFrame.AcknowledgedCommand >= selectionCommand)
                {
                    Append(BoardItemLabel, "ORGANISM #" + selectedId + "\r\n\r\n", Accent, true);
                    Append(BoardItemLabel, "This organism was no longer present when your selection reached a completed season.\r\n\r\nNo earlier action history was collected for this selection. Click another organism to begin following it.", Muted);
                    return;
                }
                if (selectedCell.HasValue)
                {
                    ViewCell cell = displayedFrame.Cells[selectedCell.Value.Y * displayedFrame.Width + selectedCell.Value.X];
                    Append(BoardItemLabel, "CELL " + cell.X + ", " + cell.Y + "\r\n\r\n", Accent, true);
                    Append(BoardItemLabel, "Local food   " + cell.Food + " / 10\r\n" + (cell.OrganismId.HasValue ? "Organism #" + cell.OrganismId + " · fetching completed details…" : "Empty · resources remain available here."), ForeColor);
                }
                else Welcome();
                return;
            }
            Append(BoardItemLabel, "ORGANISM #" + selected.Id + (selected.IsAlive ? "  ·  following" : "  ·  died") + "\r\n", selected.IsAlive ? Accent : Color.FromArgb(255, 139, 149), true);
            Append(BoardItemLabel, "Position " + selected.X + ", " + selected.Y + "   ·   Generation " + selected.Generation + "\r\n", ForeColor);
            Append(BoardItemLabel, "Parents " + Parent(selected.Parent1Id) + " / " + Parent(selected.Parent2Id) + "\r\n", Muted);
            Append(BoardItemLabel, "Lifetime " + selected.LifetimeSeasons + " seasons\r\nBiological age " + selected.Age + "   ·   Sequence age " + selected.SequenceAge + " (inheritable)\r\n\r\n", Muted);
            Append(BoardItemLabel, "Health " + selected.Health + "   ·   Carried reserves " + selected.Food + " / 10\r\nLocal food " + selected.LocalFood + " / 10\r\nOffspring " + selected.ChildrenObserved + " observed since selection\r\n\r\n", ForeColor);
            Append(BoardItemLabel, "DNA · EIGHT ORDERED SLOTS\r\n", Accent, true);
            var last = selected.Trace.LastOrDefault();
            foreach (var gene in selected.Genes)
            {
                bool chosen = last != null && gene.Slot == last.Slot;
                Append(BoardItemLabel, (chosen ? "▶ " : "   ") + (gene.Slot + 1) + "  " + gene.Name + "  " + Target(gene.Target) + "\r\n", chosen ? Color.FromArgb(255, 223, 142) : ForeColor, chosen);
            }
            Append(BoardItemLabel, "\r\nTargets form a 3 × 3 neighborhood; ● means self.\r\n", Muted);
            Append(BoardItemLabel, "▶ marks the last chosen slot; DNA can change afterward.\r\n", Muted);
            Append(BoardItemLabel, "\r\nCOMPLETED ACTIONS · latest " + selected.Trace.Count + " / 16\r\n", Accent, true);
            if (!selected.IsAlive) Append(BoardItemLabel, "Last observed state is retained. Select another organism to follow a new life.\r\n\r\n", Muted);
            if (selected.Trace.Count == 0) Append(BoardItemLabel, "No actions observed for this selection yet.\r\n", Muted);
            foreach (var action in selected.Trace.Reverse())
            {
                Append(BoardItemLabel, "S" + action.Season + "  " + action.Action + " " + Target(action.Target) + "\r\n", ForeColor, true);
                Append(BoardItemLabel, action.Result + "\r\n", Muted);
                var effects = new System.Collections.Generic.List<string>();
                if (action.FoodGathered != 0) effects.Add("gathered " + action.FoodGathered + " at target");
                if (action.FoodSpent != 0) effects.Add("spent " + action.FoodSpent);
                if (action.FoodTransferred != 0) effects.Add("transferred " + action.FoodTransferred);
                if (action.Damage != 0) effects.Add("damage " + action.Damage);
                if (action.Healing != 0) effects.Add("healed " + action.Healing);
                if (action.BirthPlaced) effects.Add("child placed");
                if (action.Moved) effects.Add("moved to " + action.DestinationX + ", " + action.DestinationY);
                if (effects.Count != 0) Append(BoardItemLabel, string.Join(" · ", effects) + "\r\n", ForeColor);
                Append(BoardItemLabel, "Whole-season change: reserves " + Signed(action.FoodChange) + ", health " + Signed(action.HealthChange) + "\r\nAssociated reward " + action.Reward.ToString("+0.00;-0.00;0.00") + " · " + action.ChoiceLabel + "\r\n\r\n", Muted);
            }
            RestoreScroll(BoardItemLabel, firstVisibleLine);
            static string Parent(long id) => id == 0 ? "founder" : "#" + id;
            void Welcome()
            {
                Append(BoardItemLabel, "FOLLOW A LIFE\r\n\r\n", Accent, true);
                Append(BoardItemLabel, "Click an organism to follow its movement, DNA and completed actions. Click an empty cell to inspect its food.\r\n\r\nThe food layer includes food under organisms. In Combined view, each colored center is an organism and the surrounding green is local food.\r\n\r\nSeason = one opportunity to act\r\nCycle = eight seasons\r\nGeneration = parent-to-child depth\r\nBiological age = the organism's aging clock\r\nSequence age = DNA clock, which can be inherited", Muted);
            }
        }

        private void ShowComplexStatistics()
        {
            if (displayedFrame == null) return;
            int scroll = -PatternList.AutoScrollPosition.Y;
            PatternList.SuspendLayout();
            foreach (System.Windows.Forms.Control old in PatternList.Controls.Cast<System.Windows.Forms.Control>().ToArray()) old.Dispose();
            PatternList.Controls.Clear();
            foreach (var pattern in displayedFrame.Patterns.Take(12))
            {
                var card = new PatternCard(pattern.Description, pattern.Count, displayedFrame.Population, pattern.Key == patternKey)
                { Width = Math.Max(260, PatternList.ClientSize.Width - 24), Height = Font.Height * 3 + 40, Margin = new Padding(0, 0, 0, 7), Cursor = Cursors.Hand, Font = Font };
                string key = pattern.Key;
                card.Click += (s, e) => { patternKey = patternKey == key ? null : key; BoardVisualizer.HighlightedPattern = patternKey; ShowComplexStatistics(); };
                Tips.SetToolTip(card, pattern.Description + "\r\nGroups ordered action types only. Directions and learned preferences may differ. This is not a fitness ranking.");
                PatternList.Controls.Add(card);
            }
            if (displayedFrame.Patterns.Count == 0) PatternList.Controls.Add(MakeLabel("No living patterns in this world.", 9, false, Muted));
            PatternList.ResumeLayout();
            PatternList.AutoScrollPosition = new Point(0, scroll);
        }
        private void ButtonComplexStats_Click(object sender, EventArgs e) => ShowComplexStatistics();
        private void ShowActivity()
        {
            if (displayedFrame == null) return;
            int firstVisibleLine = FirstVisibleLine(ActivityLabel);
            ActivityLabel.Clear(); Append(ActivityLabel, "GENES ARE NOT ACTIONS\r\n\r\n", Accent, true);
            Append(ActivityLabel, "Gene abundance counts slots in living DNA. Performed actions count committed actions across the run; a gene can be present without ever acting.\r\n\r\n", Muted);
            Append(ActivityLabel, "GENE ABUNDANCE · living slots\r\n", Accent, true);
            foreach (var item in displayedFrame.GeneCounts) Append(ActivityLabel, item.Name.PadRight(17) + "  " + item.Count.ToString("N0") + "\r\n", ForeColor);
            Append(ActivityLabel, "\r\nPERFORMED ACTIONS · run total\r\n", Accent, true);
            foreach (var item in displayedFrame.ActionCounts) Append(ActivityLabel, item.Name.PadRight(17) + "  " + item.Count.ToString("N0") + "\r\n", ForeColor);
            Append(ActivityLabel, "\r\nLast simulation season: " + displayedFrame.LastSeasonMilliseconds.ToString("0.0") + " ms\r\nLast measured throughput: " + displayedFrame.ActualSeasonsPerSecond.ToString("0.0") + " seasons/s\r\n\r\nThe viewer schedules at most 30 updates/s and receives up to 10 fresh frames/s. Repainting does not advance the simulation.\r\n\r\nHistory retains 240 sampled completed seasons. Birth, death and action curves show interval averages per season. The selected trace records each completed season after selection (last 16).\r\n\r\nMarkers show at most 64 events from the latest published season; fast playback may skip events. Trail lines connect completed movement positions.", Muted);
            if (lastSettlement != null) Append(ActivityLabel, "\r\n\r\n" + lastSettlement + " (request to boundary acknowledgment observed by the UI, including display scheduling).", Muted);
            RestoreScroll(ActivityLabel, firstVisibleLine);
        }

        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            if (!closed && simulation != null)
            {
                e.Cancel = true;
                if (!closing)
                {
                    closing = true; BoardRefreshTimer.Stop(); DisableSimulationControls(); PlaybackLabel.Text = "Closing · finishing current season";
                    try { await simulation.StopAsync(); }
                    finally { closed = true; if (!IsDisposed) Close(); }
                }
            }
            base.OnFormClosing(e);
        }
        private void ReleaseSimulationResources()
        {
            if (resourcesReleased) return;
            resourcesReleased = true; BoardRefreshTimer?.Stop(); simulation?.Dispose();
        }
        private static string Signed(int value) => value.ToString("+0;-0;0");
        public static string Target(string target) => target switch
        {
            "TopLeft" => "↖ NW", "TopCenter" => "↑ N", "TopRight" => "↗ NE", "MiddleLeft" => "← W", "Self" => "● self", "MiddleRight" => "→ E", "BottomLeft" => "↙ SW", "BottomCenter" => "↓ S", "BottomRight" => "↘ SE", _ => target
        };
        public static string Configuration(SimulationRunOptions options) => "Random placement " + options.InitialPopulationPercent + "%  ·  " +
            (options.FounderRepertoire == Dna.FounderRepertoire.UnrestrictedRandom ? "Unrestricted random DNA" : "Constrained DNA: gather + reproduce") +
            "  ·  Seed " + options.Seed + "  ·  Food " + options.InitialCellFood + "/10  ·  Regrowth " + options.FoodRegrowthPerAge + "/cycle  ·  Reserves " + options.InitialOrganismFood + "/10  ·  " +
            (options.Policy.Learning == Dna.LearningPolicy.RepairedLegacy ? "Legacy learning" : "Exploratory learning") + " / " +
            (options.Policy.Health == Dna.HealthPolicy.LegacyOverweight ? "overweight rule" : "capped health") + " / " +
            (options.Policy.Attack == Dna.AttackPolicy.DamageOnly ? "damage-only attacks" : "reserve-transfer attacks");

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);
        private static int FirstVisibleLine(RichTextBox control) => control.IsHandleCreated ? (int)SendMessage(control.Handle, 0x00CE, IntPtr.Zero, IntPtr.Zero) : 0;
        private static void RestoreScroll(RichTextBox control, int firstLine)
        {
            control.SelectionStart = 0; control.ScrollToCaret();
            if (control.IsHandleCreated && firstLine > 0) SendMessage(control.Handle, 0x00B6, IntPtr.Zero, new IntPtr(firstLine));
        }
        private static void Append(RichTextBox text, string value, Color color, bool bold = false)
        {
            text.SelectionStart = text.TextLength; text.SelectionLength = 0; text.SelectionColor = color;
            using var font = new Font(text.Font, bold ? FontStyle.Bold : FontStyle.Regular);
            text.SelectionFont = font; text.AppendText(value);
        }
    }
}
