using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ApplicationStarter.Models;
using ApplicationStarter.Services;

namespace ApplicationStarter.Views;

public partial class FileBrowserView : UserControl, IAsyncDisposable
{
    private readonly Broker broker;
    private readonly Shortcut shortcut;
    private List<FileEntry> entries = [];
    private string? currentPath;
    private bool busy, closed;
    private Window OwnerWindow => Window.GetWindow(this);

    public FileBrowserView(Broker broker, Shortcut shortcut)
    {
        InitializeComponent();
        this.broker = broker;
        this.shortcut = shortcut;
        broker.Disconnected += OnDisconnected;
        Loaded += Initialize;
    }

    private async void Initialize(object sender, RoutedEventArgs e)
    {
        Loaded -= Initialize;
        await NavigateAsync(shortcut.WorkingDirectory);
    }

    private async Task RunAsync(Func<Task> action, bool focusFiles = false)
    {
        if (busy || closed) return;
        busy = true;
        var restoreFocus = focusFiles || Files.IsKeyboardFocusWithin;
        Status.Text = "Bitte warten...";
        try
        {
            await action();
            if (!closed) Status.Text = $"Konto: {broker.Identity.Name} | {entries.Count} Eintraege | Loeschen erfolgt ohne Papierkorb.";
        }
        catch (Exception ex) { if (!closed) Status.Text = ex.Message; }
        finally
        {
            busy = false;
            if (!closed && restoreFocus) FocusFiles();
        }
    }

    private async Task LoadDirectoryAsync(string path, string? selectedPath = null)
    {
        var listing = await broker.RequestAsync<DirectoryListing>("list", path);
        if (closed) return;
        if (string.Equals(currentPath, listing.Path, StringComparison.OrdinalIgnoreCase))
            selectedPath ??= (Files.SelectedItem as FileEntry)?.Path;
        currentPath = listing.Path;
        PathInput.Text = currentPath;
        entries = listing.Entries;
        ApplyFilter(selectedPath);
    }

    private Task NavigateAsync(string path, string? selectedPath = null) =>
        RunAsync(() => LoadDirectoryAsync(path, selectedPath), true);

    private void ApplyFilter(string? selectedPath = null)
    {
        selectedPath ??= (Files.SelectedItem as FileEntry)?.Path;
        var visible = entries.Where(item => ShowHidden.IsChecked == true || !item.Hidden).ToList();
        Files.ItemsSource = visible;
        Files.SelectedItem = visible.FirstOrDefault(item => string.Equals(item.Path, selectedPath, StringComparison.OrdinalIgnoreCase))
            ?? visible.FirstOrDefault();
    }

    private void FocusFiles()
    {
        if (!IsVisible || Window.GetWindow(this)?.IsActive != true) return;
        Files.Focus();
        if (Files.SelectedItem is not FileEntry entry) return;
        Files.CurrentCell = new DataGridCellInfo(entry, Files.Columns[0]);
        Files.ScrollIntoView(entry);
        Files.UpdateLayout();
        if (Files.Columns[0].GetCellContent(entry)?.Parent is DataGridCell cell) cell.Focus();
    }

    public void FocusInput()
    {
        if (!closed && IsLoaded && IsVisible) FocusFiles();
    }

