using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Presentation;
using Fistnet.Genepool.Dna;
using Godot;

namespace Fistnet.Genepool.GodotViewer;

public partial class SettingsDialog : Window
{
    public event Action<SimulationRunOptions>? WorldRequested;
    public SimulationSettings Draft { get; private set; } = null!;
    public SpinBox Density { get; private set; } = null!;
    public SpinBox SeedValue { get; private set; } = null!;
    public CheckBox RandomSeed { get; private set; } = null!;
    public SpinBox CellFood { get; private set; } = null!;
    public SpinBox Regrowth { get; private set; } = null!;
    public SpinBox Reserves { get; private set; } = null!;
    public OptionButton Preset { get; private set; } = null!;
    public OptionButton FounderDna { get; private set; } = null!;
    public OptionButton Learning { get; private set; } = null!;
    public OptionButton Vitality { get; private set; } = null!;
    public OptionButton Predation { get; private set; } = null!;
    public Button CreateButton { get; private set; } = null!;
    private Label error = null!;
    private bool built;

    public override void _Ready() => Build();

    public void Open(SimulationRunOptions current)
    {
        Build(); Draft = new SimulationSettings(current); Preset.Select(0); LoadDraft();
        error.Text = "";
        PopupCenteredClamped(new Vector2I(630, 710), .92f);
    }

    public void ApplyPreset(int index)
    {
        Draft.ApplyPreset(index); Preset.Select(index); LoadDraft(); error.Text = "";
    }

