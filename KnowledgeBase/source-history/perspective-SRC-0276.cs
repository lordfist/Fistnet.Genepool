using System.Diagnostics;
using System.Text.Json;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Presentation;
using Godot;
using DnaCommon = Fistnet.Genepool.Dna.Common;

namespace Fistnet.Genepool.GodotViewer.Verification;

public partial class VerificationRunner
{
    private async Task DepthPassiveChecks()
    {
        Main main = viewer!;
        GetTree().Root.Size = new Vector2I(1920, 1080);
        var fixture = Step4Fixtures.Create(1000);
        main.DisplayFrame(fixture, true);
        main.SetViewMode(0);
        await Frames(4);
        var board = main.DepthView;
        Check("depth surface is passive and shares the displayed snapshot", !main.HasSimulation &&
            ReferenceEquals(board.Frame, main.DisplayedFrame));
        foreach (int mode in new[] { 0, 1 })
        {
            main.SetViewMode(mode); board.Fit(); await Frames(3);
            Check($"depth camera mode {mode}", board.Angled == (mode == 0));
            Check($"depth full-board instance coverage mode {mode}",
                board.FoodInstanceCount == 10_000 && board.OrganismInstanceCount == 1000);
            Check($"depth fitted view exposes four actual world edges mode {mode}", board.VisibleWorldEdgeCount == 4);
            foreach (var cell in new[] { (0, 0), (50, 50), (99, 99) })
                Check($"depth fitted picking mode {mode} cell {cell}", board.CellAt(board.CellCenter(cell.Item1, cell.Item2)) == cell);
            Check($"depth outside picking mode {mode}", board.CellAt(new Vector2(-1, 20)) == null && board.CellAt(board.Size + Vector2.One) == null);
            foreach (int level in new[] { 1, 2, 3, 4 })
            {
                board.SetDetailLevel(level); await Frames(2);
                Check($"depth preset {level} mode {mode}", board.DetailLevel == level);
                Check($"depth center picking level {level} mode {mode}", board.CellAt(board.CellCenter(50, 50)) == (50, 50));
                Rect2I bounds = board.VisibleBounds;
                int expectedFood = fixture.Cells.Count(cell => bounds.HasPoint(new Vector2I(cell.X, cell.Y)));
                int expectedOrganisms = fixture.Cells.Count(cell => cell.OrganismId.HasValue && bounds.HasPoint(new Vector2I(cell.X, cell.Y)));
                Check($"depth visible-area batches level {level} mode {mode}",
                    board.FoodInstanceCount == expectedFood && board.OrganismInstanceCount == expectedOrganisms,
                    $"bounds={bounds}; food={board.FoodInstanceCount}/{expectedFood}; organisms={board.OrganismInstanceCount}/{expectedOrganisms}");
                if (level > 1)
                {
                    Check($"depth interior crop is smaller than the world level {level} mode {mode}", board.FoodInstanceCount < 10_000);
                    Check($"depth interior crop does not create a false world border level {level} mode {mode}", board.VisibleWorldEdgeCount == 0);
                }
            }
            board.PanBy(new Vector2(46, -31)); await Frames(2);
            Check($"depth panned picking mode {mode}", board.CellAt(board.CellCenter(50, 50)) == (50, 50));
            board.SelectedOrganismId = fixture.Selected.Id; board.FocusSelection(); await Frames(2);
            Check($"depth focus locates selected identity mode {mode}",
                board.CellAt(board.CellCenter(fixture.Selected.X, fixture.Selected.Y)) == (fixture.Selected.X, fixture.Selected.Y));
            board.PanBy(new Vector2(100_000, 100_000)); await Frames(2);
            Check($"depth top-left world edge remains identifiable mode {mode}", board.VisibleWorldEdgeCount == 2 &&
                board.CellAt(board.CellCenter(0, 0)) == (0, 0));
            board.CenterOn(50, 50);
        }
        await DepthActionChecks();
        await DepthSelectionLifetimeChecks();
        await DepthMinimapChecks();
        foreach (var size in new[] { new Vector2I(1366, 768), new Vector2I(1920, 1080) })
        {
            GetTree().Root.Size = size; board.SetDetailLevel(3); await Frames(4);
            Check("depth resize keeps picking coherent " + size, board.CellAt(board.CellCenter(50, 50)) == (50, 50));
            Check("depth board and details do not overlap " + size,
                board.GetGlobalRect().End.X <= Control<TabContainer>("DetailTabs").GetGlobalRect().Position.X + 1);
            Check("depth camera selector fits " + size, main.CameraBox.GetGlobalRect().End.X <= size.X);
        }
        var old = main.DisplayedFrame!;
        board.HighlightedPattern = "pattern-0"; board.SelectedOrganismId = old.Selected.Id;
        main.DisplayFrame(old with { RunId = old.RunId + 1, Season = 0, Selected = null!, SeasonActions = Array.Empty<ViewObservedAction>() }, true);
        Check("depth restart clears selection and pattern", board.SelectedOrganismId == null && board.HighlightedPattern == null);
        main.SetViewMode(2); await Frames(2);
        Check("original 2D remains selectable on the same world", ReferenceEquals(main.BoardView.Frame, main.DisplayedFrame) && main.BoardView.Visible);
    }

