using System.Diagnostics;
using Fistnet.Genepool.Control;
using Godot;

namespace Fistnet.Genepool.GodotViewer;

public partial class Main : Godot.Control
{
    public bool PassiveMode { get; set; }
    public SimulationRunOptions? InitialOptions { get; set; }
    public bool HasSimulation => simulation != null;
    public ViewFrame? DisplayedFrame { get; private set; }
    public long PendingCommand => pendingCommand;
    public long SelectionCommand => selectionCommand;
    public long? SelectedId => selectedId;
    public bool IsClosing => closing;
    public double LastUiUpdateMilliseconds { get; private set; }
    public string? LastSettlement => lastSettlement;
    private SimulationRunner? simulation;
    private SimulationRunOptions? activeOptions;
    private ViewFrame? detailedFrame;
    private long pendingCommand, selectionCommand, lastDetails, settlementStarted;
    private string? pendingText, settlementRequest, lastSettlement, patternKey;
    private long? selectedId;
    private (int X, int Y)? selectedCell;
    private bool closing, unavailable, built;
    private Task? stopTask;
    private Window? hostWindow;
    private bool? historyPreference;

    public override void _Ready()
    {
        BuildLayout(); Resized += AdaptHistory; AdaptHistory();
        if (PassiveMode) { RefreshControls(); return; }
        hostWindow = GetWindow(); GetTree().AutoAcceptQuit = false;
        hostWindow.CloseRequested += OnCloseRequested;
        _ = StartSimulationAsync();
    }

    private async Task StartSimulationAsync()
    {
        try
        {
            activeOptions = InitialOptions ?? new SimulationRunOptions();
            simulation = new SimulationRunner(activeOptions); simulation.SetSpeed(15);
            await simulation.Ready;
            if (!closing && IsInsideTree() && simulation.LatestFrame is { } frame) DisplayFrame(frame, true);
        }
        catch (Exception exception)
        {
            if (closing || !IsInsideTree()) return;
            unavailable = true; PlaybackLabel.Text = "Could not start · " + exception.GetBaseException().Message;
            PlaybackLabel.Modulate = Error; RefreshControls();
        }
    }

    public override void _Process(double delta)
    {
        if (!built) return;
        ZoomLabel.Text = $"{BoardView.Zoom * 100:0}%";
        if (!closing && simulation?.LatestFrame is { } frame) DisplayFrame(frame);
    }

    public void DisplayFrame(ViewFrame frame, bool forceDetails = false)
    {
        ArgumentNullException.ThrowIfNull(frame); BuildLayout();
        long started = Stopwatch.GetTimestamp();
        bool newRun = DisplayedFrame == null || DisplayedFrame.RunId != frame.RunId;
        bool changed = !ReferenceEquals(DisplayedFrame, frame);
        if (newRun)
        {
            selectedId = null; selectedCell = null; selectionCommand = 0; patternKey = null;
            BoardView.SelectedCell = null; BoardView.SelectedOrganismId = null; BoardView.HighlightedPattern = null;
        }
        DisplayedFrame = frame;
        if (newRun || frame.AcknowledgedCommand >= pendingCommand) activeOptions = frame.Options;
        if (PassiveMode && newRun) selectedId = frame.Selected?.Id;
        if (changed) BoardView.SetFrame(frame);
        BoardView.SelectedOrganismId = selectedId;
        if (newRun) BoardView.Fit();
        if (pendingCommand != 0 && frame.AcknowledgedCommand >= pendingCommand)
        {
            if (settlementRequest != null)
                lastSettlement = $"{settlementRequest} settled in {Stopwatch.GetElapsedTime(settlementStarted).TotalMilliseconds:0} ms";
            pendingCommand = 0; pendingText = settlementRequest = null;
        }
        UpdatePlayback(); RefreshControls();
        if (forceDetails || newRun || (!ReferenceEquals(detailedFrame, frame) && Stopwatch.GetElapsedTime(lastDetails).TotalMilliseconds >= 500))
        {
            StatisticsLabel.Text = $"{frame.Population:N0} living   ·   Season {frame.Season:N0}   ·   Cycle {frame.Age:N0}\nLocal food {frame.LocalFood:N0}   ·   Carried {frame.StoredFood:N0}   ·   Births {frame.Births:N0} / Deaths {frame.Deaths:N0}";
            ConfigurationLabel.Text = Configuration(frame.Options); ConfigurationLabel.TooltipText = ConfigurationLabel.Text;
            UpdateInspector(); UpdatePatterns(); UpdateActivity(); History.SetFrame(frame);
            detailedFrame = frame; lastDetails = Stopwatch.GetTimestamp();
        }
        LastUiUpdateMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
    }

