using Fistnet.Genepool.Control.Presentation;
using Godot;

namespace Fistnet.Genepool.GodotViewer;

public partial class Main
{
    public static readonly Color Background = new("0b111a"), Surface = new("131c28"), Ink = new("e1ebf5"), Muted = new("96a9be"), Accent = new("62d9ba"), Warm = new("ffdb85"), Error = new("ff8b95");
    public BoardView2D BoardView { get; private set; } = null!;
    public BoardView3D DepthView { get; private set; } = null!;
    public OptionButton CameraBox { get; private set; } = null!;
    public MinimapView Minimap { get; private set; } = null!;
    public Button RunButton { get; private set; } = null!;
    public Button PauseButton { get; private set; } = null!;
    public Button StepButton { get; private set; } = null!;
    public Button CycleButton { get; private set; } = null!;
    public Button RestartButton { get; private set; } = null!;
    public Button SettingsButton { get; private set; } = null!;
    public OptionButton SpeedBox { get; private set; } = null!;
    public OptionButton LayerBox { get; private set; } = null!;
    public RichTextLabel InspectorText { get; private set; } = null!;
    public RichTextLabel ActivityText { get; private set; } = null!;
    public TabContainer DetailTabs { get; private set; } = null!;
    public CheckBox HistoryToggle { get; private set; } = null!;
    public CheckBox TrailToggle { get; private set; } = null!;
    public CheckBox MarkersToggle { get; private set; } = null!;
    public SettingsDialog Settings { get; private set; } = null!;
    public HistoryView History { get; private set; } = null!;
    public Label PlaybackLabel { get; private set; } = null!;
    private Label StatisticsLabel = null!, ConfigurationLabel = null!, ZoomLabel = null!;
    private VBoxContainer patternList = null!;

