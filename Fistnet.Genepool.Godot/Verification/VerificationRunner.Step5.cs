using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Presentation;
using Godot;

namespace Fistnet.Genepool.GodotViewer.Verification;

public partial class VerificationRunner
{
    // The owner replaced the initial seven-hue/saturation proposal with the original full RGB values.
    // These prospective checks therefore permit black, white, grey and naturally pale DNA colours.
    // Pixel-distance bounds are local readability regressions, not accessibility certification.
    private static readonly (string Name, int Rgb)[] Step5RgbSamples =
    {
        ("red", 0xFF0000), ("green", 0x00FF00), ("blue", 0x0000FF), ("cyan", 0x00FFFF),
        ("magenta", 0xFF00FF), ("yellow", 0xFFFF00), ("orange", 0xFF8000), ("violet", 0x8000FF),
        ("black", 0x000000), ("near-black", 0x080808), ("white", 0xFFFFFF), ("mid-grey", 0x808080),
        ("grey-ground match", 0xC1C1C1), ("brown match", 0x735039), ("olive match", 0x91854A), ("green-ground match", 0x689044)
    };

    private void Step5RgbIdentityChecks()
    {
        var rgbValues = new HashSet<int>();
        for (int red = 0; red <= 255; red += 17)
        for (int green = 0; green <= 255; green += 17)
        for (int blue = 0; blue <= 255; blue += 17) rgbValues.Add(red << 16 | green << 8 | blue);
        for (int value = 0; value <= 255; value++)
            rgbValues.Add(value << 16 | ((value * 73 + 19) & 255) << 8 | ((value * 151 + 41) & 255));
        int mismatches = 0; var rendered = new HashSet<int>();
        foreach (int rgb in rgbValues)
        {
            var cell = new ViewCell(12, 17, 3, 49, unchecked((int)0xFF000000) | rgb, "same action pattern");
            Color color = BoardView3D.ColonyColor(cell);
            if (!ExactRgb(color, rgb)) mismatches++;
            rendered.Add(color.R8 << 16 | color.G8 << 8 | color.B8);
        }
        Check("DNA RGB reaches the renderer without a restricted palette", mismatches == 0 && rendered.Count == rgbValues.Count,
            $"distinct inputs={rgbValues.Count}; distinct outputs={rendered.Count}; mapping errors={mismatches}");

        var sample = new ViewCell(50, 50, 0, 12345, unchecked((int)0xFF137BCE), "first pattern");
        Color original = BoardView3D.ColonyColor(sample);
        Check("same DNA RGB is independent of pattern labels location identity and food",
            original == BoardView3D.ColonyColor(sample with { X = 4, Y = 91, OrganismId = 98765, Food = 10, PatternKey = "different pattern" }));
        Check("organism RGB stays opaque regardless of incoming ARGB alpha", new[] { 0, 1, 17, 128, 254, 255 }.All(alpha =>
            ExactRgb(BoardView3D.ColonyColor(sample with { ColorArgb = unchecked(alpha << 24 | 0x137BCE) }), 0x137BCE)));
        Check("decorative mould shape follows identity independently of DNA colour and movement",
            BoardView3D.ColonySeed(sample) == BoardView3D.ColonySeed(sample with
            { X = 77, Y = 2, Food = 10, ColorArgb = unchecked((int)0xFFFFFFFF), PatternKey = "mutated pattern" }));
    }

    private static bool ExactRgb(Color color, int rgb) => color.A == 1 &&
        Math.Abs(color.R - ((rgb >> 16) & 255) / 255f) < .000001f &&
        Math.Abs(color.G - ((rgb >> 8) & 255) / 255f) < .000001f &&
        Math.Abs(color.B - (rgb & 255) / 255f) < .000001f;