    private void Send(string text, Func<long> command, string? settlement = null)
    {
        if (!TryCommand(command, out long number)) return;
        pendingCommand = number; pendingText = text; settlementRequest = settlement; settlementStarted = Stopwatch.GetTimestamp();
        UpdatePlayback(); RefreshControls();
    }

    private bool TryCommand(Func<long> command, out long number)
    {
        number = 0;
        if (simulation == null || closing || unavailable || DisplayedFrame?.Fault != null || simulation.Ready.IsFaulted) return false;
        try { number = command(); return true; }
        catch (ObjectDisposedException) { unavailable = true; UpdatePlayback(); RefreshControls(); return false; }
        catch (InvalidOperationException exception)
        { PlaybackLabel.Text = exception.Message; PlaybackLabel.Modulate = Warm; return false; }
    }

    private void UpdatePlayback()
    {
        if (closing) { PlaybackLabel.Text = "Closing · finishing the current season"; PlaybackLabel.Modulate = Warm; return; }
        if (unavailable) { PlaybackLabel.Text = "Simulation stopped · reopen to start a world"; PlaybackLabel.Modulate = Error; return; }
        if (DisplayedFrame is not { } frame) return;
        bool stepping = frame.Status == "Stepping";
        PlaybackLabel.Text = frame.Fault != null ? "STOPPED · " + frame.Fault : pendingCommand != 0 ? pendingText :
            (frame.IsRunning ? "RUNNING" : stepping ? "STEPPING" : "PAUSED") + "   ·   " +
            (!frame.IsRunning && !stepping && lastSettlement != null ? lastSettlement : $"{frame.ActualSeasonsPerSecond:0.0} {(frame.IsRunning || stepping ? "actual" : "last")} seasons/s");
        PlaybackLabel.Modulate = frame.Fault != null ? Error : pendingCommand != 0 ? Warm : frame.IsRunning ? Accent : Ink;
    }

    private void RefreshControls()
    {
        if (!built) return;
        var frame = DisplayedFrame;
        bool blocked = PassiveMode || simulation == null || closing || unavailable || frame == null || frame.Fault != null;
        bool pending = pendingCommand != 0, stepping = frame?.Status == "Stepping", running = frame?.IsRunning == true;
        RunButton.Disabled = blocked || running || stepping || pending;
        PauseButton.Disabled = blocked || !(running || stepping || pending);
        StepButton.Disabled = CycleButton.Disabled = blocked || running || stepping || pending;
        RestartButton.Disabled = SettingsButton.Disabled = SpeedBox.Disabled = blocked || pending;
        BoardView.MouseFilter = closing || unavailable || frame?.Fault != null ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
    }

    public void SelectCell(int x, int y)
    {
        var frame = DisplayedFrame;
        if (frame == null || closing || frame.Fault != null || pendingCommand != 0 || x < 0 || y < 0 || x >= frame.Width || y >= frame.Height) return;
        var cell = frame.Cells[y * frame.Width + x]; long command = 0;
        if (!PassiveMode && !TryCommand(() => simulation!.Select(cell.OrganismId), out command)) return;
        selectedCell = (x, y); selectedId = cell.OrganismId; selectionCommand = command;
        BoardView.SelectedCell = selectedCell; BoardView.SelectedOrganismId = selectedId;
        DetailTabs.CurrentTab = 0; InspectorText.GetVScrollBar().Value = 0; UpdateInspector();
    }

    private void OpenSettings()
    {
        if (SettingsButton.Disabled || activeOptions == null) return;
        Settings.Open(activeOptions);
    }

    public Task StopSimulationAsync()
    {
        if (stopTask != null) return stopTask;
        closing = true; UpdatePlayback(); RefreshControls();
        stopTask = StopWorkerAsync(); return stopTask;
    }

    private async Task StopWorkerAsync()
    {
        var worker = simulation;
        if (worker != null) { await worker.StopAsync(); worker.Dispose(); simulation = null; }
    }

    private async void OnCloseRequested()
    {
        await StopSimulationAsync();
        if (IsInsideTree()) GetTree().Quit();
    }

    public override void _ExitTree()
    {
        closing = true;
        if (hostWindow != null && GodotObject.IsInstanceValid(hostWindow)) hostWindow.CloseRequested -= OnCloseRequested;
        if (simulation != null) _ = simulation.StopAsync();
    }
}
