using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Presentation;
using Godot;

namespace Fistnet.Genepool.GodotViewer.Verification;

public partial class VerificationRunner
{
    private async Task PerspectiveChecks()
    {
        Step5RgbIdentityChecks();
        Main main = viewer!;
        var board = main.DepthView;
        var fixture = Step4Fixtures.Create(1000, run: 160);
        main.DisplayFrame(fixture, true);
        main.SetViewMode(0);
        foreach (var size in new[] { new Vector2I(1366, 768), new Vector2I(1920, 1080) })
        {
            GetTree().Root.Size = size; board.Fit(); await Frames(4);
            Check("angled uses actual perspective camera " + size, board.BoardCamera.Projection == Camera3D.ProjectionType.Perspective);
            Vector2[] corners = NativeCorners(board);
            float back = corners[0].DistanceTo(corners[1]), front = corners[3].DistanceTo(corners[2]);
            Check("centred trapezoid has the owner's stronger front-to-back taper " + size, front > back * 1.5f,
                $"front={front:0.00}px; back={back:0.00}px; ratio={front / back:0.000}");
            Vector2 backMiddle = (corners[0] + corners[1]) / 2, frontMiddle = (corners[2] + corners[3]) / 2;
            Check("centred trapezoid keeps both front and back edges horizontal " + size,
                Math.Abs(corners[1].Y - corners[0].Y) < .5f && Math.Abs(corners[2].Y - corners[3].Y) < .5f,
                $"back={corners[0]}->{corners[1]}; front={corners[3]}->{corners[2]}");
            Check("centred trapezoid is left-right symmetric around the viewport centre " + size,
                Math.Abs(backMiddle.X - board.Size.X / 2) < .5f && Math.Abs(frontMiddle.X - board.Size.X / 2) < .5f);
            float silhouette = (frontMiddle.Y - backMiddle.Y) / front;
            Check("centred trapezoid has a visibly low oblique silhouette " + size, silhouette is > .35f and < .55f,
                $"height/front-width={silhouette:0.000}");
            float farRow = NativeProject(board, new Vector2(5, 10)).DistanceTo(NativeProject(board, new Vector2(95, 10)));
            float nearRow = NativeProject(board, new Vector2(5, 90)).DistanceTo(NativeProject(board, new Vector2(95, 90)));
            float farGap = NativeProject(board, new Vector2(50, 10)).DistanceTo(NativeProject(board, new Vector2(50, 20)));
            float nearGap = NativeProject(board, new Vector2(50, 80)).DistanceTo(NativeProject(board, new Vector2(50, 90)));
            Check("grid rows and spacing visibly recede with distance " + size,
                nearRow > farRow * 1.1f && nearGap > farGap * 1.1f, $"rows={nearRow / farRow:0.000}; gaps={nearGap / farGap:0.000}");
            var safe = new Rect2(Vector2.One * 4, board.Size - Vector2.One * 8);
            Check("perspective fit includes all four real board corners " + size, corners.All(safe.HasPoint),
                string.Join("; ", corners.Select(point => point.ToString())) + "; size=" + board.Size);
            Check("native clip planes preserve all ground and colony corners " + size, WholeBoardBetweenClipPlanes(board),
                $"near={board.BoardCamera.Near}; far={board.BoardCamera.Far}");
            foreach (var cell in new[] { (0, 0), (12, 10), (50, 50), (84, 87), (99, 99) })
            {
                Vector2 actual = NativeProject(board, new Vector2(cell.Item1 + .5f, cell.Item2 + .5f));
                Check($"native rendered projection agrees with overlay cell {cell} {size}",
                    board.CellCenter(cell.Item1, cell.Item2).DistanceTo(actual) < .6f);
                Check($"native projected near/far cell picks correctly {cell} {size}", board.CellAt(actual) == cell);
            }
            Vector2? outside = null;
            Vector2 minimum = new(corners.Min(p => p.X), corners.Min(p => p.Y));
            Vector2 maximum = new(corners.Max(p => p.X), corners.Max(p => p.Y));
            for (int y = 1; y < 20 && outside == null; y++)
            for (int x = 1; x < 20 && outside == null; x++)
            {
                Vector2 candidate = minimum + (maximum - minimum) * new Vector2(x / 20f, y / 20f);
                if (!InsidePolygon(candidate, corners)) outside = candidate;
            }
            Check("empty space inside projected bounding box is not a board cell " + size,
                outside.HasValue && board.CellAt(outside.Value) == null, outside?.ToString());
            main.SetViewMode(1); board.Fit(); await Frames(3);
            Vector2[] flat = NativeCorners(board);
            Check("top-down remains orthographic and unskewed " + size,
                board.BoardCamera.Projection == Camera3D.ProjectionType.Orthogonal &&
                Math.Abs(flat[0].DistanceTo(flat[1]) - flat[3].DistanceTo(flat[2])) < .5f &&
                Math.Abs(flat[0].Y - flat[1].Y) < .5f && Math.Abs(flat[0].X - flat[3].X) < .5f);
            main.SetViewMode(0);
        }

        board.SetDetailLevel(2); board.CenterOn(50, 50); await Frames(3);
        Vector2 anchorWorld = new(53.5f, 53.5f), anchor = NativeProject(board, anchorWorld);
        board.ZoomAt(1.4f, anchor); await Frames(2);
        Check("perspective zoom preserves ground under the pointer", NativeProject(board, anchorWorld).DistanceTo(anchor) < 1,
            $"before={anchor}; after={NativeProject(board, anchorWorld)}");
        Vector2 panGround = NativeGround(board, board.Size / 2), before = board.Size / 2, delta = new(37, -23);
        board.PanBy(delta); await Frames(2);
        Check("perspective pan translates the center ground anchor by the requested pixels",
            NativeProject(board, panGround).DistanceTo(before + delta) < 1.5f);
        await PerspectiveDragCheck();

        board.Fit(); board.ZoomBy(80); await Frames(3);
        Vector2[] maximumFootprint = NativeFootprint(board);
        Check("maximum perspective zoom has a finite native ground footprint",
            Math.Abs(board.Zoom - 80) < .001f && maximumFootprint.All(point => point.IsFinite()) &&
            maximumFootprint[0].DistanceTo(maximumFootprint[1]) > .1f && maximumFootprint[0].DistanceTo(maximumFootprint[3]) > .1f,
            $"zoom={board.Zoom}; native FOV={board.BoardCamera.Fov}; footprint={string.Join(';', maximumFootprint.Select(point => point.ToString()))}");
        Check("native clip planes retain board depths at maximum zoom", WholeBoardBetweenClipPlanes(board));
        foreach (var point in new[] { new Vector2(50, 50), new Vector2(49.5f, 50.5f), new Vector2(50.5f, 49.5f) })
            Check("maximum zoom cached projection matches native camera " + point,
                board.ProjectGround(point).DistanceTo(NativeProject(board, point)) < 1,
                $"cached={board.ProjectGround(point)}; native={NativeProject(board, point)}; FOV={board.BoardCamera.Fov}");

        board.SetDetailLevel(3); board.CenterOn(50, 50); await Frames(3);
        Vector2[] footprint = NativeFootprint(board);
        int expected = 0, missed = 0, extra = 0;
        foreach (ViewCell cell in fixture.Cells)
        {
            bool intersects = CellIntersectsNativeFootprint(cell.X, cell.Y, footprint);
            bool actual = board.IsCellVisible(cell.X, cell.Y);
            if (intersects) expected++;
            if (intersects && !actual) missed++;
            if (!intersects && actual) extra++;
        }
        int boundingCells = board.VisibleBounds.Size.X * board.VisibleBounds.Size.Y;
        Check("perspective culling matches independent native-ray polygon clipping",
            missed == 0 && extra == 0 && board.FoodInstanceCount == expected,
            $"expected={expected}; actual={board.FoodInstanceCount}; missed={missed}; extra={extra}");
        Check("perspective culling excludes bounding-box corners", boundingCells > expected,
            $"bounding cells={boundingCells}; polygon cells={expected}");
        Check("published footprint matches native corner ray intersections", SamePolygon(footprint, board.VisiblePolygon, .015f));
        Check("minimap receives perspective polygon instead of a bounding rectangle", SamePolygon(footprint, main.Minimap.ViewFootprint, .015f));
        if (gpu)
        {
            await PerspectiveActionPixelChecks();
            await PerspectiveColonyCoverageChecks();
            await OrganismsGroundPixelChecks();
            await Step5RgbPixelChecks();
        }
    }