    private async Task Step5RgbPixelChecks()
    {
        Main main = viewer!; var board = main.DepthView;
        foreach (var backdrop in new[] { ("grey", 1, 0), ("brown", 0, 0), ("olive", 0, 5), ("green", 0, 10) })
        {
            var bare = ViewerFixtures.Create(0, run: 510 + backdrop.Item2 * 20 + backdrop.Item3);
            bare = bare with { Cells = Array.AsReadOnly(bare.Cells.Select(cell => cell with { Food = backdrop.Item3 }).ToArray()),
                LocalFood = backdrop.Item3 * bare.Width * bare.Height, History = Array.Empty<ViewHistory>() };
            var cells = bare.Cells.ToArray();
            for (int i = 0; i < Step5RgbSamples.Length; i++)
            {
                int x = 44 + i % 4 * 4, y = 46 + i / 4 * 3;
                cells[y * bare.Width + x] = cells[y * bare.Width + x] with
                { OrganismId = i + 1, ColorArgb = unchecked((int)0xFF000000) | Step5RgbSamples[i].Rgb, PatternKey = "RGB fixture" };
            }
            ViewFrame occupied = bare with { Cells = Array.AsReadOnly(cells), Population = Step5RgbSamples.Length };
            main.DisplayFrame(bare, true); main.SetViewMode(0); SelectLayerThroughUi(main, backdrop.Item2);
            board.SelectedCell = null; board.SelectedOrganismId = null; board.ShowMarkers = board.ShowTrail = false;
            // Owner correction: World now deliberately has no contrast outline. Terrain-matching RGB
            // may blend into its background there; mandatory World contrast would reinstate a rejected design.
            var views = new List<(int Level, float Pixels, string Name)>
            { (1, 0, "World fit"), (2, 0, "Habitat"), (3, 0, "Organism") };
            if (backdrop.Item1 == "grey")
            {
                views.Insert(1, (1, 11.8f, "World just below detail threshold"));
                views.Insert(2, (2, 12.2f, "Habitat just above detail threshold"));
            }
            foreach (var view in views)
            {
                int level = view.Level;
                board.SetDetailLevel(level); board.CenterOn(50, 50);
                if (view.Pixels > 0) board.ZoomBy(view.Pixels / board.ProjectedCellSize);
                main.DisplayFrame(bare, true); await Frames(4);
                using var ground = board.BoardViewport.GetTexture().GetImage();
                main.DisplayFrame(occupied, true); await Frames(4);
                using var organisms = board.BoardViewport.GetTexture().GetImage();
                var samples = new List<RgbPixelSample>();
                for (int i = 0; i < Step5RgbSamples.Length; i++)
                {
                    int x = 44 + i % 4 * 4, y = 46 + i / 4 * 3;
                    samples.Add(MeasureRgbPixels(board, new Vector2I(x, y), Step5RgbSamples[i], ground, organisms));
                }
                string detail = string.Join("; ", samples.OrderByDescending(sample => sample.OutlinePixels).ThenBy(sample => sample.ContrastFraction).Take(5).Select(sample =>
                    $"{sample.Name}: contrast={sample.ContrastPixels}/{sample.CellPixels}, outlinePixels={sample.OutlinePixels}, RGBerror={sample.CoreChannelError:0.000}" +
                    (sample.OutlineEvidence.Length > 0 ? "; " + sample.OutlineEvidence : "")));
                if (level == 1)
                    Check($"World renders RGB without dark or pale outlines on {backdrop.Item1}: {view.Name}",
                        board.DetailLevel == 1 && samples.All(sample => sample.OutlinePixels == 0), detail, false);
                else if (view.Pixels > 0)
                    Check("closer-detail dark and pale outlines remain just above the 12-pixel threshold",
                        board.DetailLevel == 2 && samples.First(sample => sample.Name == "white").OutlinePixels > 0 &&
                        samples.First(sample => sample.Name == "black").OutlinePixels > 0,
                        $"actual projected size={board.ProjectedCellSize:0.000}; " + detail, false);
                else
                {
                    Check($"full RGB bodies or edges remain visible on {backdrop.Item1} at detail {level}",
                        samples.All(sample => sample.ContrastFraction >= .025), detail, false);
                    Check($"full RGB central bodies preserve source channels on {backdrop.Item1} at detail {level}",
                        samples.All(sample => sample.CorePixels >= 4 && sample.CoreChannelError <= .12), detail, false);
                }
                measurements.Add(new { kind = "step5_rgb_visibility", backdrop = backdrop.Item1, detailLevel = level,
                    view = view.Name, projectedCellPixels = board.ProjectedCellSize, sampleCount = samples.Count,
                    minimumContrastPixels = samples.Min(sample => sample.ContrastPixels), minimumContrastFraction = samples.Min(sample => sample.ContrastFraction),
                    outlinePixels = samples.Sum(sample => sample.OutlinePixels),
                    maximumCoreChannelError = level > 1 ? samples.Max(sample => sample.CoreChannelError) : (double?)null,
                    oracle = "Native-camera projected cells; actual GPU with organisms minus identical bare-ground snapshot. RGB includes black/white/grey and terrain-matching colours.",
                    criteria = "World: per-channel pixels stay between exact bare ground and source RGB times[0.80,1.08], tolerance0.02, bounds rounded outward to the 8-bit capture grid; no mandatory contrast. Grey checkpoints at11.8/12.2px check outline boundary. Normal levels2/3 retain RGB distance>=0.18 in2.5% of cell and core channel error<=0.12. No saturation or restricted-palette criterion." });
            }
        }
        SelectLayerThroughUi(main, 0);
        await Step5HighlightPixelChecks();
    }

