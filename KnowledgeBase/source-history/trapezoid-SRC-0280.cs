using System.Diagnostics;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Presentation;
using Godot;
using BoardLayer = Fistnet.Genepool.Control.Presentation.BoardLayer;

namespace Fistnet.Genepool.GodotViewer;

/// <summary>Detached observation rendered as two visible-cell GPU batches on an XZ board.</summary>
public partial class BoardView3D : global::Godot.Control
{
    private const int Stride = 20;
    private static readonly Color[] ColonyPalette = { new("72c9df"), new("d0b1df"), new("ecc889"), new("f1b85f"), new("87d5be"), new("dbaba4"), new("a7bddf") };
    private ViewFrame? frame;
    private SubViewport? viewport;
    private Camera3D? camera;
    private MultiMesh? foodMesh, organismMesh;
    private MultiMeshInstance3D? foodInstances, organismInstances;
    private ShaderMaterial? groundMaterial, colonyMaterial;
    private Node2D? overlay;
    private float[] foodBuffer = Array.Empty<float>(), organismBuffer = Array.Empty<float>();
    private int capacity;
    private bool angled = true, dragging, showTrail, showMarkers = true;
    private float zoom = 1;
    private Vector2 center = new(50, 50), dragGroundAnchor;
    private Vector2I lastViewportSize;
    private BoardLayer layer;
    private string? highlightedPattern;
    private long? selectedOrganismId;
    private (int X, int Y)? selectedCell;
    private long markerDeadline;
    private bool markersExpired = true;

