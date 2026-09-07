using System.Diagnostics;
using System.Text.Json;
using Fistnet.Genepool.Control;
using Fistnet.Genepool.Control.Gameboard;
using Fistnet.Genepool.Control.Presentation;
using Godot;
using DnaCommon = Fistnet.Genepool.Dna.Common;

namespace Fistnet.Genepool.GodotViewer.Verification;

/// <summary>Explicit opt-in verification scene; never loaded by normal app startup.</summary>
public partial class VerificationRunner : Node
{
    private readonly List<object> checks = new();
    private readonly List<object> measurements = new();
    private Main? viewer;
    private string output = "";
    private bool outputValidated;
    private bool gpu;
    private int failures;

    public override async void _Ready()
    {
        var arguments = OS.GetCmdlineUserArgs();
        string mode = arguments.FirstOrDefault(a => a.StartsWith("--mode="))?[7..] ?? "smoke";
        string root = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), ".."));
        try
        {
            output = Path.GetFullPath(arguments.FirstOrDefault(a => a.StartsWith("--output="))?[9..]
                ?? Path.Combine(root, "KnowledgeBase", "r03_godot_verification.json"));
            string[] allowedReports = { "r03_godot_verification.json", "r03_godot_headless.json", "r03_godot_benchmark.json" };
            if (!string.Equals(Path.GetDirectoryName(output), Path.Combine(root, "KnowledgeBase"), StringComparison.OrdinalIgnoreCase) ||
                !allowedReports.Contains(Path.GetFileName(output), StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException("Verification output must use an approved r03_godot report filename directly inside the project KnowledgeBase.");
            outputValidated = true;
            if (mode is not ("smoke" or "benchmark" or "preview")) throw new ArgumentException("Unknown verification mode.");
            Engine.MaxFps = 60;
            gpu = DisplayServer.GetName() != "headless";
            string adapter = RenderingServer.GetVideoAdapterName();
            if (gpu && (adapter.Contains("llvmpipe", StringComparison.OrdinalIgnoreCase) ||
                adapter.Contains("Software", StringComparison.OrdinalIgnoreCase) || adapter.Contains("Basic Render", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Software rendering cannot establish the GPU checkpoint.");
            viewer = NewViewer(true);
            await Frames(3);
            if (mode == "benchmark") await Benchmark();
            else
            {
                await PassiveChecks();
                if (gpu) await PixelChecks();
                await Previews();
                if (mode == "smoke") await LiveChecks();
            }
        }
        catch (Exception ex)
        {
            failures++;
            checks.Add(new { name = "verification completed", passed = false, error = ex.ToString() });
            GD.PushError(ex.ToString());
        }
        finally
        {
            if (viewer != null && IsInstanceValid(viewer))
            {
                try { await viewer.StopSimulationAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (Exception ex) { failures++; checks.Add(new { name = "shutdown", passed = false, error = ex.Message }); }
            }
            if (outputValidated)
            {
                var report = new { atUtc = DateTimeOffset.UtcNow, mode, ok = failures == 0, failures,
                    backend = DisplayServer.GetName(), adapter = RenderingServer.GetVideoAdapterName(),
                    runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    engine = Engine.GetVersionInfo()["string"].AsString(), gpuExecuted = gpu,
                    externalProcessWatchdogRequired = true,
                    limits = "Synthetic viewer fixtures and bounded engineering checks. No ecological trial. Frame intervals are main-loop observations, not hardware presentation timings. UI timing covers injected Godot input/signals, not physical mouse-to-photon latency. In-scene waits cannot time out a stalled main loop; the launcher must enforce an external process deadline.",
                    checks, measurements };
                File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n");
                GD.Print($"GENEPOOL_VERIFICATION {(failures == 0 ? "PASS" : "FAIL")} {mode}: {output}");
            }
            GetTree().Quit(failures == 0 ? 0 : 1);
        }
    }

    private Main NewViewer(bool passive)
    {
        Main main = GD.Load<PackedScene>("res://Scenes/Main.tscn").Instantiate<Main>();
        main.PassiveMode = passive;
        if (!passive) main.InitialOptions = new SimulationRunOptions
            { Mode = SimulationMode.DeterministicReference, Seed = 29, InitialPopulationPercent = 0 };
        AddChild(main);
        return main;
    }

    private async Task Frames(int count = 2)
    {
        // A stalled main loop never emits this signal; the process launcher owns the hard deadline.
        for (int i = 0; i < count; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task Delay(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private void Check(string name, bool pass, string? detail = null, bool stopOnFailure = true)
    {
        checks.Add(new { name, passed = pass, detail });
        GD.Print($"{(pass ? "PASS" : "FAIL")} {name}");
        if (!pass)
        {
            failures++;
            if (stopOnFailure) throw new InvalidOperationException(name + ": " + detail);
        }
    }

    private T Control<T>(string name) where T : Node => viewer!.FindChild(name, true, false) as T
        ?? throw new InvalidOperationException("Missing named control " + name);

    private async Task WaitFor(Func<bool> condition, string description, double timeoutSeconds = 8)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (viewer?.DisplayedFrame?.Fault is string fault) throw new InvalidOperationException(fault);
            if (timer.Elapsed.TotalSeconds > timeoutSeconds) throw new TimeoutException(description);
            await Frames(1);
        }
    }

    private async Task PassiveChecks()
    {
        Main main = viewer!;
        Check("passive viewer owns no simulation", !main.HasSimulation);
        var fixture = ViewerFixtures.Create(1000);
        main.DisplayFrame(fixture, true);
        await Frames(3);
        var board = main.BoardView;
        Check("two populated instance buffers", board.FoodInstanceCount == 10_000 && board.OrganismInstanceCount == 1000);
        foreach (var cell in new[] { (0, 0), (50, 50), (99, 99) })
            Check($"fitted picking {cell}", board.CellAt(board.CellCenter(cell.Item1, cell.Item2)) == cell);
        Check("outside picking rejected", board.CellAt(new Vector2(-1, 20)) == null);
        board.ZoomAt(2, board.CellCenter(50, 50));
        board.PanBy(new Vector2(20, -15));
        Check("zoomed and panned picking", board.CellAt(board.CellCenter(50, 50)) == (50, 50));
        board.Fit();
        Check("inspector explains DNA and actual actions", Control<RichTextLabel>("InspectorText").GetParsedText().Contains("COMPLETED ACTIONS"));
        board.ShowMarkers = true;
        main.DisplayFrame(fixture with { Season = 65 }, true);
        Check("fresh season markers visible", board.MarkersVisible);
        await Delay(.6);
        main.DisplayFrame(fixture with { Season = 65, AcknowledgedCommand = 1 }, true);
        Check("acknowledgment cannot revive expired markers", !board.MarkersVisible);
        var resetSelection = fixture.Cells.First(cell => cell.OrganismId.HasValue);
        board.HighlightedPattern = resetSelection.PatternKey;
        board.SelectedOrganismId = resetSelection.OrganismId;
        Check("reset begins with active highlight and selection", board.HighlightedPattern != null && board.SelectedOrganismId != null);
        main.DisplayFrame(fixture with { RunId = 2, Selected = null!, Season = 0, History = Array.Empty<ViewHistory>() }, true);
        Check("reset clears highlight and selection", board.HighlightedPattern == null && board.SelectedOrganismId == null);
    }

    private async Task PixelChecks()
    {
        Main main = viewer!;
        GetTree().Root.Size = new Vector2I(1920, 1080);
        main.DisplayFrame(ViewerFixtures.ColorsFixture() with { RunId = 3 }, true);
        await Frames(4);
        var board = main.BoardView;
        board.ShowTrail = board.ShowMarkers = false;
        board.SelectedCell = null; board.SelectedOrganismId = null; board.HighlightedPattern = null;
        board.Layer = BoardLayer.Food;
        await Frames(3);
        using (var image = board.BoardViewport.GetTexture().GetImage())
        {
            Pixel("GPU food zero", image, board.CellCenter(0, 0), 17, 28, 37);
            Pixel("GPU food under organism", image, board.CellCenter(3, 4), 46, 76, 66);
            Pixel("GPU food maximum and axes", image, board.CellCenter(4, 3), 116, 188, 136);
        }
        board.Layer = BoardLayer.Organisms;
        await Frames(3);
        using (var image = board.BoardViewport.GetTexture().GetImage())
        {
            Pixel("GPU black DNA stays visible", image, board.CellCenter(3, 4), 95, 112, 133);
            Pixel("GPU organism color", image, board.CellCenter(5, 4), 255, 96, 96);
        }
        board.HighlightedPattern = "black";
        await Frames(3);
        using (var image = board.BoardViewport.GetTexture().GetImage())
            Pixel("GPU other patterns dim rather than disappear", image, board.CellCenter(5, 4), 79, 40, 44);
        board.HighlightedPattern = null; board.Layer = BoardLayer.Combined;
        await Frames(3);
        using (var image = board.BoardViewport.GetTexture().GetImage())
        {
            Vector2 center = board.CellCenter(3, 4);
            float side = board.CellCenter(4, 4).X - center.X;
            Pixel("GPU combined organism center", image, center, 95, 112, 133);
            Pixel("GPU combined food surround", image, center + new Vector2(-.38f * side, -.38f * side), 46, 76, 66);
        }
        // Exercise Godot's input route, including the viewport/container coordinate transforms.
        Vector2 click = board.GlobalPosition + board.CellCenter(0, 0);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = click, GlobalPosition = click });
        await Frames(1);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = click, GlobalPosition = click });
        await Frames(2);
        Check("Godot input selects the displayed empty cell", Control<RichTextLabel>("InspectorText").GetParsedText().Contains("0, 0"));
    }

    private void Pixel(string name, Image image, Vector2 point, int r, int g, int b)
    {
        Check(name + " sample lies inside image", float.IsFinite(point.X) && float.IsFinite(point.Y) &&
            point.X >= 0 && point.Y >= 0 && point.X < image.GetWidth() && point.Y < image.GetHeight(),
            $"point={point}; image={image.GetWidth()}x{image.GetHeight()}");
        int x = (int)point.X, y = (int)point.Y;
        Color color = image.GetPixel(x, y);
        Check(name, Math.Abs(color.R8 - r) <= 2 && Math.Abs(color.G8 - g) <= 2 && Math.Abs(color.B8 - b) <= 2,
            $"pixel({x},{y})={color.R8},{color.G8},{color.B8}; expected {r},{g},{b}, tolerance2");
    }

    private async Task Previews()
    {
        if (!gpu) return;
        if (!outputValidated) throw new InvalidOperationException("Preview output has not been validated.");
        string directory = Path.GetDirectoryName(output)!;
        Main main = viewer!;
        foreach (var size in new[] { new Vector2I(1920, 1080), new Vector2I(1366, 768) })
        {
            GetTree().Root.Size = size;
            main.DisplayFrame(ViewerFixtures.Create(1000, run: 10), true);
            main.BoardView.ShowMarkers = false;
            main.BoardView.ShowTrail = true;
            main.BoardView.Fit();
            await Frames(5);
            using var image = GetViewport().GetTexture().GetImage();
            Check("capture dimensions " + size, image.GetWidth() == size.X && image.GetHeight() == size.Y,
                $"actual={image.GetWidth()}x{image.GetHeight()}");
            string path = Path.Combine(directory, $"r03-godot-{size.X}x{size.Y}.png");
            Check("capture " + size, image.SavePng(path) == Error.Ok);
            Check("board and details do not overlap " + size,
                main.BoardView.GetGlobalRect().End.X <= Control<TabContainer>("DetailTabs").GetGlobalRect().Position.X + 1);
            Check("toolbar fits " + size, Control<OptionButton>("SpeedBox").GetGlobalRect().End.X <= size.X);
        }
    }

    private async Task LiveChecks()
    {
        viewer!.QueueFree(); await Frames(2);
        viewer = NewViewer(false);
        await WaitFor(() => viewer.DisplayedFrame is { Season: 0, Status: "Paused" }, "initial paused frame");
        Check("live viewer owns one worker", viewer.HasSimulation);
        Check("initial speed matches UI", viewer.DisplayedFrame!.TargetSeasonsPerSecond == 15);
        string initial = SimulationSnapshot.Capture().Sha256;
        string random = DnaCommon.RandomSource.State;
        long draws = DnaCommon.RandomSource.DrawCount;
        for (int i = 0; i < 4; i++)
        {
            viewer.BoardView.ZoomBy(1.2f);
            viewer.BoardView.Layer = (BoardLayer)(i % 3);
            viewer.DisplayFrame(viewer.DisplayedFrame!, true);
            await Frames(2);
        }
        Check("actual Godot observation preserves full paused state and RNG",
            initial == SimulationSnapshot.Capture().Sha256 && random == DnaCommon.RandomSource.State && draws == DnaCommon.RandomSource.DrawCount);
        Control<Button>("StepButton").EmitSignal(Button.SignalName.Pressed);
        await WaitFor(() => viewer.DisplayedFrame is { Season: 1, Status: "Paused" }, "single season step");
        Check("single step settles exactly one season", viewer.DisplayedFrame!.Season == 1);
        Control<Button>("CycleButton").EmitSignal(Button.SignalName.Pressed);
        await WaitFor(() => viewer.DisplayedFrame is { Season: 9, Status: "Paused" }, "eight season step");
        Check("cycle settles exactly eight seasons", viewer.DisplayedFrame!.Season == 9);
        long oldRun = viewer.DisplayedFrame.RunId;
        Control<Button>("RestartButton").EmitSignal(Button.SignalName.Pressed);
        await WaitFor(() => viewer.DisplayedFrame is { Season: 0, Status: "Paused" } f && f.RunId != oldRun, "restart");
        Check("restart preserves configured seed and state", viewer.DisplayedFrame!.Options.Seed == 29 && SimulationSnapshot.Capture().Sha256 == initial);

        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        void BlockSeason(int season, int population) { entered.Set(); if (!release.Wait(5000)) throw new TimeoutException("Controlled season block was not released."); }
        Board.SeasonCompleted += BlockSeason;
        try
        {
            Control<Button>("RunButton").EmitSignal(Button.SignalName.Pressed);
            await WaitFor(() => entered.IsSet, "controlled calculation boundary");
            var clock = Stopwatch.StartNew();
            Control<Button>("PauseButton").EmitSignal(Button.SignalName.Pressed);
            double acknowledgeMilliseconds = clock.Elapsed.TotalMilliseconds;
            float before = viewer.BoardView.Zoom;
            viewer.BoardView.ZoomBy(1.5f);
            await Frames(3);
            Check("camera remains responsive during blocked simulation", viewer.BoardView.Zoom > before && !release.IsSet);
            Check("pause handler acknowledges without waiting for simulation", acknowledgeMilliseconds <= 100, acknowledgeMilliseconds.ToString("0.00") + " ms");
            measurements.Add(new { kind = "input_handler", input = "Pause signal during blocked season", acknowledgeMilliseconds, excludes = "OS input delivery and physical display latency" });
        }
        finally { release.Set(); Board.SeasonCompleted -= BlockSeason; }
        await WaitFor(() => viewer.DisplayedFrame is { Status: "Paused", Season: 1 }, "boundary pause settlement");
        Check("pause settles at completed boundary", viewer.DisplayedFrame!.Season == 1);
        await SettingsChecks();
        await PopulatedLiveChecks();
        await viewer.StopSimulationAsync().WaitAsync(TimeSpan.FromSeconds(10));
        Check("shutdown completes", !viewer.HasSimulation);
    }

    private async Task SettingsChecks()
    {
        Main main = viewer!;
        long run = main.DisplayedFrame!.RunId;
        string state = SimulationSnapshot.Capture().Sha256;
        string rng = DnaCommon.RandomSource.State;
        main.SettingsButton.EmitSignal(Button.SignalName.Pressed);
        await Frames(3);
        var settings = main.Settings;
        Check("new simulation button opens random-seed settings", settings.Visible && settings.RandomSeed.ButtonPressed && !settings.SeedValue.Editable);
        settings.RandomSeed.ButtonPressed = false;
        settings.Density.Value = 0;
        settings.CellFood.Value = 7;
        settings.Regrowth.Value = 2;
        settings.Reserves.Value = 8;
        await Frames(2); // SpinBox refreshes programmatic values during redraw.
        settings.SeedValue.GetLineEdit().Text = "-";
        settings.SeedValue.GetLineEdit().EmitSignal(LineEdit.SignalName.TextChanged, "-");
        await Frames(2);
        Check("signed seed typing preserves an unfinished minus", settings.SeedValue.GetLineEdit().Text == "-");
        settings.SeedValue.GetLineEdit().Text = "-123";
        var edited = settings.ReadOptions(999);
        Check("typed signed seed and all numeric settings are read", edited.Seed == -123 && edited.InitialPopulationPercent == 0 && edited.InitialCellFood == 7 && edited.FoodRegrowthPerAge == 2 && edited.InitialOrganismFood == 8,
            $"seed={edited.Seed}; density={edited.InitialPopulationPercent}; food={edited.InitialCellFood}; regrowth={edited.FoodRegrowthPerAge}; reserves={edited.InitialOrganismFood}; seedText={settings.SeedValue.GetLineEdit().Text}");
        Check("settings retain hidden execution and policy values", edited.Mode == main.DisplayedFrame.Options.Mode && edited.CellExecution == main.DisplayedFrame.Options.CellExecution && edited.Policy.ExternalContextLimit == main.DisplayedFrame.Options.Policy.ExternalContextLimit);
        settings.Preset.Select(6);
        settings.Preset.EmitSignal(OptionButton.SignalName.ItemSelected, 6L);
        Check("preset event resets fields and restores random choice", settings.Regrowth.Value == 2 && settings.Density.Value == 10 && settings.RandomSeed.ButtonPressed);
        await Frames(4);
        if (gpu && outputValidated)
        {
            using var image = settings.GetTexture().GetImage();
            Check("settings preview captured", image.SavePng(Path.Combine(Path.GetDirectoryName(output)!, "r03-godot-settings.png")) == Error.Ok);
        }
        settings.FindChild("CancelButton", true, false).EmitSignal(Button.SignalName.Pressed);
        await Frames(2);
        Check("cancel leaves world and RNG unchanged", !settings.Visible && state == SimulationSnapshot.Capture().Sha256 && rng == DnaCommon.RandomSource.State);
        main.SettingsButton.EmitSignal(Button.SignalName.Pressed);
        settings.RandomSeed.ButtonPressed = false;
        settings.Density.Value = 0; settings.CellFood.Value = 7; settings.Regrowth.Value = 2; settings.Reserves.Value = 8;
        settings.FounderDna.Select(1);
        await Frames(2);
        settings.SeedValue.GetLineEdit().Text = "-123";
        settings.CreateButton.EmitSignal(Button.SignalName.Pressed);
        await WaitFor(() => main.DisplayedFrame is { Season: 0, Status: "Paused" } frame && frame.RunId != run, "new world settings settlement");
        var options = main.DisplayedFrame!.Options;
        Check("create applies settings at a new paused run", !settings.Visible && options.Seed == -123 && options.InitialPopulationPercent == 0 && options.InitialCellFood == 7 && options.FoodRegrowthPerAge == 2 && options.InitialOrganismFood == 8 && options.FounderRepertoire == Fistnet.Genepool.Dna.FounderRepertoire.GatherAndReproduce);
        main.SpeedBox.Select(4);
        main.SpeedBox.EmitSignal(OptionButton.SignalName.ItemSelected, 4L);
        await WaitFor(() => main.DisplayedFrame!.TargetSeasonsPerSecond == 60, "speed selection");
        Check("speed menu controls worker target", main.DisplayedFrame.TargetSeasonsPerSecond == 60);
        Check("history rates use actual sampled season gaps", HistoryView.IntervalRate(24, 4, 12, 2) == 2 && HistoryView.IntervalRate(2, 9, 12, 2) == 0);
    }

    private async Task PopulatedLiveChecks()
    {
        Main main = viewer!;
        long run = main.DisplayedFrame!.RunId;
        main.SettingsButton.EmitSignal(Button.SignalName.Pressed);
        main.Settings.ApplyPreset(1);
        main.Settings.RandomSeed.ButtonPressed = false;
        await Frames(2);
        main.Settings.SeedValue.GetLineEdit().Text = "29";
        main.Settings.CreateButton.EmitSignal(Button.SignalName.Pressed);
        await WaitFor(() => main.DisplayedFrame is { Season: 0, Status: "Paused" } f && f.RunId != run, "populated world startup");
        Check("Godot initializes existing random founder setup", main.DisplayedFrame!.Population > 0 && main.DisplayedFrame.Options.InitialPopulationPercent == 10);
        GetTree().Root.Size = new Vector2I(1440, 900);
        await Frames(3);
        var cell = main.DisplayedFrame.Cells.Where(c => c.OrganismId.HasValue)
            .OrderBy(c => Math.Abs(c.X - 50) + Math.Abs(c.Y - 50)).First();
        var board = main.BoardView;
        board.Fit(); board.ZoomAt(2, board.CellCenter(cell.X, cell.Y)); board.PanBy(new Vector2(20, -15));
        await Frames(2);
        if (gpu)
        {
            Vector2 click = board.GlobalPosition + board.CellCenter(cell.X, cell.Y);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = click, GlobalPosition = click });
            await Frames(1);
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = click, GlobalPosition = click });
        }
        else main.SelectCell(cell.X, cell.Y);
        await WaitFor(() => main.DisplayedFrame!.Selected?.Id == cell.OrganismId, "live same-season selection");
        Check("transformed selection follows a real organism without stepping", main.SelectedId == cell.OrganismId && main.DisplayedFrame!.Season == 0);
        main.CycleButton.EmitSignal(Button.SignalName.Pressed);
        await WaitFor(() => main.DisplayedFrame is { Season: 8, Status: "Paused" }, "populated eight-season cycle", 25);
        main.DisplayFrame(main.DisplayedFrame!, true);
        Check("real DNA and completed actions reach the inspector", main.DisplayedFrame!.Selected is { } selected && selected.Id == cell.OrganismId && selected.Genes.Count == 8 && selected.Trace.Count > 0);
        Check("populated world retains bounded detached history", main.DisplayedFrame.History.Count > 0 && main.DisplayedFrame.History.Count <= 240);
        measurements.Add(new { kind = "bounded_live_world", seed = 29, initialPopulationPercent = 10, completedSeasons = 8, endingPopulation = main.DisplayedFrame.Population, selectedId = cell.OrganismId, mode = "DeterministicReference", ecologicalEvaluation = false });
        board.Fit(); board.Layer = BoardLayer.Combined;
        await Frames(4);
        if (gpu && outputValidated)
        {
            using var image = GetViewport().GetTexture().GetImage();
            Check("live world preview captured", image.SavePng(Path.Combine(Path.GetDirectoryName(output)!, "r03-godot-live.png")) == Error.Ok);
        }
    }

    private async Task Benchmark()
    {
        if (!gpu) throw new InvalidOperationException("Renderer benchmark requires an actual GPU window.");
        GetTree().Root.Size = new Vector2I(1920, 1080);
        await Frames(4);
        const double warmupSeconds = 5, sampleSeconds = 10, snapshotHz = 10;
        const long firstSampleSlot = 50, lastReplaySlot = 149;
        const int expectedSampleSnapshots = 100;
        static int CountSampleSlots(long first, long last) =>
            (int)Math.Max(0, Math.Min(last, lastReplaySlot) - Math.Max(first, firstSampleSlot) + 1);
        foreach (int population in new[] { 0, 1000, 10_000 })
        {
            var fixture = ViewerFixtures.Create(population, run: population + 30);
            viewer!.DisplayFrame(fixture, true); viewer.BoardView.Fit();
            var frames = new List<double>(1000);
            var uploads = new List<double>(200);
            long started = Stopwatch.GetTimestamp(), previous = started;
            long? sampleStarted = null;
            long sampleEnded = started, nextReplaySlot = 1;
            int replayed = 0, skipped = 0, sampleReplayed = 0, sampleSkipped = 0;
            GD.Print($"BENCHMARK population={population}: warmup5s sample10s, replay10Hz");
            while (true)
            {
                await Frames(1);
                long now = Stopwatch.GetTimestamp();
                double elapsed = (now - started) / (double)Stopwatch.Frequency;
                if (elapsed >= warmupSeconds)
                {
                    sampleStarted ??= previous;
                    frames.Add((now - previous) * 1000d / Stopwatch.Frequency);
                    sampleEnded = now;
                }
                previous = now;
                // Keep the entire boundary-crossing interval, including any final stall.
                if (elapsed >= warmupSeconds + sampleSeconds)
                {
                    skipped += (int)Math.Max(0, lastReplaySlot - nextReplaySlot + 1);
                    sampleSkipped += CountSampleSlots(nextReplaySlot, lastReplaySlot);
                    break;
                }
                // Replay the latest due snapshot once; never burst through a late backlog.
                long dueSlot = Math.Min((long)Math.Floor(elapsed * snapshotHz), lastReplaySlot);
                if (dueSlot >= nextReplaySlot)
                {
                    skipped += (int)(dueSlot - nextReplaySlot);
                    sampleSkipped += CountSampleSlots(nextReplaySlot, dueSlot - 1);
                    viewer.DisplayFrame(fixture with { Season = fixture.Season + (int)dueSlot }, false);
                    replayed++;
                    if (dueSlot >= firstSampleSlot)
                    {
                        sampleReplayed++;
                        uploads.Add(viewer.BoardView.LastUploadMilliseconds);
                    }
                    nextReplaySlot = dueSlot + 1;
                }
            }
            frames.Sort(); uploads.Sort();
            double actualSampleSeconds = sampleStarted.HasValue ? (sampleEnded - sampleStarted.Value) / (double)Stopwatch.Frequency : 0;
            double? p95 = frames.Count == 0 ? null : frames[(int)Math.Ceiling(frames.Count * .95) - 1];
            bool frameBudgetPassed = p95 is double interval && interval <= 33.3;
            bool sampleCoverageComplete = frames.Count > 0 && actualSampleSeconds >= sampleSeconds;
            bool replayCoverageComplete = sampleReplayed == expectedSampleSnapshots && sampleSkipped == 0;
            var result = new { population, width = GetTree().Root.Size.X, height = GetTree().Root.Size.Y,
                requestedWarmupSeconds = warmupSeconds, requestedSampleSeconds = sampleSeconds,
                actualSampleSeconds,
                sampleIntervalStartSeconds = sampleStarted.HasValue ? (sampleStarted.Value - started) / (double)Stopwatch.Frequency : (double?)null,
                sampleIntervalEndSeconds = (sampleEnded - started) / (double)Stopwatch.Frequency,
                requestedSnapshotHz = snapshotHz, sampleSnapshotsReplayed = sampleReplayed,
                sampleSnapshotsSkipped = sampleSkipped, expectedSampleSnapshots,
                actualReplayHzDuringRequestedSample = sampleReplayed / sampleSeconds,
                totalSnapshotsReplayed = replayed, totalSnapshotsSkipped = skipped,
                frames = frames.Count, averageFps = actualSampleSeconds > 0 ? frames.Count / actualSampleSeconds : 0,
                p95FrameMilliseconds = p95, maxFrameMilliseconds = frames.Count == 0 ? (double?)null : frames[^1],
                p95UploadMilliseconds = uploads.Count == 0 ? (double?)null : uploads[(int)Math.Ceiling(uploads.Count * .95) - 1],
                sampleCoverageComplete, replayCoverageComplete, frameBudgetPassed, simulationCalculationIncluded = false };
            measurements.Add(result); GD.Print(JsonSerializer.Serialize(result));
            Check("renderer sample coverage at population " + population, sampleCoverageComplete,
                actualSampleSeconds.ToString("0.000") + " s across " + frames.Count + " frame intervals", stopOnFailure: false);
            Check("renderer replay coverage at population " + population, replayCoverageComplete,
                $"{sampleReplayed}/{expectedSampleSnapshots} sample snapshots; {sampleSkipped} skipped", stopOnFailure: false);
            Check("renderer frame budget at population " + population, frameBudgetPassed,
                p95?.ToString("0.00") + " ms p95", stopOnFailure: false);
        }
    }
}
