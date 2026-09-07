using System.Diagnostics;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Presentation;
using Godot;
using BoardLayer = Fistnet.Genepool.Control.Presentation.BoardLayer;

namespace Fistnet.Genepool.GodotViewer;

/// <summary>A passive GPU surface. All inputs are detached completed snapshots.</summary>
public partial class BoardView2D : global::Godot.Control
{
    private ViewFrame? frame;
    private SubViewport? viewport;
    private Camera2D? camera;
    private MultiMesh? foodMesh, organismMesh;
    private MultiMeshInstance2D? foodInstances, organismInstances;
    private Node2D? overlay;
    private float[] foodBuffer = Array.Empty<float>(), organismBuffer = Array.Empty<float>();
    private int capacity, allocatedWidth, allocatedHeight;
    private BoardLayer layer = BoardLayer.Combined;
    private string? highlightedPattern;
    private long? selectedOrganismId;
    private (int X, int Y)? selectedCell;
    private bool showTrail, showMarkers;
    private float zoom = 1;
    private Vector2 pan, dragStart, dragPan;
    private bool dragging, markersExpired = true;
    private long markerDeadline;
    private Vector2I lastViewportSize;

    public event Action<int, int>? CellPicked;
    public event Action? ViewChanged;
    public ViewFrame? Frame => frame;
    public SubViewport BoardViewport => viewport ?? throw new InvalidOperationException("Board is not ready.");
    public float Zoom => zoom;
    public int FoodInstanceCount { get; private set; }
    public int OrganismInstanceCount { get; private set; }
    public double LastUploadMilliseconds { get; private set; }
    public bool MarkersVisible => showMarkers && !markersExpired && frame != null;

    public BoardLayer Layer
    {
        get => layer;
        set
        {
            if (layer == value) return;
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            layer = value; UploadFrame();
        }
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
        set { if (selectedCell == value) return; selectedCell = value; overlay?.QueueRedraw(); }
    }
    public bool ShowTrail
    {
        get => showTrail;
        set { if (showTrail == value) return; showTrail = value; overlay?.QueueRedraw(); }
    }
    public bool ShowMarkers
    {
        get => showMarkers;
        set { if (showMarkers == value) return; showMarkers = value; ExpireMarkers(); overlay?.QueueRedraw(); }
    }

    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.Click;
        MouseDefaultCursorShape = CursorShape.Cross;
        CustomMinimumSize = new Vector2(240, 240);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        TooltipText = "Click a cell to inspect. Wheel to zoom; right or middle drag to pan.";