    private async Task DepthActionChecks()
    {
        Main main = viewer!;
        var board = main.DepthView;
        var frame = Step4Fixtures.Create(10_000, run: 120);
        main.SetViewMode(1); main.DisplayFrame(frame, true); board.ShowMarkers = true;
        board.SetDetailLevel(2); await Frames(2);
        Check("Habitat keeps action cues quiet", board.ActionCueCount == 0);
        board.SetDetailLevel(3); main.DisplayFrame(frame with { Season = 65,
            SeasonActions = StampActions(frame.SeasonActions, 65) }, true); await Frames(2);
        int expected = main.DisplayedFrame!.SeasonActions.Count(item => board.VisibleBounds.HasPoint(
            new Vector2I(item.Action.DestinationX, item.Action.DestinationY)));
        Check("Organism detail draws every visible completed action without a 64-item cap",
            expected > 64 && board.ActionCueCount == expected, $"visible={expected}; cues={board.ActionCueCount}");
        board.SetDetailLevel(4); board.FocusSelection(); await Frames(2);
        Check("Inspect draws only the focused organism's current action", board.ActionCueCount == 1,
            "cues=" + board.ActionCueCount);
        board.SelectedOrganismId = null; await Frames(2);
        Check("Inspect requires an explicit focused organism", board.ActionCueCount == 0);
        board.SelectedOrganismId = frame.Selected.Id;
        await Delay(.7);
        Check("paused completed outcomes remain available for inspection", board.ActionCueCount == 1);
        main.DisplayFrame(main.DisplayedFrame! with { Season = 66, IsRunning = true, Status = "Running",
            SeasonActions = StampActions(frame.SeasonActions, 66) }, true);
        await Frames(2);
        Check("new running season displays the focused completed action", board.ActionCueCount == 1);
        await Delay(.9);
        var acknowledged = main.DisplayedFrame! with { AcknowledgedCommand = 99 };
        main.DisplayFrame(acknowledged, true); await Frames(2);
        Check("same-season acknowledgment cannot replay expired depth actions", board.ActionCueCount == 0);
        main.DisplayFrame(acknowledged with { Season = 67, SeasonActions = StampActions(frame.SeasonActions, 67) }, true); await Frames(2);
        Check("new completed season re-enables focused action", board.ActionCueCount == 1);
        main.DisplayFrame(main.DisplayedFrame! with { Season = 68, SeasonActions = frame.SeasonActions }, true); await Frames(2);
        Check("stale event batches are not presented as current actions", board.ActionCueCount == 0);
        await DepthOutcomeBoundaryChecks(frame);
    }