    private void BuildLayout()
    {
        if (built) return;
        built = true; Theme = CreateViewerTheme();
        var background = new ColorRect { Color = Background, MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect); AddChild(background);
        var margin = new MarginContainer(); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 18); margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_top", 12); margin.AddThemeConstantOverride("margin_bottom", 10); AddChild(margin);
        var root = new VBoxContainer(); root.AddThemeConstantOverride("separation", 8); margin.AddChild(root);
        var heading = new HBoxContainer(); root.AddChild(heading);
        var title = MakeLabel("GENEPOOL", 27, Accent); title.SizeFlagsHorizontal = SizeFlags.ExpandFill; heading.AddChild(title);
        PlaybackLabel = MakeLabel("PREPARING · a new random world", 15); PlaybackLabel.Name = "PlaybackLabel";
        PlaybackLabel.HorizontalAlignment = HorizontalAlignment.Right; heading.AddChild(PlaybackLabel);
        ConfigurationLabel = MakeLabel("Starting settings will appear here", 12, Muted); ConfigurationLabel.Name = "ConfigurationLabel";
        ConfigurationLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart; root.AddChild(ConfigurationLabel);
        var playback = new HFlowContainer(); playback.AddThemeConstantOverride("h_separation", 7); playback.AddThemeConstantOverride("v_separation", 5); root.AddChild(playback);
        RunButton = MakeButton("RunButton", "▶  Run", () => Send("Run requested", () => simulation!.Run()), true);
        PauseButton = MakeButton("PauseButton", "Ⅱ  Pause", () => Send("Pause requested · finishing current season", () => simulation!.Pause(), "Pause"));
        StepButton = MakeButton("StepButton", "Step season", () => Send("Step requested", () => simulation!.Step()));
        CycleButton = MakeButton("CycleButton", "Cycle · 8", () => Send("Cycle requested · 8 seasons", () => simulation!.Step(8)));
        RestartButton = MakeButton("RestartButton", "Restart", () => { if (activeOptions != null) Send("Restart requested · finishing current season", () => simulation!.Reset(activeOptions), "Restart"); });
        SettingsButton = MakeButton("SettingsButton", "New simulation…", OpenSettings);
        foreach (var button in new[] { RunButton, PauseButton, StepButton, CycleButton, RestartButton, SettingsButton }) { button.Disabled = true; playback.AddChild(button); }
        PauseButton.TooltipText = "Your request is shown immediately. The worker finishes the current season before pausing.";
        CycleButton.TooltipText = "Run eight seasons, then pause. This is a cycle batch, not an organism's biological age.";
        RestartButton.TooltipText = "Restart with the same active configuration and seed.";
        playback.AddChild(MakeLabel("  Target speed", 13, Muted));
        SpeedBox = MakeOptions("SpeedBox", "1 season/s", "5 seasons/s", "15 seasons/s", "30 seasons/s", "60 seasons/s", "Fastest"); SpeedBox.Select(2); SpeedBox.Disabled = true;
        SpeedBox.ItemSelected += index => { if (pendingCommand == 0) TryCommand(() => simulation!.SetSpeed(index == 5 ? null : new double[] { 1, 5, 15, 30, 60 }[(int)index]), out _); };
        SpeedBox.TooltipText = "Requested target, not a guaranteed rate. Actual completed seasons per second appear above."; playback.AddChild(SpeedBox);
        var views = new HFlowContainer(); views.AddThemeConstantOverride("h_separation", 8); views.AddThemeConstantOverride("v_separation", 5); root.AddChild(views);
        views.AddChild(MakeLabel("VIEW", 12, Muted));
        CameraBox = MakeOptions("CameraBox", "Habitat · angled", "Habitat · top-down", "Original 2D");
        CameraBox.ItemSelected += index => SetViewMode((int)index); views.AddChild(CameraBox);
        LayerBox = MakeOptions("LayerBox", "Combined", "Organisms", "Food"); LayerBox.Select(0); views.AddChild(LayerBox);
        LayerBox.ItemSelected += index => { var layer = index == 0 ? BoardLayer.Combined : index == 1 ? BoardLayer.Organisms : BoardLayer.Food; BoardView.Layer = layer; DepthView.Layer = layer; Minimap.Layer = layer; };
        views.AddChild(MakeButton("FitButton", "Fit", FitView));
        views.AddChild(MakeButton("ZoomOutButton", "−", () => ZoomView(.8f)));
        views.AddChild(MakeButton("ZoomInButton", "+", () => ZoomView(1.25f)));
        views.AddChild(MakeButton("FocusButton", "Zoom to selection", FocusView));
        ZoomLabel = MakeLabel("100%", 13, Muted); ZoomLabel.CustomMinimumSize = new Vector2(45, 0); views.AddChild(ZoomLabel);
        TrailToggle = new CheckBox { Name = "TrailToggle", Text = "Selected trail" }; TrailToggle.Toggled += value => { BoardView.ShowTrail = value; DepthView.ShowTrail = value; }; views.AddChild(TrailToggle);
        MarkersToggle = new CheckBox { Name = "MarkersToggle", Text = "Actions", ButtonPressed = true }; MarkersToggle.Toggled += value => { BoardView.ShowMarkers = value; DepthView.ShowMarkers = value; }; views.AddChild(MarkersToggle);
        MarkersToggle.TooltipText = "Habitat: quiet. Organism: muted outcomes throughout the visible area. Inspect: selected organism only. The latest completed season is shown; fast playback can skip seasons. Original 2D retains its limited event markers.";
        HistoryToggle = new CheckBox { Name = "HistoryToggle", Text = "History" }; HistoryToggle.Toggled += value => { historyPreference = value; AdaptHistory(); }; views.AddChild(HistoryToggle);
        HistoryToggle.TooltipText = "Recent sampled history. Hidden initially in shorter windows to give the board more space; you can show it explicitly.";
        var legend = new HBoxContainer(); legend.AddThemeConstantOverride("separation", 2); legend.AddChild(MakeLabel("  Food 0 ", 12, Muted));
        for (int i = 0; i <= 10; i++) { var swatch = new ColorRect { Color = BoardView3D.FoodColor(i), CustomMinimumSize = new Vector2(9, 12), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore }; foodSwatches.Add(swatch); legend.AddChild(swatch); }
        legend.AddChild(MakeLabel(" 10", 12, Muted)); views.AddChild(legend);
        var detailBar = new HBoxContainer(); detailBar.AddThemeConstantOverride("separation", 7); root.AddChild(detailBar);
        foreach (var (level, label) in new[] { (1, "1 · World"), (2, "2 · Habitat"), (3, "3 · Organism"), (4, "4 · Inspect") })
        {
            int requested = level;
            var button = MakeButton("DetailLevel" + level, label, () => { if (!IsDepthView) SetViewMode(0); DepthView.SetDetailLevel(requested); });
            button.ToggleMode = true; detailButtons.Add(button);
            detailBar.AddChild(button);
        }
        ViewHint = MakeLabel("Whole world · zoom in to explore", 12, Muted); ViewHint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ViewHint.AutowrapMode = TextServer.AutowrapMode.WordSmart; detailBar.AddChild(ViewHint);
        var content = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; content.AddThemeConstantOverride("separation", 12); root.AddChild(content);
        var boardPanel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill }; content.AddChild(boardPanel);
        var boardColumn = new VBoxContainer(); boardColumn.AddThemeConstantOverride("separation", 3); boardPanel.AddChild(boardColumn);
        StatisticsLabel = MakeLabel("A fresh population is being prepared…", 14); StatisticsLabel.Name = "StatisticsLabel"; boardColumn.AddChild(StatisticsLabel);
        BoardView = new BoardView2D { Name = "BoardView", SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        var boardSurface = new Godot.Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(240,240), ClipContents = true };
        boardColumn.AddChild(boardSurface);
        BoardView.CellPicked += SelectCell; boardSurface.AddChild(BoardView); BoardView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        DepthView = new BoardView3D { Name = "DepthView", ShowMarkers = true };
        DepthView.CellPicked += SelectCell; boardSurface.AddChild(DepthView); DepthView.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Minimap = new MinimapView { Name = "Minimap", MouseFilter = MouseFilterEnum.Stop };
        Minimap.Navigate += (x, y) => DepthView.CenterOn(x, y);
        boardSurface.AddChild(Minimap); Minimap.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
        Minimap.OffsetLeft = -142; Minimap.OffsetTop = 12; Minimap.OffsetRight = -12; Minimap.OffsetBottom = 157;
        DepthView.ViewChanged += UpdateViewStatus;
        SetViewMode(0);
        DetailTabs = new TabContainer { Name = "DetailTabs", CustomMinimumSize = new Vector2(340, 0), SizeFlagsVertical = SizeFlags.ExpandFill }; content.AddChild(DetailTabs);
        var inspector = new MarginContainer { Name = "Organism" }; Pad(inspector, 12); DetailTabs.AddChild(inspector);
        InspectorText = TextPanel("InspectorText"); inspector.AddChild(InspectorText);
        var patterns = new MarginContainer { Name = "Patterns" }; Pad(patterns, 12); DetailTabs.AddChild(patterns);
        var patternsBody = new VBoxContainer(); patternsBody.AddThemeConstantOverride("separation", 12); patterns.AddChild(patternsBody);
        var note = MakeLabel("Most common action patterns\nOrdered action types only · not fitness.\nDirections and preferences can differ.\nClick a row to highlight; again to clear.", 12, Muted); note.AutowrapMode = TextServer.AutowrapMode.WordSmart; patternsBody.AddChild(note);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; patternsBody.AddChild(scroll);
        patternList = new VBoxContainer { Name = "PatternList", SizeFlagsHorizontal = SizeFlags.ExpandFill }; patternList.AddThemeConstantOverride("separation", 7); scroll.AddChild(patternList);
        var activity = new MarginContainer { Name = "Activity" }; Pad(activity, 12); DetailTabs.AddChild(activity);
        ActivityText = TextPanel("ActivityText"); activity.AddChild(ActivityText);
        History = new HistoryView { Name = "HistoryView", CustomMinimumSize = new Vector2(0, 135) }; root.AddChild(History);
        var footer = new HBoxContainer(); root.AddChild(footer);
        var hint = MakeLabel("Click to inspect · Wheel to zoom · Right / middle drag to pan · Board 100 × 100", 12, Muted); hint.SizeFlagsHorizontal = SizeFlags.ExpandFill; footer.AddChild(hint);
        footer.AddChild(MakeLabel("GPU board · completed snapshots", 12, Muted));
        Settings = new SettingsDialog { Name = "SettingsDialog", Visible = false }; AddChild(Settings);
        Settings.WorldRequested += options => Send("New world requested · finishing current season", () => simulation!.Reset(options), "New world");
        UpdateInspector(); RefreshControls();
    }

    private void AdaptHistory()
    {
        if (!built) return;
        bool visible = historyPreference ?? Size.Y >= 850;
        History.Visible = visible; HistoryToggle.SetPressedNoSignal(visible);
    }

    public static Theme CreateViewerTheme()
    {
        var theme = new Theme { DefaultFontSize = 14 };
        theme.SetColor("font_color", "Label", Ink); theme.SetColor("default_color", "RichTextLabel", Ink);
        foreach (string type in new[] { "Button", "OptionButton" })
        {
            theme.SetStylebox("normal", type, Box(new Color("1d2a3b"), new Color("35485d")));
            theme.SetStylebox("hover", type, Box(new Color("2a4054"), Accent));
            theme.SetStylebox("pressed", type, Box(new Color("23403d"), Accent));
            theme.SetStylebox("disabled", type, Box(new Color("17212e"), new Color("273545")));
            theme.SetStylebox("focus", type, Box(Colors.Transparent, Accent));
            theme.SetColor("font_color", type, Ink); theme.SetColor("font_disabled_color", type, new Color("708298"));
        }
        theme.SetStylebox("panel", "PanelContainer", Box(Surface, new Color("29394b"), 10));
        theme.SetStylebox("panel", "TabContainer", Box(Surface, new Color("29394b"), 3));
        theme.SetStylebox("tab_selected", "TabContainer", Box(new Color("23403d"), new Color("466f68")));
        theme.SetStylebox("tab_unselected", "TabContainer", Box(new Color("172230"), new Color("29394b")));
        theme.SetStylebox("normal", "LineEdit", Box(new Color("1d2a3b"), new Color("35485d")));
        theme.SetColor("font_color", "LineEdit", Ink); return theme;
    }

    private static StyleBoxFlat Box(Color background, Color border, int padding = 9) => new()
    {
        BgColor = background, BorderColor = border, BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
        ContentMarginLeft = padding, ContentMarginRight = padding, ContentMarginTop = 6, ContentMarginBottom = 6
    };
    public static Label MakeLabel(string text, int size = 14, Color? color = null)
    {
        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", size); label.AddThemeColorOverride("font_color", color ?? Ink); return label;
    }
    public static Button MakeButton(string name, string text, Action action, bool accent = false)
    {
        var button = new Button { Name = name, Text = text, CustomMinimumSize = new Vector2(0, 34) };
        if (accent) button.AddThemeStyleboxOverride("normal", Box(new Color("256055"), new Color("4aa990")));
        button.Pressed += action; return button;
    }
    public static OptionButton MakeOptions(string name, params string[] items)
    {
        var options = new OptionButton { Name = name, CustomMinimumSize = new Vector2(0, 34) };
        foreach (string item in items) options.AddItem(item); return options;
    }
    private static RichTextLabel TextPanel(string name) => new()
    {
        Name = name, BbcodeEnabled = false, SelectionEnabled = true, ScrollActive = true, ScrollFollowing = false,
        AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill
    };
    private static void Pad(MarginContainer container, int padding)
    { foreach (string side in new[] { "left", "right", "top", "bottom" }) container.AddThemeConstantOverride("margin_" + side, padding); }
}