        var container = new SubViewportContainer
        {
            Name = "BoardViewportContainer", Stretch = true, StretchShrink = 1,
            MouseFilter = MouseFilterEnum.Ignore, MouseTarget = false,
            TextureFilter = TextureFilterEnum.Nearest
        };
        AddChild(container);
        container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        viewport = new SubViewport
        {
            Name = "BoardViewport", Disable3D = true, TransparentBg = true,
            Size = new Vector2I(Math.Max(2, (int)Size.X), Math.Max(2, (int)Size.Y)),
            RenderTargetUpdateMode = SubViewport.UpdateMode.WhenVisible
        };
        container.AddChild(viewport);
        var world = new Node2D { Name = "BoardWorld", TextureFilter = TextureFilterEnum.Nearest };
        viewport.AddChild(world);
        foodMesh = CreateMesh(); organismMesh = CreateMesh();
        foodInstances = new MultiMeshInstance2D { Name = "FoodBatch", Multimesh = foodMesh };
        organismInstances = new MultiMeshInstance2D { Name = "OrganismBatch", Multimesh = organismMesh };
        world.AddChild(foodInstances); world.AddChild(organismInstances);
        overlay = new Node2D { Name = "SelectionAndEvents" };
        world.AddChild(overlay);
        overlay.Draw += DrawOverlay;
        camera = new Camera2D
        {
            Name = "BoardCamera", AnchorMode = Camera2D.AnchorModeEnum.DragCenter,
            Enabled = true, PositionSmoothingEnabled = false
        };
        world.AddChild(camera);
        Resized += OnResized;
        UploadFrame(); UpdateCamera(); QueueRedraw();
    }

    private static MultiMesh CreateMesh() => new()
    {
        TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
        UseColors = true,
        Mesh = new QuadMesh { Size = Vector2.One }
    };

    public void SetFrame(ViewFrame? value)
    {
        if (ReferenceEquals(frame, value)) return;
        bool newWorld = frame == null || value == null || frame.RunId != value.RunId;
        bool newSeason = newWorld || frame!.Season != value!.Season;
        bool dimensionsChanged = frame?.Width != value?.Width || frame?.Height != value?.Height;
        frame = value;
        if (newSeason)
        {
            markerDeadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 2;
            markersExpired = value == null;
        }
        if (newWorld) { selectedCell = null; selectedOrganismId = null; }
        // A new acknowledgment frame at the same season must not revive expired events.
        ExpireMarkers(); UploadFrame();
        if (dimensionsChanged) UpdateCamera();
        overlay?.QueueRedraw();
    }

    private void UploadFrame()
    {
        if (foodMesh == null || organismMesh == null) return;
        long started = Stopwatch.GetTimestamp();
        if (frame == null)
        {
            FoodInstanceCount = OrganismInstanceCount = 0;
            foodMesh.VisibleInstanceCount = organismMesh.VisibleInstanceCount = 0;
            LastUploadMilliseconds = 0; overlay?.QueueRedraw(); return;
        }
        if (frame.Width <= 0 || frame.Height <= 0 || (long)frame.Width * frame.Height > 1_000_000)
            throw new ArgumentException("Invalid board dimensions.", nameof(frame));
        int count = checked(frame.Width * frame.Height);
        if (capacity != count || allocatedWidth != frame.Width || allocatedHeight != frame.Height)
        {
            capacity = count; allocatedWidth = frame.Width; allocatedHeight = frame.Height;
            foodBuffer = new float[count * BoardPresentation.InstanceStride];
            organismBuffer = new float[count * BoardPresentation.InstanceStride];
            foodMesh.InstanceCount = organismMesh.InstanceCount = count;
            var bounds = new Aabb(new Vector3(0, 0, -.5f), new Vector3(frame.Width, frame.Height, 1));
            foodMesh.CustomAabb = organismMesh.CustomAabb = bounds;
        }
        int foodCount = 0, organismCount = 0;
        float organismSide = layer == BoardLayer.Combined ? 1f / 3 : 1;
        foreach (ViewCell cell in frame.Cells)
        {
            if (!BoardPresentation.ContainsCell(cell.X, cell.Y, frame.Width, frame.Height)) continue;
            if (foodCount == capacity) break;
            int background = layer == BoardLayer.Organisms ? BoardPresentation.OrganismsBackgroundArgb :
                BoardPresentation.FoodArgb(cell.Food);
            BoardPresentation.WriteInstance(foodBuffer, foodCount++, cell.X + .5f, cell.Y + .5f, 1, background);
            if (!cell.OrganismId.HasValue || layer == BoardLayer.Food) continue;
            int color = BoardPresentation.OrganismArgb(cell.ColorArgb, cell.PatternKey, highlightedPattern);
            BoardPresentation.WriteInstance(organismBuffer, organismCount++, cell.X + .5f, cell.Y + .5f,
                organismSide, color);
        }
        // Native calls upload complete reused buffers. Only the packed active prefix is drawn.
        RenderingServer.MultimeshSetBuffer(foodMesh.GetRid(), foodBuffer);
        if (organismCount > 0) RenderingServer.MultimeshSetBuffer(organismMesh.GetRid(), organismBuffer);
        foodMesh.VisibleInstanceCount = FoodInstanceCount = foodCount;
        organismMesh.VisibleInstanceCount = OrganismInstanceCount = organismCount;
        LastUploadMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        overlay?.QueueRedraw();
    }

    private BoardProjection Projection => new(
        viewport?.Size.X ?? Math.Max(2, Size.X), viewport?.Size.Y ?? Math.Max(2, Size.Y),
        frame?.Width ?? 100, frame?.Height ?? 100, zoom, pan.X, pan.Y);

    private Vector2 ToViewport(Vector2 local) => viewport == null || Size.X <= 0 || Size.Y <= 0 ? local :
        new Vector2(local.X * viewport.Size.X / Size.X, local.Y * viewport.Size.Y / Size.Y);

    private Vector2 FromViewport(Vector2 pixels) => viewport == null ? pixels :
        new Vector2(pixels.X * Size.X / viewport.Size.X, pixels.Y * Size.Y / viewport.Size.Y);

    public (int X, int Y)? CellAt(Vector2 localPosition)
    {
        if (frame == null || localPosition.X < 0 || localPosition.Y < 0 ||
            localPosition.X >= Size.X || localPosition.Y >= Size.Y) return null;
        Vector2 point = ToViewport(localPosition);
        return Projection.CellAt(point.X, point.Y);
    }

    public Vector2 CellCenter(int x, int y)
    {
        var center = Projection.CellCenter(x, y);
        return FromViewport(new Vector2(center.X, center.Y));
    }

    public void Fit()
    {
        zoom = 1; pan = Vector2.Zero; UpdateCamera(); ViewChanged?.Invoke();
    }

    public void ZoomBy(float factor) => ZoomAt(factor, Size / 2);

    public void ZoomAt(float factor, Vector2 localAnchor)
    {
        if (!float.IsFinite(factor) || factor <= 0) return;
        float next = Math.Clamp(zoom * factor, 1, 12);
        if (next == zoom) return;
        BoardProjection before = Projection;
        Vector2 anchor = ToViewport(localAnchor);
        Vector2 world = new((anchor.X - before.Left) / before.PixelsPerCell,
            (anchor.Y - before.Top) / before.PixelsPerCell);
        zoom = next;
        BoardProjection after = Projection;
        pan += anchor - new Vector2(after.Left + world.X * after.PixelsPerCell,
            after.Top + world.Y * after.PixelsPerCell);
        UpdateCamera(); ViewChanged?.Invoke();
    }

    public void PanBy(Vector2 localDistance)
    {
        pan += ToViewport(localDistance); UpdateCamera(); ViewChanged?.Invoke();
    }

    private void UpdateCamera()
    {
        if (camera == null || viewport == null) return;
        BoardProjection projection = Projection;
        var clamped = projection.ClampPan(pan.X, pan.Y);
        pan = new Vector2(clamped.X, clamped.Y);
        projection = Projection;
        float scale = projection.PixelsPerCell;
        camera.Zoom = new Vector2(scale, scale);
        camera.Position = new Vector2(projection.BoardWidth / 2f - pan.X / scale,
            projection.BoardHeight / 2f - pan.Y / scale);
        camera.ForceUpdateScroll();
        lastViewportSize = viewport.Size;
        overlay?.QueueRedraw(); QueueRedraw();
    }

    private void OnResized() { UpdateCamera(); QueueRedraw(); }

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton button)
        {
            if (button.Pressed && button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                ZoomAt(button.ButtonIndex == MouseButton.WheelUp ? 1.25f : .8f, button.Position);
                AcceptEvent();
            }
            else if (button.ButtonIndex is MouseButton.Right or MouseButton.Middle)
            {
                dragging = button.Pressed;
                if (dragging) { dragStart = ToViewport(button.Position); dragPan = pan; GrabFocus(); }
                AcceptEvent();
            }
            else if (button.Pressed && button.ButtonIndex == MouseButton.Left)
            {
                GrabFocus();
                if (CellAt(button.Position) is { } cell) CellPicked?.Invoke(cell.X, cell.Y);
                AcceptEvent();
            }
        }
        else if (input is InputEventMouseMotion motion && dragging)
        {
            pan = dragPan + ToViewport(motion.Position) - dragStart;
            UpdateCamera(); ViewChanged?.Invoke(); AcceptEvent();
        }
    }

    public override void _Process(double delta)
    {
        if (viewport != null && lastViewportSize != viewport.Size) UpdateCamera();
        ExpireMarkers();
        if (dragging && !Input.IsMouseButtonPressed(MouseButton.Right) && !Input.IsMouseButtonPressed(MouseButton.Middle))
            dragging = false;
    }

    public void ExpireMarkers()
    {
        if (markersExpired || Stopwatch.GetTimestamp() < markerDeadline) return;
        markersExpired = true;
        if (showMarkers) overlay?.QueueRedraw();
    }

    public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), new Color("0b111a"));

    private void DrawOverlay()
    {
        if (overlay == null || frame == null) return;
        float scale = Projection.PixelsPerCell;
        float hairline = 1 / scale;
        if (scale >= 15)
        {
            var grid = new Color(1, 1, 1, .12f);
            for (int x = 0; x <= frame.Width; x++) overlay.DrawLine(new Vector2(x, 0), new Vector2(x, frame.Height), grid, hairline);
            for (int y = 0; y <= frame.Height; y++) overlay.DrawLine(new Vector2(0, y), new Vector2(frame.Width, y), grid, hairline);
        }
        if (showTrail && frame.Selected != null && frame.Selected.Id == selectedOrganismId)
            foreach (ViewAction action in BoardPresentation.MovementTrail(frame.Selected, frame.Width, frame.Height))
                overlay.DrawLine(new Vector2(action.SourceX + .5f, action.SourceY + .5f),
                    new Vector2(action.DestinationX + .5f, action.DestinationY + .5f),
                    new Color("f9d96faa"), 2 * hairline, true);
        if (MarkersVisible)
        {
            int markerCount = 0;
            foreach (ViewMarker marker in frame.Markers)
            {
                if (markerCount++ >= 64) break;
                if (!BoardPresentation.ContainsCell(marker.X, marker.Y, frame.Width, frame.Height)) continue;
                Vector2 center = new(marker.X + .5f, marker.Y + .5f);
                float radius = Math.Max(3 * hairline, .7f);
                Color color = new(marker.Kind == "birth" ? "7defbb" : marker.Kind == "death" ? "ff7e88" : "ffd872");
                if (marker.Kind == "death")
                {
                    overlay.DrawLine(center - new Vector2(radius, radius), center + new Vector2(radius, radius), color, 1.5f * hairline, true);
                    overlay.DrawLine(center + new Vector2(radius, -radius), center + new Vector2(-radius, radius), color, 1.5f * hairline, true);
                }
                else overlay.DrawArc(center, radius, 0, Mathf.Tau, 24, color, 1.5f * hairline, true);
            }
        }
        (int X, int Y)? selected = selectedCell;
        if (frame.Selected != null && frame.Selected.Id == selectedOrganismId)
            selected = (frame.Selected.X, frame.Selected.Y);
        else if (selectedOrganismId.HasValue)
        {
            // While a selection command is pending, follow its identity in this completed frame.
            foreach (ViewCell cell in frame.Cells)
                if (cell.OrganismId == selectedOrganismId) { selected = (cell.X, cell.Y); break; }
        }
        if (selected is { } position && BoardPresentation.ContainsCell(position.X, position.Y, frame.Width, frame.Height))
        {
            Vector2 center = new(position.X + .5f, position.Y + .5f);
            float radius = Math.Max(5 * hairline, .65f);
            overlay.DrawRect(new Rect2(center - Vector2.One * radius, Vector2.One * (radius * 2)), new Color("ffeda5"), false, 2 * hairline);
        }
        overlay.DrawRect(new Rect2(0, 0, frame.Width, frame.Height), new Color("42586e"), false, hairline);
    }

    public override void _ExitTree()
    {
        // Node-owned instances release their references with the scene; wrappers release ours here.
        if (foodInstances != null) foodInstances.Multimesh = null;
        if (organismInstances != null) organismInstances.Multimesh = null;
        foodMesh?.Mesh?.Dispose(); organismMesh?.Mesh?.Dispose();
        foodMesh?.Dispose(); organismMesh?.Dispose();
    }
}