    private async Task DepthOutcomeBoundaryChecks(ViewFrame frame)
    {
        Main main = viewer!; var board = main.DepthView;
        board.SetDetailLevel(3); board.CenterOn(50, 50);
        Rect2I bounds = board.VisibleBounds;
        int left = bounds.Position.X, right = bounds.End.X, y = 50;
        ViewAction Basis(string action, int x) => new(70, 1, action, "MiddleRight", "Synthetic recorded outcome",
            0, -1, 0, x, y, x, y, "fixture", "Known outcome", TargetX: x + 1, TargetY: y);
        var moved = Basis("Move", right - 1) with { Moved = true, DestinationX = right };
        var blocked = moved with { Moved = false, DestinationX = moved.SourceX, Result = "Blocked" };
        var attack = Basis("Attack", left - 1) with { Damage = 2, TargetX = left };
        var child = Basis("Reproduce", left - 2) with { BirthPlaced = true, BirthX = left, BirthY = y, BirthOrganismId = 7654 };
        Check("movement cue requires a completed movement", BoardView3D.ClassifyActionCue(moved) == "move" &&
            BoardView3D.ClassifyActionCue(blocked) == "ineffective");
        Check("attack cue requires recorded damage", BoardView3D.ClassifyActionCue(attack) == "attack" &&
            BoardView3D.ClassifyActionCue(attack with { Damage = 0 }) == "ineffective");
        Check("eating carried food is distinct from gathering and healing", BoardView3D.ClassifyActionCue(
            Basis("Eat", 50) with { FoodSpent = 1, Healing = 4 }) == "consume" &&
            BoardView3D.ClassifyActionCue(Basis("Gather", 50) with { FoodGathered = 1 }) == "gather" &&
            BoardView3D.ClassifyActionCue(Basis("Heal", 50) with { Healing = 4 }) == "heal");
        Check("birth and mutation cues require recorded effects", BoardView3D.ClassifyActionCue(child) == "birth" &&
            BoardView3D.ClassifyActionCue(child with { BirthPlaced = false }) == "ineffective" &&
            BoardView3D.ClassifyActionCue(Basis("Mutate", 50) with { Mutations = 2 }) == "mutate" &&
            BoardView3D.ClassifyActionCue(Basis("Mutate", 50)) == "ineffective");
        var observations = new[] { new ViewObservedAction(1, moved), new ViewObservedAction(2, attack),
            new ViewObservedAction(3, child), new ViewObservedAction(4, Basis("Gather", left - 5) with { FoodGathered = 1 }) };
        main.DisplayFrame(frame with { Season = 70, IsRunning = false, Status = "Paused",
            SeasonActions = Array.AsReadOnly(observations) }, true); await Frames(2);
        Check("viewport includes actions intersecting by source target or actual child placement", board.ActionCueCount == 3,
            "cues=" + board.ActionCueCount);
        board.CenterOn(10, 10); await Frames(2);
        Check("panning drops offscreen cues without retaining an old viewport subset", board.ActionCueCount == 0);
        board.CenterOn(50, 50); await Frames(2);
        Check("panning back restores every visible paused outcome", board.ActionCueCount == 3);
    }

    private static IReadOnlyList<ViewObservedAction> StampActions(IReadOnlyList<ViewObservedAction> actions, int season) =>
        Array.AsReadOnly(actions.Select(item => item with { Action = item.Action with { Season = season } }).ToArray());

