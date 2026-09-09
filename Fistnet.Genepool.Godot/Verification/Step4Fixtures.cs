using Fistnet.Genepool.Control;

namespace Fistnet.Genepool.GodotViewer.Verification;

// Detached, illustrative completed outcomes. These never run or change the simulation.
internal static class Step4Fixtures
{
    internal static ViewFrame Create(int population, int season = 64, long run = 101, bool variedActions = false)
    {
        var frame = ViewerFixtures.Create(population, season, run);
        var actions = frame.Cells.Where(cell => cell.OrganismId.HasValue).Select(cell =>
        {
            int choice = variedActions ? (cell.X + cell.Y) % 4 : 0;
            var action = choice switch
            {
                1 => new ViewAction(season, 2, "Eat", "Self", "Consumed carried food", -1, 4, .2f,
                    cell.X, cell.Y, cell.X, cell.Y, "fixture", "Illustrative completed outcome", FoodSpent: 1, Healing: 4, TargetX: cell.X, TargetY: cell.Y),
                2 => new ViewAction(season, 6, "Heal", "Self", "Restored health", -1, 3, .2f,
                    cell.X, cell.Y, cell.X, cell.Y, "fixture", "Illustrative completed outcome", FoodSpent: 1, Healing: 3, TargetX: cell.X, TargetY: cell.Y),
                3 => new ViewAction(season, 1, "Move", "MiddleRight", "Blocked by an occupied neighboring cell", 0, -1, -.1f,
                    cell.X, cell.Y, cell.X, cell.Y, "fixture", "Illustrative completed outcome", TargetX: Math.Min(99, cell.X + 1), TargetY: cell.Y),
                _ => new ViewAction(season, 0, "Gather", "Self", "Gathered local food", 2, -1, .3f,
                    cell.X, cell.Y, cell.X, cell.Y, "fixture", "Illustrative completed outcome", FoodGathered: 2, TargetX: cell.X, TargetY: cell.Y)
            };
            return new ViewObservedAction(cell.OrganismId!.Value, action);
        }).ToArray();
        ViewOrganism? selected = frame.Selected;
        if (selected != null)
        {
            // The focused history and viewport feed name the same actual completed outcome.
            ViewAction current = actions.First(item => item.OrganismId == selected.Id).Action;
            selected = selected with { Trace = Array.AsReadOnly(selected.Trace.TakeLast(3).Append(current).ToArray()) };
        }
        return frame with { Selected = selected!, SeasonActions = Array.AsReadOnly(actions) };
    }

    internal static ViewFrame Appearance()
    {
        var frame = Create(0, run: 200);
        var cells = frame.Cells.Select(cell => cell with { Food = cell.X < 50 ? 0 : 10 }).ToArray();
        cells[50 * 100 + 50] = cells[50 * 100 + 50] with
            { OrganismId = 1, ColorArgb = unchecked((int)0xFF63C7CA), PatternKey = "cyan" };
        cells[50 * 100 + 52] = cells[50 * 100 + 52] with
            { OrganismId = 2, ColorArgb = unchecked((int)0xFF000000), PatternKey = "black" };
        return frame with { Cells = Array.AsReadOnly(cells), Population = 2 };
    }
}