    private async Task Step5HighlightPixelChecks()
    {
        Main main = viewer!; var board = main.DepthView;
        var bare = ViewerFixtures.Create(0, run: 540);
        var cells = bare.Cells.ToArray();
        var specimens = new[] { (46, "medium RGB threshold crossing", 0x404040, "other"),
            (50, "near-black pale boundary", 0x080808, "other"), (54, "matched RGB", 0x3399AA, "match") };
        foreach (var specimen in specimens)
            cells[50 * bare.Width + specimen.Item1] = cells[50 * bare.Width + specimen.Item1] with
            { OrganismId = specimen.Item1, ColorArgb = unchecked((int)0xFF000000) | specimen.Item3, PatternKey = specimen.Item4 };
        var occupied = bare with { Cells = Array.AsReadOnly(cells), Population = specimens.Length };
        main.DisplayFrame(bare, true); main.SetViewMode(0); SelectLayerThroughUi(main, 1);
        board.SetDetailLevel(3); board.CenterOn(50, 50); board.HighlightedPattern = null;
        board.SelectedCell = null; board.SelectedOrganismId = null; board.ShowMarkers = board.ShowTrail = false;
        await Frames(4);
        using var ground = board.BoardViewport.GetTexture().GetImage();
        main.DisplayFrame(occupied, true); await Frames(4);
        using var normal = board.BoardViewport.GetTexture().GetImage();
        board.HighlightedPattern = "match"; await Frames(4);
        using var highlighted = board.BoardViewport.GetTexture().GetImage();
        foreach (var specimen in specimens)
        {
            Vector2[] quad = new[] { new Vector2(specimen.Item1, 50), new Vector2(specimen.Item1 + 1, 50),
                new Vector2(specimen.Item1 + 1, 51), new Vector2(specimen.Item1, 51) }.Select(point =>
                    board.BoardCamera.UnprojectPosition(new Vector3(point.X, 0, point.Y))).ToArray();
            int covered = 0, brightened = 0; float largestFall = 0, largestChange = 0;
            for (int y = (int)MathF.Floor(quad.Min(point => point.Y)); y <= (int)MathF.Ceiling(quad.Max(point => point.Y)); y++)
            for (int x = (int)MathF.Floor(quad.Min(point => point.X)); x <= (int)MathF.Ceiling(quad.Max(point => point.X)); x++)
            {
                if (!InsidePolygon(new Vector2(x + .5f, y + .5f), quad)) continue;
                Color background = ground.GetPixel(x, y), before = normal.GetPixel(x, y), after = highlighted.GetPixel(x, y);
                if (ColorDistance(background, before) < .08f) continue;
                covered++;
                // Paired identical ground/geometry avoids mistaking increased contrast of a darker
                // organism on pale ground for a brighter organism. Compare signed pixel contributions.
                Color originalContribution = before - background, dimmedContribution = after - background;
                if (dimmedContribution.R - originalContribution.R > .015f || dimmedContribution.G - originalContribution.G > .015f ||
                    dimmedContribution.B - originalContribution.B > .015f) brightened++;
                largestFall = Math.Max(largestFall, before.R + before.G + before.B - after.R - after.G - after.B);
                largestChange = Math.Max(largestChange, ColorDistance(before, after));
            }
            Check("pattern filtering preserves correct body and boundary emphasis: " + specimen.Item2,
                covered > 10 && (specimen.Item4 == "match" ? largestChange < .005f : brightened == 0 && largestFall > .10f),
                $"covered={covered}; brightened={brightened}; largest channel-sum decrease={largestFall:0.000}; largest RGB change={largestChange:0.000}");
        }
        board.HighlightedPattern = null; SelectLayerThroughUi(main, 0);
    }

    private sealed record RgbPixelSample(string Name, int CellPixels, int ContrastPixels, int CorePixels, double CoreChannelError, int OutlinePixels, string OutlineEvidence)
    {
        public double ContrastFraction => ContrastPixels / (double)Math.Max(1, CellPixels);
    }