    private async Task DepthSelectionLifetimeChecks()
    {
        Main main = viewer!; var board = main.DepthView;
        var frame = Step4Fixtures.Create(1000, run: 140);
        main.DisplayFrame(frame, true); board.SetDetailLevel(4); board.FocusSelection();
        var followed = frame.Selected;
        ViewCell original = frame.Cells.First(cell => cell.OrganismId == followed.Id);
        float seed = BoardView3D.ColonySeed(original);
        Check("colony shape seed follows identity across movement", seed == BoardView3D.ColonySeed(
            original with { X = (original.X + 31) % 100, Y = (original.Y + 19) % 100 }) && seed is >= 0 and <= 1);

        const long replacementId = 900_001;
        var cells = frame.Cells.ToArray();
        cells[followed.Y * frame.Width + followed.X] = original with { OrganismId = replacementId, PatternKey = "replacement" };
        var replacementAction = new ViewAction(65, 0, "Gather", "Self", "Gathered local food", 1, -1, .2f,
            followed.X, followed.Y, followed.X, followed.Y, "fixture", "Known replacement outcome", FoodGathered: 1,
            TargetX: followed.X, TargetY: followed.Y);
        var ended = frame with { Season = 65, Cells = Array.AsReadOnly(cells),
            Selected = followed with { IsAlive = false, Health = 0 },
            SeasonActions = Array.AsReadOnly(new[] { new ViewObservedAction(replacementId, replacementAction) }) };
        main.DisplayFrame(ended, true); await Frames(3);
        Check("death retains the followed identity rather than adopting a replacement occupant",
            main.SelectedId == followed.Id && board.SelectedOrganismId == followed.Id && board.SelectionIsHistorical);
        string inspector = Control<RichTextLabel>("InspectorText").GetParsedText();
        Check("dead selection inspector labels the retained life", inspector.StartsWith($"ORGANISM #{followed.Id}") &&
            inspector.Contains("died", StringComparison.OrdinalIgnoreCase));
        Check("replacement actions do not impersonate the deceased focused life", board.ActionCueCount == 0);
        main.SelectCell(followed.X, followed.Y); await Frames(2);
        Check("explicit selection can follow the replacement life", main.SelectedId == replacementId &&
            board.SelectedOrganismId == replacementId && !board.SelectionIsHistorical && board.ActionCueCount == 1);
        // Restore a coherent selected fixture for the remaining layout/reset checks.
        main.DisplayFrame(frame with { RunId = 141 }, true); board.SetDetailLevel(3);
    }