    public event Action<int, int>? CellPicked;
    public event Action? ViewChanged;
    public ViewFrame? Frame => frame;
    public SubViewport BoardViewport => viewport ?? throw new InvalidOperationException("Board is not ready.");
    public Camera3D BoardCamera => camera ?? throw new InvalidOperationException("Board is not ready.");
    public float Zoom => zoom;
    public bool Angled { get => angled; set => SetAngled(value); }
    public float ProjectedCellSize => projectedCellSize;
    public int DetailLevel => ProjectedCellSize < 12 ? 1 : ProjectedCellSize < 32 ? 2 : ProjectedCellSize < 72 ? 3 : 4;
    public int FoodInstanceCount { get; private set; }
    public int OrganismInstanceCount { get; private set; }
    public int ActionCueCount { get; private set; }
    public double LastUploadMilliseconds { get; private set; }
    public bool MarkersVisible => showMarkers && !markersExpired && frame != null;
    public bool SelectionIsHistorical => selectedOrganismId.HasValue && frame?.Selected != null &&
        frame.Selected.Id == selectedOrganismId && !frame.Selected.IsAlive;
    public Rect2 VisibleArea => visibleArea;
    public Rect2I VisibleBounds => visibleBounds;
    public BoardLayer Layer
    {
        get => layer;
        set { if (layer == value) return; if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value)); layer = value; UploadFrame(); }
    }
    public string? HighlightedPattern
    {
        get => highlightedPattern;
        set { if (highlightedPattern == value) return; highlightedPattern = value; UploadFrame(); }
    }
    public long? SelectedOrganismId
    {
        get => selectedOrganismId;
        set { if (selectedOrganismId == value) return; selectedOrganismId = value; overlay?.QueueRedraw(); }
    }
    public (int X, int Y)? SelectedCell
    {
        get => selectedCell;
        set { selectedCell = value; overlay?.QueueRedraw(); }
    }
    public bool ShowTrail { get => showTrail; set { showTrail = value; overlay?.QueueRedraw(); } }
    public bool ShowMarkers { get => showMarkers; set { showMarkers = value; overlay?.QueueRedraw(); } }

    public override void _Ready()
    {
        ClipContents = true; MouseFilter = MouseFilterEnum.Stop; FocusMode = FocusModeEnum.Click;
        MouseDefaultCursorShape = CursorShape.Drag;
        CustomMinimumSize = new Vector2(240, 240);
        SizeFlagsHorizontal = SizeFlags.ExpandFill; SizeFlagsVertical = SizeFlags.ExpandFill;
        TooltipText = "Wheel to explore four detail levels. Right or middle drag to pan. Click an organism to focus.";
        var container = new SubViewportContainer
        {
            Name = "DepthViewportContainer", Stretch = true, StretchShrink = 1,
            MouseFilter = MouseFilterEnum.Ignore, MouseTarget = false
        };
        AddChild(container); container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        viewport = new SubViewport
        {
            Name = "DepthViewport", Disable3D = false, TransparentBg = false, OwnWorld3D = true,
            Size = new Vector2I(Math.Max(2, (int)Size.X), Math.Max(2, (int)Size.Y)),
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible
        };
        container.AddChild(viewport);
        var world = new Node3D { Name = "LivingBoard" }; viewport.AddChild(world);
        groundMaterial = CreateMaterial(GroundShader);
        colonyMaterial = CreateMaterial(ColonyShader);
        foodMesh = CreateMesh(); organismMesh = CreateMesh();
        foodInstances = new MultiMeshInstance3D { Name = "VisibleGround", Multimesh = foodMesh, MaterialOverride = groundMaterial };
        organismInstances = new MultiMeshInstance3D { Name = "VisibleColonies", Multimesh = organismMesh, MaterialOverride = colonyMaterial };
        world.AddChild(foodInstances); world.AddChild(organismInstances);
        camera = new Camera3D
        {
            Name = "BoardCamera", Projection = Camera3D.ProjectionType.Perspective,
            KeepAspect = Camera3D.KeepAspectEnum.Height, Near = .1f, Far = 500, Current = true,
            Environment = new global::Godot.Environment
            {
                BackgroundMode = global::Godot.Environment.BGMode.Color, BackgroundColor = new Color("11191d"),
                AmbientLightSource = global::Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Colors.White, AmbientLightEnergy = 1
            }
        };
        world.AddChild(camera);
        overlay = new Node2D { Name = "ObservedActionOverlay" }; viewport.AddChild(overlay); overlay.Draw += DrawOverlay;
        Resized += UpdateCamera;
        UpdateCamera();
    }

    private static ShaderMaterial CreateMaterial(string code) => new() { Shader = new Shader { Code = code } };
    private static MultiMesh CreateMesh() => new()
    {
        TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true,
        Mesh = new PlaneMesh { Size = Vector2.One }
    };

    public void SetFrame(ViewFrame? value)
    {
        if (ReferenceEquals(frame, value)) return;
        bool newWorld = frame == null || value == null || frame.RunId != value.RunId;
        bool dimensionsChanged = frame?.Width != value?.Width || frame?.Height != value?.Height;
        bool newSeason = newWorld || frame!.Season != value!.Season;
        frame = value;
        if (newWorld)
        {
            selectedCell = null; selectedOrganismId = null;
            center = new Vector2((frame?.Width ?? 100) / 2f, (frame?.Height ?? 100) / 2f);
        }
        if (newSeason)
        {
            markerDeadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 3 / 4;
            markersExpired = value == null;
        }
        ExpireMarkers();
        if (dimensionsChanged || newWorld) UpdateCamera(); else UploadFrame();
    }

    public static Color FoodColor(int quantity)
    {
        float amount = Math.Clamp(quantity, 0, 10) / 10f;
        var dirt = new Color("735039"); var middle = new Color("91854a"); var grass = new Color("689044");
        return amount < .5f ? dirt.Lerp(middle, amount * 2) : middle.Lerp(grass, (amount - .5f) * 2);
    }

    /// <summary>A stable display palette groups colours for readability, not species or unique DNA identity.</summary>
    public static Color ColonyColor(ViewCell cell, string? highlightedPattern = null)
    {
        uint hash = 2166136261;
        foreach (char c in cell.PatternKey ?? "") hash = (hash ^ c) * 16777619;
        if (string.IsNullOrEmpty(cell.PatternKey)) hash = unchecked((uint)cell.ColorArgb);
        Color color = ColonyPalette[hash % (uint)ColonyPalette.Length];
        return highlightedPattern != null && cell.PatternKey != highlightedPattern ? color.Darkened(.64f) : color;
    }

    private void UploadFrame()
    {
        if (foodMesh == null || organismMesh == null) return;
        long started = Stopwatch.GetTimestamp();
        if (frame == null)
        {
            foodMesh.VisibleInstanceCount = organismMesh.VisibleInstanceCount = 0;
            FoodInstanceCount = OrganismInstanceCount = ActionCueCount = 0; overlay?.QueueRedraw(); return;
        }
        if (frame.Width <= 0 || frame.Height <= 0 || (long)frame.Width * frame.Height > 1_000_000)
            throw new ArgumentException("Invalid board dimensions.", nameof(frame));
        Rect2I bounds = VisibleBounds;
        int required = Math.Max(1, checked(bounds.Size.X * bounds.Size.Y));
        // Capacity tracks the visible working set. Shrink after a substantial zoom, without churn while panning.
        if (capacity < required || capacity > Math.Max(256, required * 3))
        {
            capacity = Math.Min(frame.Width * frame.Height, Math.Max(64, ((required + 63) / 64) * 64));
            foodMesh.InstanceCount = organismMesh.InstanceCount = capacity;
            foodBuffer = new float[capacity * Stride]; organismBuffer = new float[capacity * Stride];
        }
        var aabb = new Aabb(new Vector3(0, -.1f, 0), new Vector3(frame.Width, 1, frame.Height));
        foodMesh.CustomAabb = organismMesh.CustomAabb = aabb;
        int groundCount = 0, colonyCount = 0;
        foreach (ViewCell cell in frame.Cells)
        {
            if (!IsCellVisible(cell.X, cell.Y)) continue;
            if (groundCount >= capacity) break;
            float seed = ColonySeed(cell);
            Color ground = layer == BoardLayer.Organisms ? new Color("453b32") : FoodColor(cell.Food);
            WriteInstance(foodBuffer, groundCount++, cell.X + .5f, 0, cell.Y + .5f, 1, ground,
                layer == BoardLayer.Organisms ? 0 : Math.Clamp(cell.Food, 0, 10) / 10f, CellSeed(cell.X, cell.Y, 0));
            if (cell.OrganismId.HasValue && layer != BoardLayer.Food)
                WriteInstance(organismBuffer, colonyCount++, cell.X + .5f, .024f, cell.Y + .5f,
                    DetailLevel == 1 ? .64f : .92f, ColonyColor(cell, highlightedPattern), seed, 0);
        }
        groundMaterial!.SetShaderParameter("cell_pixels", ProjectedCellSize);
        colonyMaterial!.SetShaderParameter("cell_pixels", ProjectedCellSize);
        RenderingServer.MultimeshSetBuffer(foodMesh.GetRid(), foodBuffer);
        if (colonyCount > 0) RenderingServer.MultimeshSetBuffer(organismMesh.GetRid(), organismBuffer);
        foodMesh.VisibleInstanceCount = FoodInstanceCount = groundCount;
        organismMesh.VisibleInstanceCount = OrganismInstanceCount = colonyCount;
        LastUploadMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        overlay?.QueueRedraw();
    }

    private static float CellSeed(int x, int y, long id)
    {
        uint value = unchecked((uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)id * 83492791);
        value ^= value >> 13; value *= 1274126177; value ^= value >> 16;
        return (value & 65535) / 65535f;
    }

    /// <summary>Identity alone controls the decorative colony silhouette; movement cannot make it change shape.</summary>
    public static float ColonySeed(ViewCell cell) => CellSeed(0, 0, cell.OrganismId ?? 0);

    private static void WriteInstance(float[] buffer, int index, float x, float height, float z, float side,
        Color color, float data0, float data1)
    {
        int p = index * Stride;
        buffer[p] = side; buffer[p + 1] = 0; buffer[p + 2] = 0; buffer[p + 3] = x;
        buffer[p + 4] = 0; buffer[p + 5] = 1; buffer[p + 6] = 0; buffer[p + 7] = height;
        buffer[p + 8] = 0; buffer[p + 9] = 0; buffer[p + 10] = side; buffer[p + 11] = z;
        buffer[p + 12] = color.R; buffer[p + 13] = color.G; buffer[p + 14] = color.B; buffer[p + 15] = color.A;
        buffer[p + 16] = data0; buffer[p + 17] = data1; buffer[p + 18] = 0; buffer[p + 19] = 0;
    }

    public void SetAngled(bool value)
    {
        if (angled == value) return;
        float previous = ProjectedCellSize;
        bool wasFitted = zoom == 1;
        angled = value;
        zoom = 1; RefreshProjection();
        if (!wasFitted) zoom = Math.Clamp(previous / Math.Max(.001f, ProjectedCellSize), 1, 80);
        UpdateCamera(); ViewChanged?.Invoke();
    }
    public void Fit() { zoom = 1; center = new Vector2((frame?.Width ?? 100) / 2f, (frame?.Height ?? 100) / 2f); UpdateCamera(); ViewChanged?.Invoke(); }
    public void SetDetailLevel(int value)
    {
        if (value < 1 || value > 4) throw new ArgumentOutOfRangeException(nameof(value));
        if (value == 1) { Fit(); return; }
        float target = value == 2 ? 24 : value == 3 ? 48 : 96;
        zoom = 1; RefreshProjection();
        zoom = Math.Clamp(target / Math.Max(.001f, ProjectedCellSize), 1, 80);
        UpdateCamera(); ViewChanged?.Invoke();
    }
    public void CenterOn(float x, float y)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y)) return;
        center = new Vector2(x, y); UpdateCamera(); ViewChanged?.Invoke();
    }
    public void FocusSelection()
    {
        var position = SelectedPosition();
        if (position == null) return;
        center = new Vector2(position.Value.X + .5f, position.Value.Y + .5f);
        SetDetailLevel(4);
    }
    public void ZoomBy(float factor) => ZoomAt(factor, Size / 2);
    public void ZoomAt(float factor, Vector2 localAnchor)
    {
        if (!float.IsFinite(factor) || factor <= 0) return;
        float next = Math.Clamp(zoom * factor, 1, 80);
        if (next == zoom) return;
        Vector2 before = GroundAt(localAnchor); zoom = next;
        RefreshProjection();
        center += before - GroundAt(localAnchor);
        UpdateCamera(); ViewChanged?.Invoke();
    }
    public void PanBy(Vector2 localDistance)
    {
        if (!float.IsFinite(localDistance.X) || !float.IsFinite(localDistance.Y)) return;
        // Translate the ground under the cursor, using the same plane intersection as picking.
        // Very large programmatic pans extrapolate a finite in-viewport ray step instead of crossing the horizon.
        float steps = Math.Max(1, Math.Max(MathF.Abs(localDistance.X) * 2 / projectionSize.X,
            MathF.Abs(localDistance.Y) * 2 / projectionSize.Y));
        center += (GroundAt(projectionSize / 2) - GroundAt(projectionSize / 2 + localDistance / steps)) * steps;
        UpdateCamera(); ViewChanged?.Invoke();
    }
    private Vector2 OverlayPoint(Vector2 local) => viewport == null || Size.X <= 0 || Size.Y <= 0 ? local :
        new Vector2(local.X * viewport.Size.X / Size.X, local.Y * viewport.Size.Y / Size.Y);
    public Vector2 CellCenter(int x, int y) => ProjectGround(new Vector2(x + .5f, y + .5f));
    public (int X, int Y)? CellAt(Vector2 localPosition)
    {
        if (frame == null || !new Rect2(Vector2.Zero, Size).HasPoint(localPosition)) return null;
        Vector2 world = GroundAt(localPosition);
        int x = (int)Math.Floor(world.X), y = (int)Math.Floor(world.Y);
        return BoardPresentation.ContainsCell(x, y, frame.Width, frame.Height) ? (x, y) : null;
    }
    private void UpdateCamera()
    {
        if (camera == null || viewport == null) return;
        int width = frame?.Width ?? 100, height = frame?.Height ?? 100;
        // At fit the whole world is centered. Close views may reach its actual edges without a crop rim.
        center = zoom == 1 ? new Vector2(width / 2f, height / 2f) :
            new Vector2(Math.Clamp(center.X, 0, width), Math.Clamp(center.Y, 0, height));
        RefreshProjection();
        lastViewportSize = viewport.Size;
        UploadFrame(); QueueRedraw();
    }
    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton button)
        {
            if (button.Pressed && button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            { ZoomAt(button.ButtonIndex == MouseButton.WheelUp ? 1.2f : 1 / 1.2f, button.Position); AcceptEvent(); }
            else if (button.ButtonIndex is MouseButton.Right or MouseButton.Middle)
            {
                dragging = button.Pressed;
                if (dragging) { dragGroundAnchor = GroundAt(button.Position); GrabFocus(); }
                MouseDefaultCursorShape = dragging ? CursorShape.Move : CursorShape.Drag; AcceptEvent();
            }
            else if (button.Pressed && button.ButtonIndex == MouseButton.Left)
            { GrabFocus(); if (CellAt(button.Position) is { } cell) CellPicked?.Invoke(cell.X, cell.Y); AcceptEvent(); }
        }
        else if (input is InputEventMouseMotion motion && dragging)
        {
            center += dragGroundAnchor - GroundAt(motion.Position);
            UpdateCamera(); ViewChanged?.Invoke(); AcceptEvent();
        }
    }
    public override void _Process(double delta)
    {
        if (viewport != null && lastViewportSize != viewport.Size) UpdateCamera();
        ExpireMarkers();
        if (dragging && !Input.IsMouseButtonPressed(MouseButton.Right) && !Input.IsMouseButtonPressed(MouseButton.Middle))
        { dragging = false; MouseDefaultCursorShape = CursorShape.Drag; }
    }
    public void ExpireMarkers()
    {
        // A paused completed observation remains inspectable. Acknowledgment frames do not restart its clock.
        bool expired = frame == null || (frame.IsRunning && Stopwatch.GetTimestamp() >= markerDeadline);
        if (expired == markersExpired) return;
        markersExpired = expired; overlay?.QueueRedraw();
    }
    public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), new Color("11191d"));
    public override void _ExitTree()
    {
        if (foodInstances != null) foodInstances.Multimesh = null;
        if (organismInstances != null) organismInstances.Multimesh = null;
        foodMesh?.Mesh?.Dispose(); organismMesh?.Mesh?.Dispose();
        foodMesh?.Dispose(); organismMesh?.Dispose();
        groundMaterial?.Shader?.Dispose(); colonyMaterial?.Shader?.Dispose();
        groundMaterial?.Dispose(); colonyMaterial?.Dispose(); camera?.Environment?.Dispose();
    }
}
