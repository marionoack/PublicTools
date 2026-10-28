using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using SqlTransfer.Infrastructure;
using SqlTransfer.Models;
using SqlTransfer.Services;

namespace SqlTransfer.ViewModels;

public enum ResultState { None, Success, Failed, Canceled }

public sealed class MainViewModel : ObservableObject
{
    private readonly ServerStore _store;
    private readonly SqlService _sql = new();
    private readonly TransferOrchestrator _orchestrator;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly System.Diagnostics.Stopwatch _totalClock = new();
    private readonly System.Text.StringBuilder _log = new();
    private CancellationTokenSource? _cts;

    private ServerEntry? _sourceServer;
    private ServerEntry? _targetServer;
    private List<string> _sourceDatabases = [];
    private string? _selectedSourceDb;
    private string _targetDb = "";
    private bool _overwrite;
    private bool _deleteSourceBackup;
    private bool _deleteTargetBackup;
    private SourceMode _mode;
    private string _backupFile = "";
    private string _sourceInfo = "";
    private string _targetInfo = "";
    private bool _isLoadingDatabases;
    private bool _isRunning;
    private bool _isCancelling;
    private string _totalElapsed = "";
    private string _resultText = "";
    private ResultState _resultState;

    public MainViewModel(ServerStore store)
    {
        _store = store;
        _orchestrator = new TransferOrchestrator(_sql, new FileCopyService());
        Steps = new TransferSteps(
            new StepViewModel("Step_Prepare"), new StepViewModel("Step_Backup"), new StepViewModel("Step_Copy"),
            new StepViewModel("Step_Restore"), new StepViewModel("Step_Cleanup"));

        StartCommand = new RelayCommand(StartAsync, () => CanStart);
        CancelCommand = new RelayCommand(Cancel, () => IsRunning && !_isCancelling);
        SwapCommand = new RelayCommand(SwapAsync, () => !IsRunning && IsNewBackupMode);
        TestSourceCommand = new RelayCommand(() => TestAsync(SourceServer, i => SourceInfo = i), () => !IsRunning && SourceServer is not null);
        TestTargetCommand = new RelayCommand(() => TestAsync(TargetServer, i => TargetInfo = i), () => !IsRunning && TargetServer is not null);

        _timer.Tick += (_, _) => Tick();
        ReloadServers();
        SourceServer = Servers.FirstOrDefault(s => s.Id == _store.Settings.LastSourceId);
        TargetServer = Servers.FirstOrDefault(s => s.Id == _store.Settings.LastTargetId);
    }

    public ObservableCollection<ServerEntry> Servers { get; } = [];

    public TransferSteps Steps { get; }

    public IEnumerable<StepViewModel> StepList => Steps.All;

    public RelayCommand StartCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand SwapCommand { get; }

    public RelayCommand TestSourceCommand { get; }

    public RelayCommand TestTargetCommand { get; }

    public ServerEntry? SourceServer
    {
        get => _sourceServer;
        set
        {
            if (!Set(ref _sourceServer, value))
                return;
            SourceInfo = "";
            SourceDatabases = [];
            _selectedSourceDb = null;
            OnPropertyChanged(nameof(SelectedSourceDb));
            _store.Settings.LastSourceId = value?.Id;
            _store.SaveSettings();
        }
    }

    public ServerEntry? TargetServer
    {
        get => _targetServer;
        set
        {
            if (!Set(ref _targetServer, value))
                return;
            TargetInfo = "";
            _store.Settings.LastTargetId = value?.Id;
            _store.SaveSettings();
        }
    }

    public List<string> SourceDatabases { get => _sourceDatabases; private set => Set(ref _sourceDatabases, value); }

    public string? SelectedSourceDb
    {
        get => _selectedSourceDb;
        set
        {
            if (value is null && _isLoadingDatabases)
                return;
            if (Set(ref _selectedSourceDb, value) && value is not null)
                TargetDb = value;
        }
    }

