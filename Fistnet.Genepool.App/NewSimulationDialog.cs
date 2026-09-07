using System;
using System.ComponentModel;
using System.Drawing;
using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Windows.Forms;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Dna;

namespace Fistnet.Genepool.App
{
    [SupportedOSPlatform("windows6.1")]
    public sealed class NewSimulationDialog : Form
    {
        private readonly NumericUpDown Density, SeedValue, CellFood, Regrowth, Reserves;
        private readonly CheckBox RandomSeed;
        private readonly ComboBox Learning, Vitality, Predation, Preset, FounderDna;
        private readonly Panel SettingsBody;
        private readonly Button CreateButton, CancelActionButton, DefaultsButton;
        private SimulationPolicy policy;
        private SimulationRunOptions original;
        private readonly ToolTip tips = new ToolTip { AutoPopDelay = 18000 };
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SimulationRunOptions Options { get; private set; }

        public NewSimulationDialog(SimulationRunOptions current = null)
        {
            original = current ?? new SimulationRunOptions(); policy = original.Policy;
            Text = "New simulation · starting settings"; StartPosition = FormStartPosition.CenterParent;
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(560, 694); MinimumSize = new Size(500, 400); FormBorderStyle = FormBorderStyle.Sizable; MaximizeBox = false; MinimizeBox = false;
            Font = new Font("Segoe UI", 9.5f); BackColor = Color.FromArgb(19, 28, 40); ForeColor = Color.FromArgb(225, 235, 245);
            var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            Controls.Add(shell);
            SettingsBody = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
            shell.Controls.Add(SettingsBody, 0, 0);
            var layout = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(22), ColumnCount = 2, RowCount = 14 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 53)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 47));
            for (int i = 0; i < 14; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, i == 0 ? 53 : i == 1 ? 62 : i == 9 ? 62 : i == 13 ? 77 : 36));
            SettingsBody.Controls.Add(layout);
            var title = TextLabel("A new world, your starting point", true); title.Font = new Font(Font.FontFamily, 15, FontStyle.Bold); title.ForeColor = Color.FromArgb(98, 217, 186);
            layout.Controls.Add(title, 0, 0); layout.SetColumnSpan(title, 2);
            var explanation = TextLabel("Unrestricted random DNA is the default. Optional constrained DNA includes gather and reproduction genes; targets stay random. Settings apply only to a new run, without rescue.");
            layout.Controls.Add(explanation, 0, 1); layout.SetColumnSpan(explanation, 2);
            Density = Number(0, 100); SeedValue = Number(int.MinValue, int.MaxValue); CellFood = Number(0, 10); Regrowth = Number(0, 10); Reserves = Number(0, 10);
            AddRow(2, "Initial population (%)", Density);
            var seedPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            SeedValue.Width = 127; RandomSeed = new CheckBox { Text = "Random", AutoSize = true, ForeColor = ForeColor, Margin = new Padding(7, 4, 0, 0) };
            RandomSeed.CheckedChanged += (s, e) => SeedValue.Enabled = !RandomSeed.Checked;
            seedPanel.Controls.AddRange(new System.Windows.Forms.Control[] { SeedValue, RandomSeed }); AddRow(3, "Seed", seedPanel);
            AddRow(4, "Food in each cell (0–10)", CellFood);
            AddRow(5, "Food regrowth per cycle (0–10)", Regrowth);
            AddRow(6, "Founder reserves (0–10)", Reserves);
            FounderDna = Combo(new[] { "Unrestricted random (default)", "Gather + reproduce" });
            AddRow(7, "Founder DNA", FounderDna);
            tips.SetToolTip(FounderDna, "Constrained founders have at least one gather and one reproduction gene: two random distinct slots are reserved, and the other six genes and all directions remain random. This does not guarantee available targets, feeding, births or survival. Descendants inherit and mutate normally.");
            var presetsLabel = TextLabel("Comparison presets", true); layout.Controls.Add(presetsLabel, 0, 8);
            Preset = Combo(new[] { "Current settings", "Random defaults", "Exploratory learning only", "Capped health only", "Reserve-transfer predation only", "Gather + reproduce repertoire", "Food regrowth 2" }); layout.Controls.Add(Preset, 1, 8);
            Preset.DropDownWidth = 320;
            var advanced = TextLabel("Advanced · candidate policies\r\nThese are optional comparisons, not recommended or proven balance settings."); advanced.ForeColor = Color.FromArgb(239, 204, 126);
            layout.Controls.Add(advanced, 0, 9); layout.SetColumnSpan(advanced, 2);
            Learning = Combo(new[] { "Repaired legacy", "Bounded exploration" });
            Vitality = Combo(new[] { "Legacy overweight rule", "Capped health" });
            Predation = Combo(new[] { "Damage only", "Actual reserve transfer" });
            AddRow(10, "Learning", Learning); AddRow(11, "Vitality", Vitality); AddRow(12, "Predation", Predation);
            var units = TextLabel("A cycle is eight seasons. The 100 × 100 board is unchanged. The chosen seed and active settings remain visible in the viewer. Restart repeats the same configuration and seed.");
            layout.Controls.Add(units, 0, 13); layout.SetColumnSpan(units, 2);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = Padding.Empty, Padding = new Padding(14, 12, 22, 12) };
            CreateButton = Button("Create world", 120); CreateButton.Click += Create_Click;
            CancelActionButton = Button("Cancel", 82); CancelActionButton.DialogResult = DialogResult.Cancel;
            DefaultsButton = Button("Restore defaults", 136); DefaultsButton.Click += (s, e) => { LoadOptions(new SimulationRunOptions()); Preset.SelectedIndex = 1; };
            buttons.Controls.AddRange(new System.Windows.Forms.Control[] { CreateButton, CancelActionButton, DefaultsButton }); shell.Controls.Add(buttons, 0, 1);
            AcceptButton = CreateButton; CancelButton = CancelActionButton;
            Preset.SelectedIndexChanged += (s, e) =>
            {
                int index = Preset.SelectedIndex;
                LoadOptions(index == 0 ? original : new SimulationRunOptions());
                if (index == 2) Learning.SelectedIndex = 1;
                if (index == 3) Vitality.SelectedIndex = 1;
                if (index == 4) Predation.SelectedIndex = 1;
                if (index == 5) FounderDna.SelectedIndex = 1;
                if (index == 6) Regrowth.Value = 2;
            };
            Preset.SelectedIndex = 0;
            void AddRow(int row, string label, System.Windows.Forms.Control control)
            { layout.Controls.Add(TextLabel(label), 0, row); layout.Controls.Add(control, 1, row); }
        }
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Rectangle work = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(MinimumSize.Width, work.Width - 16), Math.Min(MinimumSize.Height, work.Height - 16));
            Size = new Size(Math.Min(Width, work.Width - 16), Math.Min(Height, work.Height - 16));
        }
        private void LoadOptions(SimulationRunOptions options)
        {
            policy = options.Policy;
            Density.Value = options.InitialPopulationPercent; SeedValue.Value = options.Seed; CellFood.Value = options.InitialCellFood;
            Regrowth.Value = options.FoodRegrowthPerAge; Reserves.Value = options.InitialOrganismFood;
            FounderDna.SelectedIndex = (int)options.FounderRepertoire;
            Learning.SelectedIndex = (int)options.Policy.Learning; Vitality.SelectedIndex = (int)options.Policy.Health; Predation.SelectedIndex = (int)options.Policy.Attack;
            tips.SetToolTip(Learning, "Bounded option: " + policy.ExternalContextLimit + " external contexts and " + policy.ExplorationPercent + "% exploration. Unseen contexts are tried first.");
            tips.SetToolTip(Vitality, "Capped option: maximum health " + policy.MaximumHealth + ". Legacy overweight behavior remains available.");
            tips.SetToolTip(Predation, "Reserve-transfer option: attack costs " + policy.AttackCost + ", and transfers up to " + policy.AttackFoodLimit + " actual victim reserves only on a direct lethal hit, within carrying capacity.");
            RandomSeed.Checked = true;
        }
        private void Create_Click(object sender, EventArgs e)
        {
            var selected = new SimulationRunOptions
            {
                Mode = original.Mode, CellExecution = original.CellExecution,
                Seed = RandomSeed.Checked ? RandomNumberGenerator.GetInt32(int.MaxValue) : (int)SeedValue.Value,
                InitialPopulationPercent = (int)Density.Value, InitialCellFood = (int)CellFood.Value,
                FoodRegrowthPerAge = (int)Regrowth.Value, InitialOrganismFood = (int)Reserves.Value,
                FounderRepertoire = (FounderRepertoire)FounderDna.SelectedIndex,
                Policy = new SimulationPolicy
                {
                    Learning = (LearningPolicy)Learning.SelectedIndex, Health = (HealthPolicy)Vitality.SelectedIndex,
                    Attack = (AttackPolicy)Predation.SelectedIndex, ExternalContextLimit = policy.ExternalContextLimit,
                    ExplorationPercent = policy.ExplorationPercent, MaximumHealth = policy.MaximumHealth,
                    AttackCost = policy.AttackCost, AttackFoodLimit = policy.AttackFoodLimit
                }
            };
            selected.Validate(); Options = selected; DialogResult = DialogResult.OK; Close();
        }
        private Label TextLabel(string text, bool bold = false) => new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font(Font, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = ForeColor, Margin = new Padding(0, 2, 8, 2) };
        private NumericUpDown Number(decimal minimum, decimal maximum) => new NumericUpDown { Minimum = minimum, Maximum = maximum, Width = 130, BackColor = Color.FromArgb(29, 42, 59), ForeColor = ForeColor, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0, 5, 0, 0), Font = Font };
        private ComboBox Combo(string[] choices)
        {
            var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Color.FromArgb(29, 42, 59), ForeColor = ForeColor, FlatStyle = FlatStyle.Flat, Font = Font, Margin = new Padding(0, 4, 0, 0) };
            combo.Items.AddRange(choices); return combo;
        }
        private Button Button(string title, int width) => new Button { Text = title, Width = width, Height = 34, BackColor = Color.FromArgb(34, 62, 66), ForeColor = ForeColor, FlatStyle = FlatStyle.Flat, Margin = new Padding(8, 0, 0, 0), Font = Font };
        protected override void Dispose(bool disposing) { if (disposing) tips.Dispose(); base.Dispose(disposing); }
    }
}
