using Godot;

namespace Fistnet.Genepool.GodotViewer;

public partial class Main
{
    private Label ViewHint = null!;
    private readonly List<Button> detailButtons = new();
    private readonly List<ColorRect> foodSwatches = new();
    public bool IsDepthView => CameraBox.Selected != 2;

    public void SetViewMode(int mode)
    {
        if (mode < 0 || mode > 2) throw new ArgumentOutOfRangeException(nameof(mode));
        CameraBox.Select(mode);
        BoardView.Visible = mode == 2;
        DepthView.Visible = mode != 2;
        Minimap.Visible = mode != 2;
        BoardView.ShowMarkers = DepthView.ShowMarkers = MarkersToggle.ButtonPressed;
        BoardView.ShowTrail = DepthView.ShowTrail = TrailToggle.ButtonPressed;
        for (int i = 0; i < foodSwatches.Count; i++)
        {
            float t = i / 10f;
            foodSwatches[i].Color = mode != 2 ? BoardView3D.FoodColor(i) :
                Color.Color8((byte)(17 + 99 * t), (byte)(28 + 160 * t), (byte)(37 + 99 * t));
        }
        // A hidden fallback must not consume a second GPU frame every refresh.
        if (BoardView.IsNodeReady())
            BoardView.BoardViewport.RenderTargetUpdateMode = mode == 2 ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        if (DepthView.IsNodeReady())
            DepthView.BoardViewport.RenderTargetUpdateMode = mode != 2 ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
        if (mode != 2) DepthView.Angled = mode == 0;
        if (DisplayedFrame is { } frame)
        {
            if (mode == 2) BoardView.SetFrame(frame); else DepthView.SetFrame(frame);
        }
        BoardView.SelectedCell = selectedCell; BoardView.SelectedOrganismId = selectedId; BoardView.HighlightedPattern = patternKey;
        DepthView.SelectedCell = selectedCell; DepthView.SelectedOrganismId = selectedId; DepthView.HighlightedPattern = patternKey;
        UpdateViewStatus();
    }

    private void FitView() { if (IsDepthView) DepthView.Fit(); else BoardView.Fit(); }
    private void ZoomView(float factor) { if (IsDepthView) DepthView.ZoomBy(factor); else BoardView.ZoomBy(factor); }
    private void FocusView()
    {
        if (IsDepthView) { DepthView.FocusSelection(); return; }
        (int X, int Y)? position = selectedCell;
        if (DisplayedFrame?.Selected is { } selected && selected.Id == selectedId) position = (selected.X, selected.Y);
        if (position is not { } cell) return;
        BoardView.Fit(); BoardView.ZoomAt(12, BoardView.CellCenter(cell.X, cell.Y));
        BoardView.PanBy(BoardView.Size / 2 - BoardView.CellCenter(cell.X, cell.Y));
    }

    private void UpdateViewStatus()
    {
        if (DepthView == null || ViewHint == null || Minimap == null) return;
        ZoomLabel.Text = $"{(IsDepthView ? DepthView.Zoom : BoardView.Zoom) * 100:0}%";
        for (int i = 0; i < detailButtons.Count; i++) detailButtons[i].SetPressedNoSignal(IsDepthView && DepthView.DetailLevel == i + 1);
        if (!IsDepthView)
        {
            ViewHint.Text = "Original 2D · wheel to zoom · right / middle drag to pan";
            return;
        }
        string level = DepthView.DetailLevel switch { 1 => "World", 2 => "Habitat", 3 => "Organism", _ => "Inspect" };
        string actions = DepthView.DetailLevel switch
        {
            3 when MarkersToggle.ButtonPressed => "muted outcomes · latest displayed season",
            4 when !selectedId.HasValue => "select an organism to inspect actions",
            4 when MarkersToggle.ButtonPressed => "focused organism's outcomes",
            _ => "quiet view"
        };
        ViewHint.Text = $"{level} · {actions} · drag to explore";
        Minimap.ViewArea = DepthView.VisibleArea;
        Minimap.ViewFootprint = DepthView.VisiblePolygon;
        Minimap.SelectedCell = DepthView.SelectedCell;
        if (DisplayedFrame?.Selected is { } selected && selected.Id == selectedId)
            Minimap.SelectedCell = (selected.X, selected.Y);
    }
}