    public string TargetDb { get => _targetDb; set => Set(ref _targetDb, value); }

    public bool Overwrite { get => _overwrite; set => Set(ref _overwrite, value); }

    public bool DeleteSourceBackup { get => _deleteSourceBackup; set => Set(ref _deleteSourceBackup, value); }

    public bool DeleteTargetBackup { get => _deleteTargetBackup; set => Set(ref _deleteTargetBackup, value); }

    public bool IsNewBackupMode
    {
        get => _mode == SourceMode.NewBackup;
        set { if (value) SetMode(SourceMode.NewBackup); }
    }

    public bool IsExistingBackupMode
    {
        get => _mode == SourceMode.ExistingBackup;
        set { if (value) SetMode(SourceMode.ExistingBackup); }
    }

    public string BackupFile
    {
        get => _backupFile;
        set
        {
            if (Set(ref _backupFile, value) && !string.IsNullOrWhiteSpace(value))
                TargetDb = TransferOrchestrator.DeriveDatabaseName(value);
        }
    }

    public string SourceInfo { get => _sourceInfo; private set => Set(ref _sourceInfo, value); }

    public string TargetInfo { get => _targetInfo; private set => Set(ref _targetInfo, value); }

    public bool IsLoadingDatabases { get => _isLoadingDatabases; private set => Set(ref _isLoadingDatabases, value); }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (Set(ref _isRunning, value))
                OnPropertyChanged(nameof(IsIdle));
        }
    }

    public bool IsIdle => !IsRunning;

    public string TotalElapsed { get => _totalElapsed; private set => Set(ref _totalElapsed, value); }

    public string ResultText { get => _resultText; private set => Set(ref _resultText, value); }

    public ResultState ResultState { get => _resultState; private set => Set(ref _resultState, value); }

    public string LogText => _log.ToString();

    private bool CanStart =>
        !IsRunning && TargetServer is not null && !string.IsNullOrWhiteSpace(TargetDb)
        && (IsExistingBackupMode
            ? !string.IsNullOrWhiteSpace(BackupFile)
            : SourceServer is not null && !string.IsNullOrEmpty(SelectedSourceDb));

    private void SetMode(SourceMode mode)
    {
        if (_mode == mode)
            return;
        _mode = mode;
        OnPropertyChanged(nameof(IsNewBackupMode));
        OnPropertyChanged(nameof(IsExistingBackupMode));

        // Zieldatenbankname passend zur Quelle neu vorbelegen
        if (mode == SourceMode.ExistingBackup && !string.IsNullOrWhiteSpace(BackupFile))
            TargetDb = TransferOrchestrator.DeriveDatabaseName(BackupFile);
        else if (mode == SourceMode.NewBackup && !string.IsNullOrEmpty(SelectedSourceDb))
            TargetDb = SelectedSourceDb;
        CommandManager.InvalidateRequerySuggested();
    }

    public void ReloadServers()
    {
        var sourceId = SourceServer?.Id;
        var targetId = TargetServer?.Id;
        Servers.Clear();
        foreach (var s in _store.Servers)
            Servers.Add(s);
        SourceServer = Servers.FirstOrDefault(s => s.Id == sourceId);
        TargetServer = Servers.FirstOrDefault(s => s.Id == targetId);
    }

    /// <summary>Füllt die Datenbankliste der Quelle (beim Öffnen der Auswahl).</summary>
    public async Task LoadSourceDatabasesAsync(string? preselect = null)
    {
        var server = SourceServer;
        if (server is null || IsLoadingDatabases)
            return;

        var keep = preselect ?? SelectedSourceDb;
        IsLoadingDatabases = true;
        try
        {
            var list = await _sql.ListDatabasesAsync(server);
            if (server != SourceServer)
                return;
            SourceDatabases = list;
            _selectedSourceDb = list.FirstOrDefault(d => string.Equals(d, keep, StringComparison.OrdinalIgnoreCase));
            OnPropertyChanged(nameof(SelectedSourceDb));
        }
        catch (Exception ex)
        {
            SourceInfo = Loc.T("Test_Fail", ex.Message);
        }
        finally
        {
            IsLoadingDatabases = false;
        }
    }

    private async Task TestAsync(ServerEntry? server, Action<string> setInfo)
    {
        if (server is null)
            return;
        setInfo(Loc.T("Test_Running"));
        try
        {
            var info = await _sql.TestAsync(server);
            setInfo(Loc.T("Test_Ok", info.Display));
        }
        catch (Exception ex)
        {
            setInfo(Loc.T("Test_Fail", ex.Message));
        }
    }

    private async Task SwapAsync()
    {
        var (oldSource, oldTarget) = (SourceServer, TargetServer);
        var (oldSourceDb, oldTargetDb) = (SelectedSourceDb, TargetDb);

        SourceServer = oldTarget;
        TargetServer = oldSource;
        TargetDb = oldSourceDb ?? "";
        await LoadSourceDatabasesAsync(oldTargetDb);
        TargetDb = oldSourceDb ?? "";
    }

    private async Task StartAsync()
    {
        if (TargetServer is null)
            return;
        TransferRequest request;
        if (IsExistingBackupMode)
        {
            if (string.IsNullOrWhiteSpace(BackupFile))
                return;
            request = new TransferRequest(SourceMode.ExistingBackup, null, "", BackupFile.Trim(),
                TargetServer, TargetDb.Trim(), Overwrite, false, DeleteTargetBackup);
        }
        else
        {
            if (SourceServer is null || SelectedSourceDb is null)
                return;
            request = new TransferRequest(SourceMode.NewBackup, SourceServer, SelectedSourceDb, null,
                TargetServer, TargetDb.Trim(), Overwrite, DeleteSourceBackup, DeleteTargetBackup);
        }

        foreach (var step in Steps.All)
            step.Reset();
        _log.Clear();
        OnPropertyChanged(nameof(LogText));
        ResultState = ResultState.None;
        ResultText = "";

        _cts = new CancellationTokenSource();
        _isCancelling = false;
        IsRunning = true;
        _totalClock.Restart();
        _timer.Start();
        CommandManager.InvalidateRequerySuggested();

        TransferResult result;
        try
        {
            result = await _orchestrator.RunAsync(request, Steps, Log, _cts.Token);
        }
        finally
        {
            _timer.Stop();
            _totalClock.Stop();
            TotalElapsed = Fmt.Duration(_totalClock.Elapsed);
            _cts.Dispose();
            _cts = null;
            IsRunning = false;
            CommandManager.InvalidateRequerySuggested();
        }

        (ResultState, ResultText) = result.Outcome switch
        {
            TransferOutcome.Success => (ResultState.Success, Loc.T("Result_Success", TotalElapsed)),
            TransferOutcome.Canceled => (ResultState.Canceled, Loc.T("Result_Canceled")),
            _ => (ResultState.Failed, Loc.T("Result_Failed", result.Error)),
        };
        Log(ResultText);
    }

    private void Cancel()
    {
        _isCancelling = true;
        _cts?.Cancel();
    }

    /// <summary>Wird vom Fenster beim Schließen aufgerufen. True = Schließen erlaubt.</summary>
    public bool ConfirmClose()
    {
        if (!IsRunning)
            return true;
        if (MessageBox.Show(Loc.T("Close_Confirm"), BuildInfo.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return false;
        Cancel();
        return true;
    }

    private void Tick()
    {
        TotalElapsed = Fmt.Duration(_totalClock.Elapsed);
        foreach (var step in Steps.All)
            step.Tick();
    }

    private void Log(string message)
    {
        _log.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        OnPropertyChanged(nameof(LogText));
    }
}