    private async Task DepthMinimapChecks()
    {
        Main main = viewer!; var board = main.DepthView;
        main.SetViewMode(0); board.SetDetailLevel(3); board.CenterOn(65, 65); await Frames(3);
        ViewFrame snapshot = main.DisplayedFrame!;
        long? selection = main.SelectedId;
        // A point well inside the map's upper-left quadrant avoids dependence on exact decorative padding.
        Vector2 local = main.Minimap.Size * .25f;
        Vector2 click = main.Minimap.GlobalPosition + local;
        if (gpu)
        {
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = click, GlobalPosition = click });
            await Frames(1);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = click, GlobalPosition = click });
        }
        else main.Minimap._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = local });
        await Frames(3);
        Vector2 center = board.VisibleArea.GetCenter();
        Check(gpu ? "Godot minimap input navigates the depth camera" : "headless minimap handler navigates the depth camera",
            center.X < 35 && center.Y < 35 && board.DetailLevel == 3, "center=" + center);
        Check("minimap navigation preserves selected life and completed frame", main.SelectedId == selection &&
            ReferenceEquals(snapshot, main.DisplayedFrame) && main.Minimap.ViewArea == board.VisibleArea);
        board.CenterOn(50, 50);
    }

    private async Task DepthPixelChecks()
    {
        Main main = viewer!;
        main.SetViewMode(1); main.DisplayFrame(Step4Fixtures.Appearance(), true);
        var board = main.DepthView;
        board.SelectedCell = null; board.SelectedOrganismId = null;
        board.ShowTrail = board.ShowMarkers = false; board.Layer = BoardLayer.Food;
        board.SetDetailLevel(4); await Frames(5);
        Color dry, lush, plain;
        using (var image = board.BoardViewport.GetTexture().GetImage())
        {
            dry = AveragePatch(image, board.CellCenter(48, 50), 4);
            lush = AveragePatch(image, board.CellCenter(51, 50), 4);
            plain = AveragePatch(image, board.CellCenter(50, 50), 4);
            Check("depth empty-food terrain is brown", dry.R > dry.G && dry.G > dry.B,
                $"rgb={dry.R:0.000},{dry.G:0.000},{dry.B:0.000}");
            Check("depth plentiful-food terrain is green", lush.G > lush.R && lush.G > lush.B,
                $"rgb={lush.R:0.000},{lush.G:0.000},{lush.B:0.000}");
            Check("depth food quantities are visibly distinct", ColorDistance(dry, lush) > .08f);
        }
        board.Layer = BoardLayer.Combined; await Frames(4);
        using (var image = board.BoardViewport.GetTexture().GetImage())
        {
            Color organism = AveragePatch(image, board.CellCenter(50, 50), 4);
            Color darkDna = AveragePatch(image, board.CellCenter(52, 50), 4);
            Check("mould body separates from its food background", ColorDistance(organism, plain) > .10f,
                $"organism={organism}; food={plain}");
            Check("black DNA has a visible organism treatment", Math.Max(darkDna.R, Math.Max(darkDna.G, darkDna.B)) > .15f,
                darkDna.ToString());
        }
        Vector2 click = board.GlobalPosition + board.CellCenter(48, 50);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = click, GlobalPosition = click });
        await Frames(1);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = click, GlobalPosition = click });
        await Frames(2);
        Check("depth GPU input picks the visible empty cell", Control<RichTextLabel>("InspectorText").GetParsedText().Contains("48, 50"));
    }

    private Color AveragePatch(Image image, Vector2 center, int radius)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y) || center.X - radius < 0 || center.Y - radius < 0 ||
            center.X + radius >= image.GetWidth() || center.Y + radius >= image.GetHeight())
            throw new InvalidOperationException($"Appearance sample {center} radius {radius} is outside {image.GetWidth()}x{image.GetHeight()}.");
        Color sum = new(0, 0, 0, 0); int count = 0;
        for (int y = (int)center.Y - radius; y <= (int)center.Y + radius; y++)
        for (int x = (int)center.X - radius; x <= (int)center.X + radius; x++) { sum += image.GetPixel(x, y); count++; }
        return sum / count;
    }

    private static float ColorDistance(Color first, Color second) =>
        new Vector3(first.R - second.R, first.G - second.G, first.B - second.B).Length();

    private async Task DepthPreviews()
    {
        if (!gpu || !outputValidated) return;
        Main main = viewer!;
        GetTree().Root.Size = new Vector2I(1920, 1080);
        main.SetViewMode(0);
        var board = main.DepthView;
        board.Layer = BoardLayer.Combined; board.ShowMarkers = true; board.ShowTrail = false;
        var fixture = Step4Fixtures.Create(1800, run: 300, variedActions: true);
        main.DisplayFrame(fixture, true); await Frames(4);
        foreach (var preset in new[] { (2, "habitat"), (3, "organism"), (4, "inspect") })
        {
            board.FocusSelection(); board.SetDetailLevel(preset.Item1);
            int season = fixture.Season + preset.Item1;
            var actions = StampActions(fixture.SeasonActions, season);
            var selected = fixture.Selected with { Trace = Array.AsReadOnly(new[] { actions.First(item => item.OrganismId == fixture.Selected.Id).Action }) };
            main.DisplayFrame(fixture with { Season = season, SeasonActions = actions, Selected = selected }, true);
            await Frames(4);
            Check("depth preview uses requested detail " + preset.Item2, board.DetailLevel == preset.Item1);
            using var image = GetViewport().GetTexture().GetImage();
            string path = Path.Combine(Path.GetDirectoryName(output)!, $"r03_step4_{preset.Item2}.png");
            Check("depth current preview " + preset.Item2, image.SavePng(path) == Error.Ok);
        }
    }

    private async Task DepthLiveChecks()
    {
        viewer!.QueueFree(); await Frames(2);
        viewer = NewViewer(false, 10); viewer.SetViewMode(0);
        await WaitFor(() => viewer.DisplayedFrame is { Season: 0, Status: "Paused" }, "depth initial paused worker");
        Main main = viewer;
        string state = SimulationSnapshot.Capture().Sha256, rng = DnaCommon.RandomSource.State;
        long draws = DnaCommon.RandomSource.DrawCount;
        long run = main.DisplayedFrame!.RunId;
        var organism = main.DisplayedFrame.Cells.Where(cell => cell.OrganismId.HasValue)
            .OrderBy(cell => Math.Abs(cell.X - 50) + Math.Abs(cell.Y - 50)).First();
        main.SelectCell(organism.X, organism.Y);
        await WaitFor(() => main.DisplayedFrame!.Selected?.Id == organism.OrganismId, "depth live organism selection");
        Check("depth inspection selects a real organism without a simulation step", main.DisplayedFrame!.Season == 0 &&
            main.SelectedId == organism.OrganismId && main.DisplayedFrame.Population > 0);
        foreach (int mode in new[] { 0, 1, 2, 0 })
        {
            main.SetViewMode(mode);
            main.DepthView.SetDetailLevel(4); main.DepthView.PanBy(new Vector2(38, -17));
            main.DepthView.Layer = BoardLayer.Food; main.DepthView.ShowMarkers = true;
            main.DisplayFrame(main.DisplayedFrame!, true); await Frames(2);
        }
        Check("depth camera layers zoom pan and view switching preserve paused state and RNG",
            state == SimulationSnapshot.Capture().Sha256 && rng == DnaCommon.RandomSource.State && draws == DnaCommon.RandomSource.DrawCount);
        Check("depth renderer switches keep the same simulation run", main.HasSimulation && main.DisplayedFrame!.RunId == run);
        main.StepButton.EmitSignal(Button.SignalName.Pressed);
        await WaitFor(() => main.DisplayedFrame is { Season: 1, Status: "Paused" }, "depth single-season settlement");
        Check("depth view remains attached after worker step", main.DisplayedFrame!.Season == 1 &&
            ReferenceEquals(main.DepthView.Frame, main.DisplayedFrame));
        Check("depth live step supplies a complete current-season action batch", main.DisplayedFrame.SeasonActions.Count > 64 &&
            main.DisplayedFrame.SeasonActions.All(item => item.Action.Season == main.DisplayedFrame.Season));
        await main.StopSimulationAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Check("depth live worker shuts down", !main.HasSimulation);
    }

    private async Task DepthBenchmark()
    {
        if (!gpu) throw new InvalidOperationException("Depth renderer benchmark requires an actual GPU window.");
        GetTree().Root.Size = new Vector2I(1920, 1080);
        viewer!.SetViewMode(0); await Frames(4);
        const double warmupSeconds = 3, sampleSeconds = 7, snapshotHz = 10;
        const long firstSampleSlot = 30, lastReplaySlot = 99;
        const int expectedSampleSnapshots = 70;
        static int CountSampleSlots(long first, long last) =>
            (int)Math.Max(0, Math.Min(last, lastReplaySlot) - Math.Max(first, firstSampleSlot) + 1);
        foreach (var workload in new[] { (0, 1), (1000, 1), (10_000, 1), (10_000, 3) })
        {
            int population = workload.Item1, level = workload.Item2;
            ViewFrame fixture = level == 3 ? Step4Fixtures.Create(population, run: 400 + population + level) :
                ViewerFixtures.Create(population, run: 400 + population + level);
            viewer.DisplayFrame(fixture, true); viewer.DepthView.SetDetailLevel(level); viewer.DepthView.ShowMarkers = true;
            var intervals = new List<double>(700); var uploads = new List<double>(100);
            long started = Stopwatch.GetTimestamp(), previous = started, ended = started, nextReplaySlot = 1;
            long? sampleStarted = null;
            int replayed = 0, skipped = 0;
            GD.Print($"DEPTH_BENCHMARK population={population} detail={level}: warmup3s sample7s replay10Hz");
            while (true)
            {
                await Frames(1);
                long now = Stopwatch.GetTimestamp();
                double elapsed = (now - started) / (double)Stopwatch.Frequency;
                if (elapsed >= warmupSeconds)
                {
                    sampleStarted ??= previous;
                    intervals.Add((now - previous) * 1000d / Stopwatch.Frequency); ended = now;
                }
                previous = now;
                if (elapsed >= warmupSeconds + sampleSeconds)
                {
                    skipped += CountSampleSlots(nextReplaySlot, lastReplaySlot); break;
                }
                long dueSlot = Math.Min((long)Math.Floor(elapsed * snapshotHz), lastReplaySlot);
                if (dueSlot < nextReplaySlot) continue;
                skipped += CountSampleSlots(nextReplaySlot, dueSlot - 1);
                int season = fixture.Season + (int)dueSlot;
                viewer.DisplayFrame(fixture with { Season = season,
                    SeasonActions = level == 3 ? StampActions(fixture.SeasonActions, season) : Array.Empty<ViewObservedAction>() }, false);
                if (dueSlot >= firstSampleSlot) { replayed++; uploads.Add(viewer.DepthView.LastUploadMilliseconds); }
                nextReplaySlot = dueSlot + 1;
            }
            intervals.Sort(); uploads.Sort();
            double actualSampleSeconds = sampleStarted.HasValue ? (ended - sampleStarted.Value) / (double)Stopwatch.Frequency : 0;
            double? p95 = intervals.Count > 0 ? intervals[(int)Math.Ceiling(intervals.Count * .95) - 1] : null;
            bool frameBudgetPassed = p95.HasValue && p95 <= 33.3;
            bool sampleCoverageComplete = intervals.Count > 0 && actualSampleSeconds >= sampleSeconds;
            bool replayCoverageComplete = replayed == expectedSampleSnapshots && skipped == 0;
            var result = new { kind = "depth_renderer", population, detailLevel = level, angled = true,
                width = GetTree().Root.Size.X, height = GetTree().Root.Size.Y,
                visibleFoodInstances = viewer.DepthView.FoodInstanceCount, visibleOrganismInstances = viewer.DepthView.OrganismInstanceCount,
                visibleActionCues = viewer.DepthView.ActionCueCount, requestedWarmupSeconds = warmupSeconds,
                requestedSampleSeconds = sampleSeconds, actualSampleSeconds, requestedSnapshotHz = snapshotHz,
                expectedSampleSnapshots, sampleSnapshotsReplayed = replayed, sampleSnapshotsSkipped = skipped,
                frames = intervals.Count, averageFps = actualSampleSeconds > 0 ? intervals.Count / actualSampleSeconds : 0,
                p95FrameMilliseconds = p95, maxFrameMilliseconds = intervals.Count > 0 ? intervals[^1] : (double?)null,
                p95UploadMilliseconds = uploads.Count > 0 ? uploads[(int)Math.Ceiling(uploads.Count * .95) - 1] : (double?)null,
                sampleCoverageComplete, replayCoverageComplete, frameBudgetPassed, simulationCalculationIncluded = false,
                notes = "Detached snapshots. Dense detail3 includes full 10000-outcome batches at10Hz; no spatial event cap. Intervals include replay stamping and UI upload, but exclude simulation calculation. Main-loop observations, not hardware presentation timings." };
            measurements.Add(result); GD.Print(JsonSerializer.Serialize(result));
            string label = $"population {population} detail {level}";
            Check("depth benchmark sample coverage " + label, sampleCoverageComplete, $"{actualSampleSeconds:0.000}s", false);
            Check("depth benchmark replay coverage " + label, replayCoverageComplete, $"{replayed}/{expectedSampleSnapshots}; skipped {skipped}", false);
            Check("depth benchmark frame budget " + label, frameBudgetPassed, $"p95 {p95:0.00}ms", false);
        }
    }
}
