using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SqlTransfer.Infrastructure;
using SqlTransfer.Models;
using SqlTransfer.ViewModels;

namespace SqlTransfer.Views;

public partial class ServersWindow : Window
{
    private readonly ServersViewModel _vm;
    private bool _syncingPassword;

    public ServersWindow(IEnumerable<ServerEntry> servers)
    {
        _vm = new ServersViewModel(servers);
        InitializeComponent();
        DataContext = _vm;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
        SyncPasswordBox();
    }

    public List<ServerEntry> Result { get; private set; } = [];

    // PasswordBox unterstützt kein Binding: Wert beim Wechsel der Auswahl manuell setzen
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ServersViewModel.Selected))
            SyncPasswordBox();
    }

    private void SyncPasswordBox()
    {
        _syncingPassword = true;
        PasswordBox.Password = _vm.Selected?.Password ?? "";
        _syncingPassword = false;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_syncingPassword && _vm.Selected is not null)
            _vm.Selected.Password = PasswordBox.Password;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_vm.IsValid)
        {
            MessageBox.Show(this, Loc.T("Srv_Invalid"), BuildInfo.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = _vm.ToEntries();
        DialogResult = true;
    }
}
