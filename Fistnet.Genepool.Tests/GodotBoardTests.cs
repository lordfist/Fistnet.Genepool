using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Presentation;

namespace Fistnet.Genepool.Tests;

internal static class GodotBoardTests
{
    public static IEnumerable<TestCase> Cases()
    {
        yield return new("Godot board: food palette preserves the existing clamped scale", "godot-board", FoodPalette);
        yield return new("Godot board: black organisms remain visible and pattern dimming is selective", "godot-board", OrganismPalette);
        yield return new("Godot board: viewport projection handles rectangular boards and exclusive borders", "godot-board", PickingBorders);
        yield return new("Godot board: zoomed and panned picking rejects clipped cells", "godot-board", PickingAfterNavigation);
        yield return new("Godot board: camera pan stays bounded and Fit removes it", "godot-board", PanBounds);
        yield return new("Godot board: native instance layout keeps independent positions and colors", "godot-board", InstanceLayout);
        yield return new("Godot board: trails contain only successful observed moves", "godot-board", SuccessfulTrails);
        yield return new("Godot board: trails retain the newest sixteen actual moves", "godot-board", TrailBound);
    }

    private static void FoodPalette()
    {
        Check.Equal(unchecked((int)0xff111c25), BoardPresentation.FoodArgb(0));
        Check.Equal(unchecked((int)0xff2e4c42), BoardPresentation.FoodArgb(3));
        Check.Equal(unchecked((int)0xff426c56), BoardPresentation.FoodArgb(5));
        Check.Equal(unchecked((int)0xff74bc88), BoardPresentation.FoodArgb(10));
        Check.Equal(BoardPresentation.FoodArgb(0), BoardPresentation.FoodArgb(-20));
        Check.Equal(BoardPresentation.FoodArgb(10), BoardPresentation.FoodArgb(20));
        for (int food = 0; food <= 10; food++)
            Check.Equal(Fistnet.Genepool.Visualization.BoardView.FoodColor(food).ToArgb(),
                BoardPresentation.FoodArgb(food), "Godot food scale diverged from the established viewer");
    }

    private static void OrganismPalette()
    {
        Check.Equal(unchecked((int)0xff5f7085), BoardPresentation.OrganismArgb(0, "A", null));
        Check.Equal(unchecked((int)0xff5f7085), BoardPresentation.OrganismArgb(unchecked((int)0xff000000), "A", "A"));
        Check.Equal(unchecked((int)0xff272c35), BoardPresentation.OrganismArgb(0, "B", "A"));
        Check.Equal(unchecked((int)0xffc86420), BoardPresentation.OrganismArgb(0x22c86420, "A", "A"));
        Check.Equal(unchecked((int)0xff42291c), BoardPresentation.OrganismArgb(0x22c86420, "B", "A"));
        Check.Equal(unchecked((int)0xff42291c), BoardPresentation.OrganismArgb(0x22c86420, null, "A"));
    }

    private static void PickingBorders()
    {
        var projection = new BoardProjection(1028, 828, 100, 100);
        Check.Equal(8f, projection.PixelsPerCell);
        Check.Equal(((int X, int Y)?)(0, 0), projection.CellAt(114, 14));
        Check.Equal(((int X, int Y)?)(99, 99), projection.CellAt(913.9f, 813.9f));
        Check.True(projection.CellAt(113.9f, 14) == null, "Left board margin was selectable");
        Check.True(projection.CellAt(914, 14) == null, "Right border must be exclusive");
        Check.True(projection.CellAt(114, 814) == null, "Bottom border must be exclusive");
        Check.True(projection.CellAt(float.NaN, 20) == null, "NaN coordinate was selectable");
        Check.True(projection.CellAt(120, float.PositiveInfinity) == null, "Infinite coordinate was selectable");
        var rectangle = new BoardProjection(360, 260, 40, 20);
        var center = rectangle.CellCenter(10, 5);
        Near(101.15f, center.X); Near(92.65f, center.Y);
        Check.Equal(((int X, int Y)?)(10, 5), rectangle.CellAt(center.X, center.Y));
    }