    private static RgbPixelSample MeasureRgbPixels(BoardView3D board, Vector2I cell, (string Name, int Rgb) sample, Image ground, Image organisms)
    {
        Vector2[] Quad(float low, float high) => new[] { new Vector2(low, low), new Vector2(high, low),
            new Vector2(high, high), new Vector2(low, high) }.Select(offset =>
                board.BoardCamera.UnprojectPosition(new Vector3(cell.X + offset.X, 0, cell.Y + offset.Y))).ToArray();
        Vector2[] cellQuad = Quad(0, 1), coreQuad = Quad(.42f, .58f);
        int left = (int)MathF.Floor(cellQuad.Min(point => point.X)), right = (int)MathF.Ceiling(cellQuad.Max(point => point.X));
        int top = (int)MathF.Floor(cellQuad.Min(point => point.Y)), bottom = (int)MathF.Ceiling(cellQuad.Max(point => point.Y));
        if (left < 0 || top < 0 || right >= ground.GetWidth() || bottom >= ground.GetHeight())
            throw new InvalidOperationException($"RGB readability fixture {sample.Name} cell {cell} is not fully visible.");
        int pixels = 0, contrast = 0, corePixels = 0, outlinePixels = 0; Color core = new(0, 0, 0, 0);
        var outlineEvidence = new List<string>(3);
        bool diagnoseBorderless = board.DetailLevel == 1;
        Color rgb = Color.Color8((byte)((sample.Rgb >> 16) & 255), (byte)((sample.Rgb >> 8) & 255), (byte)(sample.Rgb & 255));
        // Compare on the capture's 8-bit channel grid: a fractional bound between codes must
        // not misclassify ordinary rounding as an outline (e.g. near-black code 1 vs bound 1.3).
        static float LowerBound(float background, float source) => MathF.Floor((Math.Min(background, source * .80f) - .02f) * 255) / 255;
        static float UpperBound(float background, float source) => MathF.Ceiling((Math.Max(background, Math.Min(1, source * 1.08f)) + .02f) * 255) / 255;
        static bool OutsideBodyBlend(float pixel, float background, float source) =>
            pixel < LowerBound(background, source) || pixel > UpperBound(background, source);
        for (int y = top; y <= bottom; y++)
        for (int x = left; x <= right; x++)
        {
            Vector2 point = new(x + .5f, y + .5f);
            if (!InsidePolygon(point, cellQuad)) continue;
            pixels++;
            Color current = organisms.GetPixel(x, y), background = ground.GetPixel(x, y);
            if (ColorDistance(background, current) >= .18f) contrast++;
            // A borderless body and alpha fringe can only blend source-like RGB with this same ground.
            // Values outside that range identify a dark/pale contrast outline without duplicating the shader.
            if (OutsideBodyBlend(current.R, background.R, rgb.R) || OutsideBodyBlend(current.G, background.G, rgb.G) ||
                OutsideBodyBlend(current.B, background.B, rgb.B))
            {
                outlinePixels++;
                if (diagnoseBorderless && outlineEvidence.Count < 3)
                {
                    string Channel(float pixel, float bare, float source) =>
                        $"pixel={pixel:0.000000},ground={bare:0.000000},source={source:0.000000}," +
                        $"allowed=[{LowerBound(bare, source):0.000000},{UpperBound(bare, source):0.000000}]";
                    outlineEvidence.Add($"offender({x},{y}) R({Channel(current.R, background.R, rgb.R)}) " +
                        $"G({Channel(current.G, background.G, rgb.G)}) B({Channel(current.B, background.B, rgb.B)})");
                }
            }
            if (InsidePolygon(point, coreQuad)) { core += current; corePixels++; }
        }
        core /= Math.Max(1, corePixels);
        double error = Math.Max(Math.Abs(core.R - ((sample.Rgb >> 16) & 255) / 255f),
            Math.Max(Math.Abs(core.G - ((sample.Rgb >> 8) & 255) / 255f), Math.Abs(core.B - (sample.Rgb & 255) / 255f)));
        return new RgbPixelSample(sample.Name, pixels, contrast, corePixels, error, outlinePixels, string.Join(" | ", outlineEvidence));
    }

    private async Task Step5Previews()
    {
        if (!gpu || !outputValidated) return;
        Main main = viewer!; var board = main.DepthView;
        var fixture = Step4Fixtures.Create(1800, run: 550, variedActions: true);
        // Illustrative full RGB values, not a claimed ecological state or pattern palette.
        var cells = fixture.Cells.Select(cell => cell.OrganismId.HasValue ? cell with
        { ColorArgb = unchecked((int)0xFF000000) | ((cell.X * 37 + cell.Y * 11) & 255) << 16 |
            ((cell.X * 13 + cell.Y * 53) & 255) << 8 | ((cell.X * 67 + cell.Y * 7) & 255) } : cell).ToArray();
        fixture = fixture with { Cells = Array.AsReadOnly(cells) };
        main.DisplayFrame(fixture, true); main.SetViewMode(0);
        foreach (var preview in new[] { (1, 1, "overview"), (0, 2, "habitat"), (0, 4, "inspect") })
        {
            SelectLayerThroughUi(main, preview.Item1);
            board.SetDetailLevel(preview.Item2);
            if (preview.Item2 == 4) board.FocusSelection();
            board.ShowMarkers = true; await Frames(4);
            using var image = GetViewport().GetTexture().GetImage();
            Check("capture actual Step5 RGB " + preview.Item3,
                image.SavePng(Path.Combine(Path.GetDirectoryName(output)!, $"r03_step5_{preview.Item3}.png")) == Error.Ok);
        }
        SelectLayerThroughUi(main, 0);
    }
}
