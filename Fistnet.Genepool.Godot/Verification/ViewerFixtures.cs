using Fistnet.Genepool.Control;

namespace Fistnet.Genepool.GodotViewer.Verification;

// Synthetic detached frames. Fixture creation never initializes or advances a world.
internal static class ViewerFixtures
{
    private static readonly int[] Colors = { unchecked((int)0xFF63C7CA), unchecked((int)0xFFDFBA66),
        unchecked((int)0xFFD97B9A), unchecked((int)0xFF8FBEE8), unchecked((int)0xFFB998DD), unchecked((int)0xFF99CA75) };

    internal static ViewFrame Create(int population, int season = 64, long run = 1)
    {
        if (population < 0 || population > 10_000) throw new ArgumentOutOfRangeException(nameof(population));
        var cells = new ViewCell[10_000];
        var counts = new int[6];
        long reserves = 0, food = 0;
        ViewCell? followed = null;
        for (int y = 0; y < 100; y++)
        for (int x = 0; x < 100; x++)
        {
            int index = y * 100 + x;
            bool occupied = index * 7919 % 10_000 < population;
            int group = (x + y * 3) % 6;
            int localFood = (x / 9 + y / 7 + 3) % 11;
            cells[index] = new ViewCell(x, y, localFood, occupied ? index + 1 : null,
                occupied ? Colors[group] : 0, occupied ? $"pattern-{group}" : null!);
            food += localFood;
            if (occupied)
            {
                counts[group]++; reserves += 5;
                if (followed is null || Math.Abs(x - 50) + Math.Abs(y - 50) < Math.Abs(followed.Value.X - 50) + Math.Abs(followed.Value.Y - 50))
                    followed = cells[index];
            }
        }
        var patterns = counts.Select((count, i) => new ViewPattern($"pattern-{i}",
            i switch { 0 => "Gather → Move → Eat → Reproduce → Move → Gather → Heal → Move",
                1 => "Move → Gather → Move → Eat → Reproduce → Gather → Move → Heal",
                2 => "Gather → Gather → Eat → Move → Reproduce → Move → Heal → Move",
                3 => "Move → Attack → Gather → Eat → Move → Reproduce → Heal → Move",
                4 => "Heal → Gather → Move → Eat → Reproduce → Gather → Move → Move",
                _ => "Gather → Eat → Reproduce → Move → Gather → Move → Heal → Eat" }, count))
            .Where(p => p.Count > 0).OrderByDescending(p => p.Count).ToArray();
        var history = Enumerable.Range(0, 65).Select(i => new ViewHistory(i, Math.Max(0, population - 150 + i * 2),
            food - 1000 + i * 15, Math.Max(0, reserves - 350 + i * 5), i * 4L, i * 2L, i * Math.Max(population, 1L))).ToArray();
        var genes = new[] { "Gather", "Move", "Eat", "Reproduce", "Move", "Gather", "Heal", "Move" }
            .Select((name, i) => new ViewGene(i, name, i % 2 == 0 ? "Self" : "MiddleRight", i)).ToArray();
        ViewOrganism? selected = null;
        if (followed is ViewCell f)
        {
            var trace = new[] {
                new ViewAction(61, 1, "Move", "MiddleRight", "Moved to the neighboring cell", 0, -1, 0.25f,
                    f.X-2, f.Y, f.X-1, f.Y, "legacy", "Best learned choice", Moved:true, TargetX:f.X-1, TargetY:f.Y),
                new ViewAction(62, 5, "Gather", "Self", "Gathered available local food", 3, -1, 0.50f,
                    f.X-1, f.Y, f.X-1, f.Y, "legacy", "Best learned choice", FoodGathered:3, TargetX:f.X-1, TargetY:f.Y),
                new ViewAction(63, 3, "Reproduce", "Self", "Placed a child in a free neighboring cell", -2, -1, 0.40f,
                    f.X-1, f.Y, f.X-1, f.Y, "legacy", "Best learned choice", FoodSpent:2, BirthPlaced:true, TargetX:f.X-1, TargetY:f.Y),
                new ViewAction(64, 7, "Move", "MiddleRight", "Moved to the neighboring cell", 0, -1, 0.25f,
                    f.X-1, f.Y, f.X, f.Y, "legacy", "Best learned choice", Moved:true, TargetX:f.X, TargetY:f.Y)
            };
            selected = new ViewOrganism(f.OrganismId!.Value, 17, 24, 3, f.X, f.Y, true, 83, 5, f.Food,
                8, 64, 41, 2, f.PatternKey, Array.AsReadOnly(genes), Array.AsReadOnly(trace));
        }
        return new ViewFrame(run, 0, season, season / 8, 100, 100, "Paused", false, null!, 15, 12.4, 15,
            population, food, reserves, 256, 128, Array.AsReadOnly(cells), Array.AsReadOnly(patterns),
            Array.AsReadOnly(new[] { new ViewCount("Gather", population*2L), new ViewCount("Move", population*3L),
                new ViewCount("Eat", population), new ViewCount("Reproduce", population), new ViewCount("Heal", population) }),
            Array.AsReadOnly(new[] { new ViewCount("Gather", 2300), new ViewCount("Move", 4800),
                new ViewCount("Eat", 1900), new ViewCount("Reproduce", 256), new ViewCount("Heal", 630) }),
            Array.AsReadOnly(history), Array.AsReadOnly(selected is null ? Array.Empty<ViewMarker>() :
                new[] { new ViewMarker(selected.X, selected.Y, "action"), new ViewMarker(selected.X - 1, selected.Y + 1, "birth") }),
            selected!, new SimulationRunOptions { Mode = SimulationMode.DeterministicReference, Seed = 29 });
    }

    internal static ViewFrame ColorsFixture()
    {
        var frame = Create(0);
        var cells = frame.Cells.ToArray();
        cells[0] = new ViewCell(0, 0, 0, null, 0, null!);
        cells[4*100+3] = new ViewCell(3, 4, 3, 1, unchecked((int)0xFF000000), "black");
        cells[3*100+4] = new ViewCell(4, 3, 10, null, 0, null!);
        cells[4*100+5] = new ViewCell(5, 4, 10, 2, unchecked((int)0xFFFF6060), "red");
        return frame with { Cells = Array.AsReadOnly(cells), Population = 2, Selected = null! };
    }
}