    private async void Browser_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (closed) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        if (key == Key.Tab && Files.IsKeyboardFocusWithin &&
            (modifiers == ModifierKeys.None || modifiers == ModifierKeys.Shift))
        {
            e.Handled = true;
            // Bypass DataGrid's internal cell tab groups.
            Files.MoveFocus(new TraversalRequest(modifiers == ModifierKeys.Shift
                ? FocusNavigationDirection.Previous : FocusNavigationDirection.Next));
        }
        else if ((modifiers == ModifierKeys.Control && key == Key.L) ||
            (modifiers == ModifierKeys.Alt && key == Key.D))
        {
            e.Handled = true;
            PathInput.Focus();
            PathInput.SelectAll();
        }
        else if (modifiers == ModifierKeys.Alt && key == Key.Up)
        {
            e.Handled = true;
            await UpAsync();
        }
        else if (modifiers == ModifierKeys.None && key == Key.F5)
        {
            e.Handled = true;
            await RefreshAsync();
        }
        else if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && key == Key.N)
        {
            e.Handled = true;
            await NewAsync(true);
        }
        else if (modifiers == ModifierKeys.None && Files.IsKeyboardFocusWithin)
        {
            switch (key)
            {
                case Key.Enter: e.Handled = true; await OpenSelectedAsync(); break;
                case Key.Back: e.Handled = true; await UpAsync(); break;
                case Key.F2: e.Handled = true; await RenameAsync(); break;
                case Key.Delete: e.Handled = true; await DeleteAsync(); break;
            }
        }
    }

    private async void Navigate_Click(object sender, RoutedEventArgs e) => await NavigateAsync(PathInput.Text);
    private async void Path_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { e.Handled = true; await NavigateAsync(PathInput.Text); }
    }
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();
    private Task RefreshAsync() => currentPath is not null ? NavigateAsync(currentPath) : ShowDrivesAsync();

    private async void Up_Click(object sender, RoutedEventArgs e) => await UpAsync();
    private Task UpAsync()
    {
        if (currentPath is null || busy) return Task.CompletedTask;
        var parent = System.IO.Directory.GetParent(currentPath);
        return parent is null ? ShowDrivesAsync(currentPath) : NavigateAsync(parent.FullName, currentPath);
    }
    private async void Drives_Click(object sender, RoutedEventArgs e) => await ShowDrivesAsync();
    private Task ShowDrivesAsync(string? selectedPath = null) => RunAsync(async () =>
    {
        entries = await broker.RequestAsync<List<FileEntry>>("drives", null);
        currentPath = null;
        PathInput.Text = "";
        ApplyFilter(selectedPath);
    }, true);
    private void Hidden_Changed(object sender, RoutedEventArgs e) { if (Files is not null) ApplyFilter(); }

    private async void Files_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(Files, e.OriginalSource as DependencyObject) is not DataGridRow) return;
        await OpenSelectedAsync();
    }

    private async Task OpenSelectedAsync()
    {
        if (busy || Files.SelectedItem is not FileEntry entry) return;
        if (entry.IsDirectory) await NavigateAsync(entry.Path);
        else await RunAsync(async () => await broker.RequestAsync<bool>("file", new FileRequest("open", entry.Path)));
    }

    private async Task NewAsync(bool folder)
    {
        if (currentPath is null || busy) return;
        var name = DialogHelpers.Prompt(OwnerWindow, folder ? "Ordner anlegen" : "Datei anlegen", "Name:");
        if (name is null) return;
        await RunAsync(async () =>
        {
            var path = DialogHelpers.ChildPath(currentPath, name);
            if (await broker.RequestAsync<bool>("exists", path)) throw new IOException("Dieser Name existiert bereits.");
            await broker.RequestAsync<bool>("file", new FileRequest(folder ? "mkdir" : "newfile", path));
            await LoadDirectoryAsync(currentPath, path);
        }, true);
    }
    private async void NewFile_Click(object sender, RoutedEventArgs e) => await NewAsync(false);
    private async void NewFolder_Click(object sender, RoutedEventArgs e) => await NewAsync(true);

    private async void Rename_Click(object sender, RoutedEventArgs e) => await RenameAsync();
    private async Task RenameAsync()
    {
        if (currentPath is null || busy || Files.SelectedItem is not FileEntry entry) return;
        var name = DialogHelpers.Prompt(OwnerWindow, "Umbenennen", "Neuer Name:", entry.Name);
        if (name is null) return;
        await RunAsync(async () =>
        {
            var target = DialogHelpers.ChildPath(currentPath, name);
            await broker.RequestAsync<bool>("file", new FileRequest("rename", entry.Path, target));
            await LoadDirectoryAsync(currentPath, target);
        }, true);
    }

    private async Task TransferAsync(bool move)
    {
        if (currentPath is null || busy || Files.SelectedItem is not FileEntry entry) return;
        var destination = DialogHelpers.Prompt(OwnerWindow, move ? "Verschieben" : "Kopieren",
            "Vollstaendiger Zielpfad einschliesslich Datei-/Ordnername (Variablen des Startkontos erlaubt):", entry.Path);
        if (destination is null) return;
        await RunAsync(async () =>
        {
            var exists = await broker.RequestAsync<bool>("exists", destination);
            if (exists && !DialogHelpers.Confirm(OwnerWindow,
                "Das Ziel existiert. Vorhandene Dateien ersetzen und Ordnerinhalte zusammenfuehren? Die Operation ist nicht rueckgaengig zu machen.")) return;
            await broker.RequestAsync<bool>("file", new FileRequest(move ? "move" : "copy", entry.Path, destination, exists));
            await LoadDirectoryAsync(currentPath);
        }, true);
    }
    private async void Copy_Click(object sender, RoutedEventArgs e) => await TransferAsync(false);
    private async void Move_Click(object sender, RoutedEventArgs e) => await TransferAsync(true);

    private async void Delete_Click(object sender, RoutedEventArgs e) => await DeleteAsync();
    private async Task DeleteAsync()
    {
        if (currentPath is null || busy || Files.SelectedItem is not FileEntry entry) return;
        if (!DialogHelpers.Confirm(OwnerWindow,
            $"'{entry.Name}'{(entry.IsDirectory ? " mit allen Inhalten" : "")} endgueltig loeschen?\nKein Papierkorb. Nicht rueckgaengig zu machen.")) return;
        await RunAsync(async () =>
        {
            await broker.RequestAsync<bool>("file", new FileRequest("delete", entry.Path));
            await LoadDirectoryAsync(currentPath);
        }, true);
    }

    private void OnDisconnected(Exception ex) =>
        Dispatcher.InvokeAsync(() => { if (!closed) Status.Text = ex.Message; });

    public async ValueTask DisposeAsync()
    {
        if (closed) return;
        closed = true;
        broker.Disconnected -= OnDisconnected;
        await broker.DisposeAsync();
    }
}