    private async Task PerspectiveDragCheck()
    {
        var board = viewer!.DepthView;
        board.CenterOn(50, 50); await Frames(2);
        Vector2 start = board.Size * new Vector2(.47f, .46f), end = start + new Vector2(43, 29);
        Vector2 heldGround = NativeGround(board, start);
        var delivered = new List<(string Kind, Vector2 Position)>();
        if (gpu)
        {
            Vector2 globalStart = board.GlobalPosition + start, globalEnd = board.GlobalPosition + end;
            void Observe(InputEvent input)
            {
                if (input is InputEventMouseButton { ButtonIndex: MouseButton.Middle } button)
                    delivered.Add((button.Pressed ? "press" : "release", button.Position));
                else if (input is InputEventMouseMotion motion) delivered.Add(("motion", motion.Position));
            }
            Input.FlushBufferedEvents();
            bool accumulated = Input.UseAccumulatedInput;
            board.GuiInput += Observe;
            try
            {
                // Keep the real viewport/GUI route, but deliver this synthetic gesture together. Waiting
                // between its held-button events lets unrelated OS mouse motion become part of the drag.
                Input.UseAccumulatedInput = false;
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true,
                    Position = globalStart, GlobalPosition = globalStart });
                Input.ParseInputEvent(new InputEventMouseMotion { Position = globalEnd, GlobalPosition = globalEnd,
                    Relative = end - start, ButtonMask = MouseButtonMask.Middle });
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = false,
                    Position = globalEnd, GlobalPosition = globalEnd });
                Input.FlushBufferedEvents();
            }
            finally { Input.UseAccumulatedInput = accumulated; board.GuiInput -= Observe; }
            Check("perspective drag traverses the real Godot GUI input route",
                delivered.Count == 3 && delivered[0].Kind == "press" && delivered[1].Kind == "motion" && delivered[2].Kind == "release" &&
                delivered[0].Position.DistanceTo(start) < .1f && delivered[1].Position.DistanceTo(end) < .1f && delivered[2].Position.DistanceTo(end) < .1f,
                string.Join("; ", delivered.Select(item => $"{item.Kind}@{item.Position}")));
        }
        else
        {
            board._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = true, Position = start });
            board._GuiInput(new InputEventMouseMotion { Position = end, Relative = end - start, ButtonMask = MouseButtonMask.Middle });
            board._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Middle, Pressed = false, Position = end });
        }
        await Frames(2);
        Check(gpu ? "actual Godot middle-drag keeps the grabbed ground point under the cursor" :
            "headless perspective drag handler preserves its ground anchor", NativeProject(board, heldGround).DistanceTo(end) < 1.5f,
            $"expected={end}; actual={NativeProject(board, heldGround)}; GUI events={string.Join("; ", delivered.Select(item => $"{item.Kind}@{item.Position}"))}");
    }

    private async Task PerspectiveActionPixelChecks()
    {
        Main main = viewer!; var board = main.DepthView;
        var fixture = Step4Fixtures.Create(0, run: 170);
        main.DisplayFrame(fixture, true); main.SetViewMode(0);
        board.SetDetailLevel(3); board.CenterOn(50, 50);
        board.SelectedCell = null; board.SelectedOrganismId = null; board.ShowTrail = false;
        board.Layer = BoardLayer.Combined; await Frames(3);
        var locations = new[] { new Vector2(.40f, .25f), new Vector2(.60f, .75f) }
            .Select(uv => NativeGround(board, board.Size * uv))
            .Select(point => new Vector2I((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y))).ToArray();
        var actions = locations.Select((cell, index) => new ViewObservedAction(index + 1,
            new ViewAction(fixture.Season, 0, "Gather", "Self", "Synthetic completed gathering", 1, 0, 0,
                cell.X, cell.Y, cell.X, cell.Y, "fixture", "Synthetic known effect", FoodGathered: 1,
                TargetX: cell.X, TargetY: cell.Y))).ToArray();
        main.DisplayFrame(fixture with { SeasonActions = Array.AsReadOnly(actions) }, true);
        board.ShowMarkers = false; await Frames(4);
        using var quiet = board.BoardViewport.GetTexture().GetImage();
        board.ShowMarkers = true; await Frames(4);
        using var active = board.BoardViewport.GetTexture().GetImage();
        Vector2[] centers = locations.Select(cell => board.BoardCamera.UnprojectPosition(new Vector3(cell.X + .5f, 0, cell.Y + .5f))).ToArray();
        int[] nearby = new int[2]; int unexpected = 0;
        for (int y = 0; y < active.GetHeight(); y++)
        for (int x = 0; x < active.GetWidth(); x++)
        {
            if (ColorDistance(quiet.GetPixel(x, y), active.GetPixel(x, y)) < .025f) continue;
            Vector2 point = new(x, y);
            int closest = point.DistanceSquaredTo(centers[0]) <= point.DistanceSquaredTo(centers[1]) ? 0 : 1;
            if (point.DistanceTo(centers[closest]) <= 35) nearby[closest]++; else unexpected++;
        }
        Check("perspective action pixels appear at native projected near and far cells",
            board.ActionCueCount == 2 && nearby.All(count => count >= 5) && unexpected == 0,
            $"near/far changed pixels={string.Join(',', nearby)}; outside35px={unexpected}; cues={board.ActionCueCount}");
    }

    private async Task PerspectivePreviews()
    {
        if (!gpu || !outputValidated) return;
        Main main = viewer!; var board = main.DepthView;
        foreach (var preview in new[] { (0, 1, "world"), (0, 2, "detail"), (1, 1, "topdown") })
        {
            main.SetViewMode(preview.Item1); board.SetDetailLevel(preview.Item2); await Frames(4);
            using var image = GetViewport().GetTexture().GetImage();
            Check("capture actual perspective correction " + preview.Item3,
                image.SavePng(Path.Combine(Path.GetDirectoryName(output)!, $"r03_perspective_{preview.Item3}.png")) == Error.Ok);
            if (preview.Item3 == "world")
            {
                SelectLayerThroughUi(main, 1); await Frames(4);
                using var organisms = GetViewport().GetTexture().GetImage();
                Check("capture actual Organisms-only light-grey board",
                    organisms.SavePng(Path.Combine(Path.GetDirectoryName(output)!, "r03_perspective_organisms.png")) == Error.Ok);
                SelectLayerThroughUi(main, 0); await Frames(3);
            }
        }
        main.SetViewMode(0);
    }

    private static void SelectLayerThroughUi(Main main, int index)
    {
        main.LayerBox.Select(index);
        main.LayerBox.EmitSignal(OptionButton.SignalName.ItemSelected, (long)index);
    }

    private async Task OrganismsGroundPixelChecks()
    {
        Main main = viewer!; var board = main.DepthView;
        var fixture = Step4Fixtures.Appearance() with { RunId = 180 };
        foreach (int mode in new[] { 0, 1 })
        {
            main.DisplayFrame(fixture, true); main.SetViewMode(mode);
            board.SetDetailLevel(3); board.CenterOn(50, 50);
            board.SelectedCell = null; board.SelectedOrganismId = null; board.HighlightedPattern = null;
            board.ShowMarkers = board.ShowTrail = false;
            SelectLayerThroughUi(main, 0); await Frames(4);
            Vector2 dryPoint = board.BoardCamera.UnprojectPosition(new Vector3(48.5f, 0, 50.5f));
            Vector2 lushPoint = board.BoardCamera.UnprojectPosition(new Vector3(51.5f, 0, 50.5f));
            Color dry, lush, greyDry, greyLush;
            using (var combined = board.BoardViewport.GetTexture().GetImage())
            {
                dry = AveragePatch(combined, dryPoint, 3); lush = AveragePatch(combined, lushPoint, 3);
                Check("Combined preserves food0 brown and food10 green mode " + mode,
                    dry.R > dry.G && dry.G > dry.B && lush.G > lush.R && lush.G > lush.B,
                    $"food0={dry}; food10={lush}");
            }
            SelectLayerThroughUi(main, 1); await Frames(4);
            Check("Organisms layer selector reaches both renderers and minimap mode " + mode,
                main.LayerBox.Selected == 1 && board.Layer == BoardLayer.Organisms &&
                main.BoardView.Layer == BoardLayer.Organisms && main.Minimap.Layer == BoardLayer.Organisms);
            using (var organisms = board.BoardViewport.GetTexture().GetImage())
            {
                greyDry = AveragePatch(organisms, dryPoint, 3); greyLush = AveragePatch(organisms, lushPoint, 3);
                static bool LightNeutral(Color color) => Math.Min(color.R, Math.Min(color.G, color.B)) > .55f &&
                    Math.Max(color.R, Math.Max(color.G, color.B)) - Math.Min(color.R, Math.Min(color.G, color.B)) < .025f;
                Check("Organisms-only ground is light neutral grey at both food amounts mode " + mode,
                    LightNeutral(greyDry) && LightNeutral(greyLush), $"food0={greyDry}; food10={greyLush}");
            }
            var swapped = fixture.Cells.ToArray();
            swapped[50 * fixture.Width + 48] = swapped[50 * fixture.Width + 48] with { Food = 10 };
            swapped[50 * fixture.Width + 51] = swapped[50 * fixture.Width + 51] with { Food = 0 };
            main.DisplayFrame(fixture with { Season = fixture.Season + 1, Cells = Array.AsReadOnly(swapped) }, true); await Frames(4);
            using (var changedFood = board.BoardViewport.GetTexture().GetImage())
            {
                Color nowDry = AveragePatch(changedFood, dryPoint, 3), nowLush = AveragePatch(changedFood, lushPoint, 3);
                Check("Organisms-only grey is independent of changing food quantities mode " + mode,
                    ColorDistance(greyDry, nowDry) < .005f && ColorDistance(greyLush, nowLush) < .005f,
                    $"cell48 difference={ColorDistance(greyDry, nowDry):0.0000}; cell51 difference={ColorDistance(greyLush, nowLush):0.0000}");
            }
            main.DisplayFrame(fixture with { Season = fixture.Season + 2 }, true);
            SelectLayerThroughUi(main, 0); await Frames(4);
            using (var restored = board.BoardViewport.GetTexture().GetImage())
            {
                Color restoredDry = AveragePatch(restored, dryPoint, 3), restoredLush = AveragePatch(restored, lushPoint, 3);
                Check("returning to Combined restores the actual food palette mode " + mode,
                    ColorDistance(dry, restoredDry) < .015f && ColorDistance(lush, restoredLush) < .015f,
                    $"food0={restoredDry}; food10={restoredLush}");
            }
            measurements.Add(new { kind = "organisms_ground_pixels", angled = mode == 0,
                combinedFood0 = dry.ToString(), combinedFood10 = lush.ToString(),
                organismsFood0 = greyDry.ToString(), organismsFood10 = greyLush.ToString(),
                oracle = "Actual GPU image patches at native-camera-projected empty cells; UI layer signals and same-cell food quantity swap." });
        }
        main.SetViewMode(0);
    }

    private async Task PerspectiveColonyCoverageChecks()
    {
        Main main = viewer!; var board = main.DepthView;
        var fixture = Step4Fixtures.Create(10_000, run: 175);
        main.DisplayFrame(fixture, true); main.SetViewMode(0);
        board.SelectedCell = null; board.SelectedOrganismId = null;
        board.ShowMarkers = board.ShowTrail = false; board.HighlightedPattern = null;
        foreach (int level in new[] { 2, 3 })
        {
            board.SetDetailLevel(level); board.CenterOn(50, 50);
            board.Layer = BoardLayer.Food; await Frames(4);
            using var ground = board.BoardViewport.GetTexture().GetImage();
            board.Layer = BoardLayer.Combined; await Frames(4);
            using var colonies = board.BoardViewport.GetTexture().GetImage();
            var samples = new List<ColonyCoverage>();
            var seen = new HashSet<Vector2I>();
            for (int row = 0; row < 6; row++)
            for (int column = 0; column < 6; column++)
            {
                Vector2 world = NativeGround(board, board.Size * new Vector2(.15f + column * .14f, .15f + row * .14f));
                var cell = new Vector2I((int)MathF.Floor(world.X), (int)MathF.Floor(world.Y));
                if (!seen.Add(cell)) continue;
                samples.Add(MeasureColonyCoverage(board, cell, ground, colonies));
            }
            Check("perspective colony coverage samples 36 distinct near and far cells level " + level,
                samples.Count == 36 && samples.All(sample => sample.CorePixels >= 8));
            string detail = string.Join("; ", samples.OrderBy(sample => sample.CoreCoverage).Take(6).Select(sample =>
                $"{sample.Cell}: body={sample.BodyCoverage:P1}, core={sample.CoreCoverage:P1}, connected={sample.ConnectedShare:P1}"));
            // These are readable-mat contracts, independent of the shader's lobes/noise. Irregular fringe
            // is allowed; a visible organism must not collapse into a thin shard or lose its solid center.
            Check("perspective colonies retain visible body area level " + level,
                samples.All(sample => sample.BodyCoverage >= .15 && sample.BodyCoverage <= .60), detail, false);
            Check("perspective colonies retain their central body level " + level,
                samples.All(sample => sample.CoreCoverage >= .90), detail, false);
            Check("perspective colonies form one connected mat level " + level,
                samples.All(sample => sample.ConnectedShare >= .85), detail, false);
            measurements.Add(new { kind = "perspective_colony_coverage", detailLevel = level, sampleCount = samples.Count,
                minimumBodyCoverage = samples.Min(sample => sample.BodyCoverage), maximumBodyCoverage = samples.Max(sample => sample.BodyCoverage),
                minimumCoreCoverage = samples.Min(sample => sample.CoreCoverage), minimumConnectedShare = samples.Min(sample => sample.ConnectedShare),
                oracle = "Actual GPU Combined minus Food-only pixels, native Camera3D projected cells, central 20%-width square; 8-connected visible body mask.",
                thresholds = "RGB distance >0.08 identifies the body; area15%-60% of cell, central square>=90%, largest component>=85%." });
        }
    }

    private sealed record ColonyCoverage(Vector2I Cell, int CorePixels, double BodyCoverage, double CoreCoverage, double ConnectedShare);

    private static ColonyCoverage MeasureColonyCoverage(BoardView3D board, Vector2I cell, Image ground, Image colonies)
    {
        Vector2[] Quad(float low, float high) => new[] { new Vector2(low, low), new Vector2(high, low),
            new Vector2(high, high), new Vector2(low, high) }.Select(offset =>
                board.BoardCamera.UnprojectPosition(new Vector3(cell.X + offset.X, 0, cell.Y + offset.Y))).ToArray();
        Vector2[] cellQuad = Quad(0, 1), coreQuad = Quad(.4f, .6f);
        int left = (int)MathF.Floor(cellQuad.Min(point => point.X)), top = (int)MathF.Floor(cellQuad.Min(point => point.Y));
        int right = (int)MathF.Ceiling(cellQuad.Max(point => point.X)), bottom = (int)MathF.Ceiling(cellQuad.Max(point => point.Y));
        if (left < 0 || top < 0 || right >= ground.GetWidth() || bottom >= ground.GetHeight())
            throw new InvalidOperationException($"Coverage fixture cell {cell} is not fully visible.");
        int width = right - left + 1, height = bottom - top + 1;
        var body = new bool[width * height];
        int pixels = 0, bodyPixels = 0, corePixels = 0, coreBodyPixels = 0;
        for (int y = top; y <= bottom; y++)
        for (int x = left; x <= right; x++)
        {
            var point = new Vector2(x + .5f, y + .5f);
            if (!InsidePolygon(point, cellQuad)) continue;
            pixels++;
            bool changed = ColorDistance(ground.GetPixel(x, y), colonies.GetPixel(x, y)) > .08f;
            if (changed) { body[(y - top) * width + x - left] = true; bodyPixels++; }
            if (InsidePolygon(point, coreQuad)) { corePixels++; if (changed) coreBodyPixels++; }
        }
        int largest = 0;
        var queue = new Queue<int>();
        for (int index = 0; index < body.Length; index++)
        {
            if (!body[index]) continue;
            body[index] = false; queue.Enqueue(index); int connected = 0;
            while (queue.Count > 0)
            {
                int current = queue.Dequeue(); connected++;
                int x = current % width, y = current / width;
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if ((dx == 0 && dy == 0) || nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    int next = ny * width + nx;
                    if (body[next]) { body[next] = false; queue.Enqueue(next); }
                }
            }
            largest = Math.Max(largest, connected);
        }
        return new ColonyCoverage(cell, corePixels, bodyPixels / (double)Math.Max(1, pixels),
            coreBodyPixels / (double)Math.Max(1, corePixels), largest / (double)Math.Max(1, bodyPixels));
    }

    // These oracles use the actual Godot camera, not BoardView3D's cached ground homography.
    private static Vector2 NativeProject(BoardView3D board, Vector2 ground) =>
        board.BoardCamera.UnprojectPosition(new Vector3(ground.X, 0, ground.Y)) * board.Size / (Vector2)board.BoardViewport.Size;

    private static Vector2 NativeGround(BoardView3D board, Vector2 local)
    {
        Vector2 point = local * (Vector2)board.BoardViewport.Size / board.Size;
        Vector3 origin = board.BoardCamera.ProjectRayOrigin(point), direction = board.BoardCamera.ProjectRayNormal(point);
        if (Math.Abs(direction.Y) < .000001f) throw new InvalidOperationException("Fixture camera ray misses the board plane.");
        Vector3 ground = origin - direction * (origin.Y / direction.Y);
        return new Vector2(ground.X, ground.Z);
    }

    private static Vector2[] NativeCorners(BoardView3D board) => new[] { Vector2.Zero,
        new Vector2(board.Frame!.Width, 0), new Vector2(board.Frame.Width, board.Frame.Height), new Vector2(0, board.Frame.Height) }
        .Select(point => NativeProject(board, point)).ToArray();

    private static Vector2[] NativeFootprint(BoardView3D board) => new[] { Vector2.Zero,
        new Vector2(board.Size.X, 0), board.Size, new Vector2(0, board.Size.Y) }.Select(point => NativeGround(board, point)).ToArray();

    private static bool WholeBoardBetweenClipPlanes(BoardView3D board)
    {
        Transform3D inverse = board.BoardCamera.GlobalTransform.AffineInverse();
        foreach (float height in new[] { 0f, .024f })
        foreach (float x in new[] { 0f, (float)board.Frame!.Width })
        foreach (float y in new[] { 0f, (float)board.Frame!.Height })
        {
            float depth = -(inverse * new Vector3(x, height, y)).Z;
            if (depth <= board.BoardCamera.Near || depth >= board.BoardCamera.Far) return false;
        }
        return true;
    }

    private static bool SamePolygon(IReadOnlyList<Vector2> expected, IReadOnlyList<Vector2> actual, float tolerance) =>
        expected.Count == actual.Count && expected.All(point => actual.Any(other => point.DistanceTo(other) <= tolerance));

    private static bool InsidePolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector2 a = polygon[i], b = polygon[j];
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    // Independent Sutherland-Hodgman clipping; production uses separating-axis visibility tests.
    private static bool CellIntersectsNativeFootprint(int x, int y, IReadOnlyList<Vector2> footprint)
    {
        var polygon = footprint.ToList();
        foreach (var edge in new[] { (0, (float)x, true), (0, x + 1f, false), (1, (float)y, true), (1, y + 1f, false) })
        {
            var clipped = new List<Vector2>();
            if (polygon.Count == 0) return false;
            Vector2 previous = polygon[^1];
            float Coordinate(Vector2 point) => edge.Item1 == 0 ? point.X : point.Y;
            bool IsInside(Vector2 point) => edge.Item3 ? Coordinate(point) >= edge.Item2 : Coordinate(point) <= edge.Item2;
            bool previousInside = IsInside(previous);
            foreach (Vector2 current in polygon)
            {
                bool currentInside = IsInside(current);
                if (previousInside != currentInside)
                {
                    float fraction = (edge.Item2 - Coordinate(previous)) / (Coordinate(current) - Coordinate(previous));
                    clipped.Add(previous.Lerp(current, fraction));
                }
                if (currentInside) clipped.Add(current);
                previous = current; previousInside = currentInside;
            }
            polygon = clipped;
        }
        double area = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
            area += (double)a.X * b.Y - (double)a.Y * b.X;
        }
        return Math.Abs(area) > .000002;
    }
}
