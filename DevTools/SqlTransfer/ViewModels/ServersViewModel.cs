using System.Collections.ObjectModel;
using SqlTransfer.Infrastructure;
using SqlTransfer.Models;
using SqlTransfer.Services;

namespace SqlTransfer.ViewModels;

public sealed class ServerEditItem : ObservableObject
{
    private string _name = "";
    private string _dataSource = "";
    private bool _useSqlAuth;
    private string _userName = "";
    private string _backupPath = "";

    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get => _name; set { if (Set(ref _name, value)) OnPropertyChanged(nameof(DisplayName)); } }

    public string DataSource { get => _dataSource; set { if (Set(ref _dataSource, value)) OnPropertyChanged(nameof(DisplayName)); } }

    public bool UseSqlAuth
    {
        get => _useSqlAuth;
        set
        {
            if (Set(ref _useSqlAuth, value))
                OnPropertyChanged(nameof(UseWindowsAuth));
        }
    }

    public bool UseWindowsAuth { get => !UseSqlAuth; set => UseSqlAuth = !value; }

    public string UserName { get => _userName; set => Set(ref _userName, value); }

    public string Password { get; set; } = "";

    public string BackupPath { get => _backupPath; set => Set(ref _backupPath, value); }

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Name) ? Name : !string.IsNullOrWhiteSpace(DataSource) ? DataSource : Loc.T("Srv_NewServer");

    public static ServerEditItem From(ServerEntry e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        DataSource = e.DataSource,
        UseSqlAuth = e.UseSqlAuth,
        UserName = e.UserName,
        Password = e.GetPassword() ?? "",
        BackupPath = e.BackupPath,
    };

    public ServerEntry ToEntry()
    {
        var entry = new ServerEntry
        {
            Id = Id,
            Name = Name.Trim(),
            DataSource = DataSource.Trim(),
            UseSqlAuth = UseSqlAuth,
            UserName = UseSqlAuth ? UserName.Trim() : "",
            BackupPath = BackupPath.Trim(),
        };
        entry.SetPassword(UseSqlAuth ? Password : null);
        return entry;
    }
}

public sealed class ServersViewModel : ObservableObject
{
    private readonly SqlService _sql = new();
    private ServerEditItem? _selected;
    private string _testResult = "";
    private bool _isTesting;

    public ServersViewModel(IEnumerable<ServerEntry> servers)
    {
        Items = new ObservableCollection<ServerEditItem>(servers.Select(ServerEditItem.From));
        Selected = Items.FirstOrDefault();
        AddCommand = new RelayCommand(Add);
        RemoveCommand = new RelayCommand(Remove, () => Selected is not null);
        TestCommand = new RelayCommand(TestAsync, () => Selected is not null && !IsTesting);
    }

    public ObservableCollection<ServerEditItem> Items { get; }

    public ServerEditItem? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
            {
                TestResult = "";
                OnPropertyChanged(nameof(HasSelection));
            }
        }
    }

    public bool HasSelection => Selected is not null;

    public string TestResult { get => _testResult; private set => Set(ref _testResult, value); }

    public bool IsTesting { get => _isTesting; private set => Set(ref _isTesting, value); }

    public RelayCommand AddCommand { get; }

    public RelayCommand RemoveCommand { get; }

    public RelayCommand TestCommand { get; }

    public bool IsValid => Items.All(i => !string.IsNullOrWhiteSpace(i.DataSource));

    public List<ServerEntry> ToEntries() => Items.Select(i => i.ToEntry()).ToList();

    private void Add()
    {
        var item = new ServerEditItem();
        Items.Add(item);
        Selected = item;
    }

    private void Remove()
    {
        if (Selected is null)
            return;
        var index = Items.IndexOf(Selected);
        Items.Remove(Selected);
        Selected = Items.Count == 0 ? null : Items[Math.Min(index, Items.Count - 1)];
    }

    private async Task TestAsync()
    {
        if (Selected is null)
            return;
        var entry = Selected.ToEntry();
        IsTesting = true;
        TestResult = Loc.T("Test_Running");
        try
        {
            var lines = new List<string>();
            try
            {
                var info = await _sql.TestAsync(entry);
                lines.Add(Loc.T("Test_Ok", info.Display));
            }
            catch (Exception ex)
            {
                lines.Add(Loc.T("Test_Fail", ex.Message));
            }

            if (string.IsNullOrWhiteSpace(entry.BackupPath))
                lines.Add(Loc.T("Test_PathNone"));
            else if (await FileCopyService.DirectoryExistsAsync(entry.BackupPath, CancellationToken.None))
                lines.Add(Loc.T("Test_PathOk"));
            else
                lines.Add(Loc.T("Test_PathFail", entry.BackupPath));

            TestResult = string.Join(Environment.NewLine, lines);
        }
        finally
        {
            IsTesting = false;
        }
    }
}
