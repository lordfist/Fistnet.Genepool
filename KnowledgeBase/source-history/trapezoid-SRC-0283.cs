using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Presentation;
using Godot;

namespace Fistnet.Genepool.GodotViewer;

/// <summary>A detached overview and navigation control; it never owns a simulation.</summary>
public partial class MinimapView : Godot.Control
{
    private ViewFrame? frame;
    private ImageTexture? texture;
    private Rect2 area;
    private Vector2[] footprint = Array.Empty<Vector2>();
    private (int X, int Y)? selected;
    private BoardLayer layer;
    private readonly StyleBoxFlat panel = new() { BgColor = new Color("101923ed"), BorderColor = new Color("51616c"),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 };
    public event Action<float, float>? Navigate;
    public Rect2 ViewArea { get => area; set { if (area == value) return; area = value; QueueRedraw(); } }
    public IReadOnlyList<Vector2> ViewFootprint
    {
        get => footprint;
        set { footprint = value.ToArray(); QueueRedraw(); }
    }
    public (int X, int Y)? SelectedCell { get => selected; set { if (selected == value) return; selected = value; QueueRedraw(); } }
    public BoardLayer Layer { get => layer; set { if (layer == value) return; layer = value; Upload(); } }
    private Rect2 Map => new(6, 6, Size.X - 12, Size.Y - 29);

    public override void _Ready()
    {
        TooltipText = "The whole world. The pale outline shows your visible ground area. Click to move the camera.";
        MouseDefaultCursorShape = CursorShape.PointingHand;
        Resized += QueueRedraw;
    }

    public void SetFrame(ViewFrame value)
    {
        if (ReferenceEquals(frame, value)) return;
        frame = value; Upload();
    }

    private void Upload()
    {
        if (frame == null) return;
        var pixels = new byte[frame.Width * frame.Height * 4];
        foreach (ViewCell cell in frame.Cells)
        {
            if (!BoardPresentation.ContainsCell(cell.X, cell.Y, frame.Width, frame.Height)) continue;
            Color color = cell.OrganismId.HasValue && layer != BoardLayer.Food ? BoardView3D.ColonyColor(cell) :
                layer == BoardLayer.Organisms ? Main.Background : BoardView3D.FoodColor(cell.Food);
            int offset = (cell.Y * frame.Width + cell.X) * 4;
            pixels[offset] = (byte)color.R8; pixels[offset + 1] = (byte)color.G8;
            pixels[offset + 2] = (byte)color.B8; pixels[offset + 3] = 255;
        }
        using var image = Image.CreateFromData(frame.Width, frame.Height, false, Image.Format.Rgba8, pixels);
        if (texture == null || texture.GetWidth() != frame.Width || texture.GetHeight() != frame.Height)
        { texture?.Dispose(); texture = ImageTexture.CreateFromImage(image); }
        else texture.Update(image);
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent input)
    {
        if (frame != null && input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click && Map.HasPoint(click.Position))
        {
            Vector2 uv = (click.Position - Map.Position) / Map.Size;
            Navigate?.Invoke(uv.X * frame.Width, uv.Y * frame.Height); AcceptEvent();
        }
    }

    public override void _Draw()
    {
        DrawStyleBox(panel, new Rect2(Vector2.Zero, Size));
        if (texture != null && frame != null)
        {
            DrawTextureRect(texture, Map, false);
            Vector2 scale = Map.Size / new Vector2(frame.Width, frame.Height);
            var outline = ClipFootprint(frame.Width, frame.Height);
            if (outline.Count > 1)
            {
                var points = new Vector2[outline.Count + 1];
                for (int i = 0; i < outline.Count; i++) points[i] = Map.Position + outline[i] * scale;
                points[^1] = points[0];
                DrawPolyline(points, new Color("ffeda5"), 1.5f, true);
            }
            if (selected is { } cell)
                DrawCircle(Map.Position + new Vector2(cell.X + .5f, cell.Y + .5f) * scale, 2, Colors.White);
        }
        DrawString(ThemeDB.FallbackFont, new Vector2(9, Size.Y - 7), "WORLD · click to pan", HorizontalAlignment.Left, -1, 10, Main.Muted);
    }

    private List<Vector2> ClipFootprint(float width, float height)
    {
        var points = footprint.Length > 0 ? new List<Vector2>(footprint) :
            new List<Vector2> { area.Position, new(area.End.X, area.Position.Y), area.End, new(area.Position.X, area.End.Y) };
        // Intersect the camera's ground quadrilateral with the world, preserving angled edges.
        for (int side = 0; side < 4 && points.Count > 0; side++)
        {
            bool horizontal = side < 2;
            float boundary = side == 1 ? width : side == 3 ? height : 0;
            bool lower = side % 2 == 0;
            float Component(Vector2 p) => horizontal ? p.X : p.Y;
            bool Inside(Vector2 p) => lower ? Component(p) >= boundary : Component(p) <= boundary;
            var clipped = new List<Vector2>(points.Count + 2);
            Vector2 previous = points[^1]; bool previousInside = Inside(previous);
            foreach (Vector2 current in points)
            {
                bool currentInside = Inside(current);
                if (currentInside != previousInside)
                {
                    float t = (boundary - Component(previous)) / (Component(current) - Component(previous));
                    clipped.Add(previous.Lerp(current, t));
                }
                if (currentInside) clipped.Add(current);
                previous = current; previousInside = currentInside;
            }
            points = clipped;
        }
        return points;
    }

    public override void _ExitTree() { texture?.Dispose(); panel.Dispose(); }
}