    private static void PickingAfterNavigation()
    {
        var projection = new BoardProjection(1028, 828, 100, 100, 2, 80, -60);
        // Cell 50,50 is now centered at (602,362), after a 2x zoom and a bounded pan.
        var center = projection.CellCenter(50, 50);
        Check.Equal((602f, 362f), center);
        Check.Equal(((int X, int Y)?)(50, 50), projection.CellAt(602, 362));
        Check.Equal(((int X, int Y)?)(51, 50), projection.CellAt(618, 362));
        var clipped = projection.CellCenter(0, 0);
        Check.True(clipped.X < 0 && projection.CellAt(clipped.X, clipped.Y) == null,
            "A cell outside the clipped viewport remained selectable");
        Check.True(projection.CellAt(-1, 400) == null, "Negative viewport coordinate was selectable");
        Check.True(projection.CellAt(1028, 400) == null, "Viewport right edge was selectable");
    }

    private static void PanBounds()
    {
        var fitted = new BoardProjection(1028, 828, 100, 100);
        Check.Equal((0f, 0f), fitted.ClampPan(150, -150));
        var zoomed = fitted with { Zoom = 2 };
        Check.Equal((300f, -400f), zoomed.ClampPan(10000, -10000));
        Check.Equal((50f, 90f), zoomed.ClampPan(50, 90));
    }

    private static void InstanceLayout()
    {
        var buffer = Enumerable.Repeat(-1f, 24).ToArray();
        BoardPresentation.WriteInstance(buffer, 1, 17.5f, 9.5f, 1f / 3, unchecked((int)0xff804020));
        Check.True(buffer.Take(12).All(value => value == -1), "Writing one instance changed its neighbor");
        Near(1f / 3, buffer[12]); Near(1f / 3, buffer[17]);
        Check.Equal(17.5f, buffer[15]); Check.Equal(9.5f, buffer[19]);
        foreach (int index in new[] { 13, 14, 16, 18 }) Check.Equal(0f, buffer[index]);
        Near(128f / 255, buffer[20]); Near(64f / 255, buffer[21]); Near(32f / 255, buffer[22]);
        Check.Equal(1f, buffer[23]);
    }

    private static ViewAction Action(int season, bool moved, int sourceX = 2, int sourceY = 3,
        int destinationX = 3, int destinationY = 3) => new(season, 0, "Move", "East", moved ? "Moved" : "Blocked",
            0, 0, 0, sourceX, sourceY, destinationX, destinationY, "fixture", "fixture", Moved: moved);

    private static ViewOrganism Selected(params ViewAction[] actions) => new(7, 0, 0, 0, 2, 3, true,
        10, 5, 3, 0, 0, 0, 0, "fixture", Array.Empty<ViewGene>(), actions);

    private static void SuccessfulTrails()
    {
        var selected = Selected(Action(1, false), Action(2, true), Action(3, true, destinationX: 2),
            Action(4, true, sourceX: -1), Action(5, true, destinationY: 100));
        var trail = BoardPresentation.MovementTrail(selected, 100, 100).ToArray();
        Check.Equal(1, trail.Length);
        Check.Equal(2, trail[0].Season);
        Check.Equal(5, selected.Trace.Count, "Observation changed the source trace");
    }

    private static void TrailBound()
    {
        var actions = Enumerable.Range(1, 30).Select(season => Action(season, season % 3 != 0)).ToArray();
        var trail = BoardPresentation.MovementTrail(Selected(actions), 100, 100).ToArray();
        Check.Equal(16, trail.Length);
        Check.Equal(7, trail[0].Season);
        Check.Equal(29, trail[^1].Season);
        Check.True(trail.All(action => action.Moved), "Failed movement was drawn as a trail");
    }

    private static void Near(float expected, float actual) =>
        Check.True(Math.Abs(expected - actual) < .0001f, $"expected={expected}, actual={actual}");
}
