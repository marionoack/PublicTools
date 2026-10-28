using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SqlTransfer.Infrastructure;
using SqlTransfer.Services;
using SqlTransfer.ViewModels;

namespace SqlTransfer.Views;

public partial class MainWindow : Window
{
    private readonly ServerStore _store;
    private readonly MainViewModel _vm;

    public MainWindow(ServerStore store, MainViewModel vm)
    {
        _store = store;
        _vm = vm;
        InitializeComponent();
        Title = BuildInfo.Title;
        DataContext = vm;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_vm.ConfirmClose())
            e.Cancel = true;
        base.OnClosing(e);
    }

    private void ManageServers_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ServersWindow(_store.Servers) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _store.SaveServers(dialog.Result);
            _vm.ReloadServers();
        }
    }

    private async void SourceDatabases_DropDownOpened(object? sender, EventArgs e) =>
        await _vm.LoadSourceDatabasesAsync();

    private void BrowseBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "SQL Server Backup (*.bak)|*.bak|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) == true)
            _vm.BackupFile = dialog.FileName;
    }

    private void LogBox_TextChanged(object sender, TextChangedEventArgs e) => LogBox.ScrollToEnd();
}