    private void Build()
    {
        if (built) return;
        built = true; Title = "New simulation · starting settings"; Visible = false;
        Transient = true; Exclusive = true; MinSize = new Vector2I(460, 370);
        Theme = Main.CreateViewerTheme(); CloseRequested += Hide;
        var background = new ColorRect { Color = Main.Background, MouseFilter = Godot.Control.MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(Godot.Control.LayoutPreset.FullRect); AddChild(background);
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(Godot.Control.LayoutPreset.FullRect);
        foreach (string side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 20);
        AddChild(margin);
        var shell = new VBoxContainer(); shell.AddThemeConstantOverride("separation", 12); margin.AddChild(shell);
        var scroll = new ScrollContainer { SizeFlagsVertical = Godot.Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        shell.AddChild(scroll);
        var body = new VBoxContainer { SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12); scroll.AddChild(body);
        body.AddChild(Main.MakeLabel("A new world, your starting point", 23, Main.Accent));
        body.AddChild(Note("Unrestricted random DNA is the default. Optional constrained founders include gather and reproduction genes; targets stay random. These settings apply only to a new run, without rescue."));
        var grid = new GridContainer { Columns = 2 }; grid.AddThemeConstantOverride("h_separation", 18); grid.AddThemeConstantOverride("v_separation", 10); body.AddChild(grid);
        Density = Number("Density", 0, 100); SeedValue = Number("SeedValue", int.MinValue, int.MaxValue);
        CellFood = Number("CellFood", 0, 10); Regrowth = Number("Regrowth", 0, 10); Reserves = Number("Reserves", 0, 10);
        Row("Initial population (%)", Density);
        var seedRow = new HBoxContainer(); SeedValue.CustomMinimumSize = new Vector2(140, 34); seedRow.AddChild(SeedValue);
        RandomSeed = new CheckBox { Name = "RandomSeed", Text = "Random" }; seedRow.AddChild(RandomSeed);
        RandomSeed.Toggled += value => SeedValue.Editable = !value;
        Row("Seed", seedRow); Row("Food in each cell (0–10)", CellFood); Row("Food regrowth per cycle (0–10)", Regrowth); Row("Founder reserves (0–10)", Reserves);
        FounderDna = Main.MakeOptions("FounderDna", "Unrestricted random (default)", "Gather + reproduce"); Row("Founder DNA", FounderDna);
        FounderDna.TooltipText = "Two distinct random slots contain gather and reproduction. The other six genes and every direction remain random. This does not guarantee feeding, births or survival. Descendants inherit and mutate normally.";
        Preset = Main.MakeOptions("Preset", SimulationSettings.PresetNames.ToArray()); Row("Comparison preset", Preset);
        Preset.ItemSelected += index => ApplyPreset((int)index);
        body.AddChild(Note("ADVANCED · CANDIDATE POLICIES\nOptional comparisons, not recommended or proven balance settings.", Main.Warm));
        var policies = new GridContainer { Columns = 2 }; policies.AddThemeConstantOverride("h_separation", 18); policies.AddThemeConstantOverride("v_separation", 10); body.AddChild(policies);
        Learning = Main.MakeOptions("Learning", "Repaired legacy", "Bounded exploration");
        Vitality = Main.MakeOptions("Vitality", "Legacy overweight rule", "Capped health");
        Predation = Main.MakeOptions("Predation", "Damage only", "Actual reserve transfer");
        PolicyRow("Learning", Learning); PolicyRow("Vitality", Vitality); PolicyRow("Predation", Predation);
        body.AddChild(Note("A cycle is eight seasons. The board stays 100 × 100. The chosen seed and active settings remain visible in the viewer. Restart repeats the same configuration and seed."));
        error = Note("", Main.Error); error.Name = "SettingsError"; body.AddChild(error);
        var actions = new HBoxContainer(); actions.AddThemeConstantOverride("separation", 10); shell.AddChild(actions);
        var defaults = Main.MakeButton("DefaultsButton", "Restore defaults", () => ApplyPreset(1)); actions.AddChild(defaults);
        actions.AddChild(new Godot.Control { SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill });
        actions.AddChild(Main.MakeButton("CancelButton", "Cancel", Hide));
        CreateButton = Main.MakeButton("CreateButton", "Create world", CreateWorld, true); actions.AddChild(CreateButton);
        void Row(string label, Godot.Control control) { grid.AddChild(FieldLabel(label)); control.SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill; grid.AddChild(control); }
        void PolicyRow(string label, Godot.Control control) { policies.AddChild(FieldLabel(label)); control.SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill; policies.AddChild(control); }
    }

    private void LoadDraft()
    {
        Density.Value = Draft.Density; SeedValue.Value = Draft.Seed; CellFood.Value = Draft.CellFood;
        Regrowth.Value = Draft.Regrowth; Reserves.Value = Draft.Reserves;
        FounderDna.Select((int)Draft.FounderDna); Learning.Select((int)Draft.Learning);
        Vitality.Select((int)Draft.Health); Predation.Select((int)Draft.Attack);
        RandomSeed.ButtonPressed = Draft.RandomSeed; SeedValue.Editable = !Draft.RandomSeed;
        var policy = Draft.LoadedPolicy;
        Learning.TooltipText = $"Bounded option: {policy.ExternalContextLimit} external contexts, {policy.ExplorationPercent}% exploration. Unseen contexts are tried first.";
        Vitality.TooltipText = $"Capped option: maximum health {policy.MaximumHealth}. Legacy overweight behavior remains available.";
        Predation.TooltipText = $"Reserve transfer: attack costs {policy.AttackCost}; direct lethal hits transfer up to {policy.AttackFoodLimit} actual victim reserves within carrying capacity.";
    }

    public SimulationRunOptions ReadOptions(int generatedSeed)
    {
        foreach (var number in new[] { Density, SeedValue, CellFood, Regrowth, Reserves }) number.Apply();
        Draft.Density = (int)Density.Value; Draft.Seed = (int)SeedValue.Value; Draft.RandomSeed = RandomSeed.ButtonPressed;
        Draft.CellFood = (int)CellFood.Value; Draft.Regrowth = (int)Regrowth.Value; Draft.Reserves = (int)Reserves.Value;
        Draft.FounderDna = (FounderRepertoire)FounderDna.Selected; Draft.Learning = (LearningPolicy)Learning.Selected;
        Draft.Health = (HealthPolicy)Vitality.Selected; Draft.Attack = (AttackPolicy)Predation.Selected;
        return Draft.CreateOptions(generatedSeed);
    }

    private void CreateWorld()
    {
        try
        {
            var options = ReadOptions(RandomSeed.ButtonPressed ? System.Security.Cryptography.RandomNumberGenerator.GetInt32(int.MaxValue) : 0);
            Hide(); WorldRequested?.Invoke(options);
        }
        catch (ArgumentException exception) { error.Text = "Please check the starting settings. " + exception.Message; }
    }

    private static SpinBox Number(string name, double minimum, double maximum) => new()
    // Commit complete input on submission/focus loss or ReadOptions; a lone '-' must remain editable.
    { Name = name, MinValue = minimum, MaxValue = maximum, Step = 1, Rounded = true, CustomMinimumSize = new Vector2(160, 34), UpdateOnTextChanged = false };
    private static Label FieldLabel(string text) => new() { Text = text, CustomMinimumSize = new Vector2(225, 0), VerticalAlignment = VerticalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private static Label Note(string text, Color? color = null)
    {
        var label = Main.MakeLabel(text, 14, color ?? Main.Muted); label.AutowrapMode = TextServer.AutowrapMode.WordSmart; return label;
    }
}
