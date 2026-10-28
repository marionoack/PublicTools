using System.ComponentModel;
using System.Reflection;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ApplicationStarter.Models;
using ApplicationStarter.Services;
using ApplicationStarter.Views;
using Microsoft.Win32;

namespace ApplicationStarter;

public partial class MainWindow : Window
{
    public static readonly RoutedUICommand LaunchNumberedShortcutCommand =
        new("Verknuepfung direkt starten", nameof(LaunchNumberedShortcutCommand), typeof(MainWindow));
    private readonly SettingsStore store;
    private Configuration config;
    private readonly List<Broker> applicationBrokers = [];
    private List<ShortcutItem> shortcutItems = [];
    private bool closing, canClose, starting;
    private sealed record ShortcutItem(Shortcut Shortcut, UserProfile Profile, ImageSource Icon, int? Number = null)
    {
        public string DisplayName => Number is int number ? $"[{number}] {Shortcut.Name}" : Shortcut.Name;
        public string AccountName => Profile.IsCurrent ? Profile.Name : $"{Environment.MachineName}\\{Profile.UserName}";
    }

    public MainWindow() : this(new SettingsStore()) { }

    public MainWindow(SettingsStore store)
    {
        InitializeComponent();
        this.store = store;
        config = store.Load();
        store.Save(config);
        var buildDate = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "BuildDate")?.Value ?? "unbekannt";
        Title = $"Application Starter | Build {buildDate}";
        AccountLabel.Text = WindowsIdentity.GetCurrent().Name;
        StorageLabel.Text = $"Lokale Konfiguration: {store.Root}\nPasswoerter: separate, DPAPI-verschluesselte Dateien fuer Ihr Windows-Konto.";
        RefreshShortcuts();
        CommandBindings.Add(new CommandBinding(LaunchNumberedShortcutCommand, LaunchNumberedShortcut_Executed,
            (_, e) => e.CanExecute = !starting && !closing && e.Parameter is int number &&
                number >= 1 && number <= Math.Min(9, ShortcutsList.Items.Count)));
        for (var number = 1; number <= 9; number++)
        {
            InputBindings.Add(new KeyBinding(LaunchNumberedShortcutCommand,
                new KeyGesture((Key)((int)Key.D1 + number - 1), ModifierKeys.Control))
            { CommandParameter = number });
            InputBindings.Add(new KeyBinding(LaunchNumberedShortcutCommand,
                new KeyGesture((Key)((int)Key.NumPad1 + number - 1), ModifierKeys.Control))
            { CommandParameter = number });
        }
        Loaded += (_, _) => ShortcutFilter.Focus();
        Activated += (_, _) => QueueWorkspaceFocus();
    }

    private void RefreshShortcuts(string? id = null)
    {
        shortcutItems = config.Shortcuts.Select(shortcut =>
        {
            var profile = config.Profiles.First(p => p.Id == shortcut.ProfileId);
            return new ShortcutItem(shortcut, profile, IconService.Get(shortcut, profile, store.Root));
        }).ToList();
        ApplyShortcutFilter(id);
    }

    private void ApplyShortcutFilter(string? id = null)
    {
        var query = ShortcutFilter.Text.Trim();
        var visible = shortcutItems.Where(item => item.Shortcut.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .Select((item, index) => item with { Number = index < 9 ? index + 1 : null }).ToList();
        ShortcutsList.ItemsSource = visible;
        ShortcutsList.SelectedItem = ShortcutsList.Items.Cast<ShortcutItem>().FirstOrDefault(i => i.Shortcut.Id == id)
            ?? ShortcutsList.Items.Cast<ShortcutItem>().FirstOrDefault();
        StartButton.IsEnabled = !starting && !closing && visible.Count > 0;
        CommandManager.InvalidateRequerySuggested();
    }

    private void ShortcutFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (ShortcutsList is not null) ApplyShortcutFilter();
    }

    private void ShortcutsList_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None || string.IsNullOrEmpty(e.Text) || e.Text.All(char.IsControl)) return;
        e.Handled = true;
        ShortcutFilter.Focus();
        ShortcutFilter.AppendText(e.Text);
        ShortcutFilter.CaretIndex = ShortcutFilter.Text.Length;
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab && (Keyboard.Modifiers == ModifierKeys.Control ||
            Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)))
        {
            e.Handled = true;
            SwitchWorkspaceTab(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
            return;
        }
        if (!(ShortcutFilter.IsKeyboardFocusWithin || ShortcutsList.IsKeyboardFocusWithin)) return;
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.E or Key.I)
        {
            e.Handled = true;
            if (e.Key == Key.I) EditShortcut(null);
            else if (ShortcutsList.SelectedItem is ShortcutItem item) EditShortcut(item.Shortcut);
            return;
        }
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (Keyboard.Modifiers == ModifierKeys.Alt && key is Key.Up or Key.Down)
        {
            e.Handled = true;
            MoveShortcut(key == Key.Up ? -1 : 1);
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.D)
        {
            e.Handled = true;
            DuplicateSelectedShortcut();
            return;
        }
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            ShortcutFilter.Clear();
            ShortcutFilter.Focus();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await StartAsync();
        }
        else if (e.Key == Key.Down && ShortcutFilter.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            ShortcutsList.Focus();
        }
        else if (e.Key == Key.Back && ShortcutsList.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            ShortcutFilter.Focus();
            ShortcutFilter.CaretIndex = ShortcutFilter.Text.Length;
            EditingCommands.Backspace.Execute(null, ShortcutFilter);
        }
    }

    private async void LaunchNumberedShortcut_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (starting || closing || e.Parameter is not int number || number < 1 ||
            number > Math.Min(9, ShortcutsList.Items.Count)) return;
        e.Handled = true;
        ShortcutsList.SelectedIndex = number - 1;
        ShortcutsList.ScrollIntoView(ShortcutsList.SelectedItem);
        await StartAsync();
    }

    private void Profiles_Click(object sender, RoutedEventArgs e)
    {
        new ProfileWindow(config, store) { Owner = this }.ShowDialog();
        RefreshShortcuts((ShortcutsList.SelectedItem as ShortcutItem)?.Shortcut.Id);
    }

    private void EditShortcut(Shortcut? shortcut, bool duplicate = false)
    {
        var dialog = new ShortcutWindow(config, shortcut, duplicate) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try
        {
            if (shortcut is null || duplicate) config.Shortcuts.Add(dialog.Result);
            else config.Shortcuts[config.Shortcuts.IndexOf(shortcut)] = dialog.Result;
            store.Save(config);
            RefreshShortcuts(dialog.Result.Id);
        }
        catch (Exception ex) { DialogHelpers.Error(this, ex); }
    }
    private void New_Click(object sender, RoutedEventArgs e) => EditShortcut(null);
    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (ShortcutsList.SelectedItem is ShortcutItem item) EditShortcut(item.Shortcut);
    }
    private void Duplicate_Click(object sender, RoutedEventArgs e) => DuplicateSelectedShortcut();
    private void DuplicateSelectedShortcut()
    {
        if (ShortcutsList.SelectedItem is ShortcutItem item) EditShortcut(item.Shortcut, true);
    }
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ShortcutsList.SelectedItem is not ShortcutItem item) return;
        if (!DialogHelpers.Confirm(this, $"Verknuepfung '{item.Shortcut.Name}' entfernen? Das Programm bleibt installiert.")) return;
        try { config.Shortcuts.Remove(item.Shortcut); store.Save(config); RefreshShortcuts(); }
        catch (Exception ex) { DialogHelpers.Error(this, ex); }
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => MoveShortcut(-1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => MoveShortcut(1);
    private void MoveShortcut(int direction)
    {
        if (ShortcutsList.SelectedItem is not ShortcutItem item) return;
        var target = ShortcutsList.SelectedIndex + direction;
        if (target < 0 || target >= ShortcutsList.Items.Count) return;
        var neighbor = (ShortcutItem)ShortcutsList.Items[target];
        var sourceIndex = config.Shortcuts.IndexOf(item.Shortcut);
        var targetIndex = config.Shortcuts.IndexOf(neighbor.Shortcut);
        (config.Shortcuts[sourceIndex], config.Shortcuts[targetIndex]) = (config.Shortcuts[targetIndex], config.Shortcuts[sourceIndex]);
        try { store.Save(config); }
        catch (Exception ex)
        {
            (config.Shortcuts[sourceIndex], config.Shortcuts[targetIndex]) = (config.Shortcuts[targetIndex], config.Shortcuts[sourceIndex]);
            DialogHelpers.Error(this, ex);
            return;
        }
        RefreshShortcuts(item.Shortcut.Id);
        ShortcutsList.ScrollIntoView(ShortcutsList.SelectedItem);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var selection = new ExportWindow(config) { Owner = this };
        if (selection.ShowDialog() != true) return;
        var dialog = new SaveFileDialog { Filter = "JSON-Datei (*.json)|*.json", FileName = "ApplicationStarter-Verknuepfungen.json", DefaultExt = ".json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            File.WriteAllText(dialog.FileName, ShortcutTransfer.Export(config, selection.SelectedIds));
            Status.Text = $"{selection.SelectedIds.Count} Verknuepfungen exportiert (ohne Passwoerter).";
        }
        catch (Exception ex) { DialogHelpers.Error(this, ex); }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "JSON-Datei (*.json)|*.json", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var archive = ShortcutTransfer.Parse(File.ReadAllText(dialog.FileName));
            var options = new ImportWindow(archive.Shortcuts.Count) { Owner = this };
            if (options.ShowDialog() != true) return;
            var result = ShortcutTransfer.Import(config, archive, options.Mode);
            store.Save(result.Configuration);
            config = result.Configuration;
            RefreshShortcuts();
            Status.Text = $"Import: {result.Added} neu, {result.Replaced} ueberschrieben, {result.Skipped} uebersprungen. " +
                $"{result.AccountsAdded} Kontokonfigurationen ohne Passwort angelegt.";
        }
        catch (Exception ex) { DialogHelpers.Error(this, ex); }
    }
    private async void Start_Click(object sender, RoutedEventArgs e) => await StartAsync();
    private async void Shortcut_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(ShortcutsList, e.OriginalSource as DependencyObject) is ListBoxItem)
            await StartAsync();
    }

    private async Task StartAsync()
    {
        if (starting || closing || ShortcutsList.SelectedItem is not ShortcutItem item) return;
        starting = true;
        StartButton.IsEnabled = false;
        Status.Text = $"Starte {item.Shortcut.Name} unter {item.Profile.DisplayName}...";
        Broker? broker = null;
        try
        {
            broker = await Broker.StartAsync(item.Profile, item.Shortcut.LoadProfile, store, item.Shortcut.RequiresElevation);
            if (closing) { await broker.DisposeAsync(); return; }
            if (string.IsNullOrWhiteSpace(item.Shortcut.IconPath) && item.Shortcut.Kind != ShortcutKind.FileBrowser)
            {
                var png = await broker.RequestAsync<byte[]>("icon", new StartRequest(item.Shortcut));
                try { IconService.Cache(item.Shortcut, item.Profile, store.Root, png); }
                catch (IOException) { }
                item = item with { Icon = IconService.Get(item.Shortcut, item.Profile, store.Root) };
                RefreshShortcuts(item.Shortcut.Id);
            }
            if (item.Shortcut.Kind == ShortcutKind.Application)
            {
                await broker.RequestAsync<bool>("launch", new StartRequest(item.Shortcut));
                applicationBrokers.Add(broker);
            }
            else
            {
                UserControl view = item.Shortcut.OpensTerminal
                    ? new TerminalView(broker, item.Shortcut, store.Root)
                    : item.Shortcut.Kind == ShortcutKind.FileBrowser
                        ? new FileBrowserView(broker, item.Shortcut)
                        : new ExecutionView(broker, item.Shortcut);
                AddTab(item, view, broker.Identity.Name + (broker.Identity.IsElevated ? " | UAC" : ""));
            }
            Status.Text = $"Gestartet: {item.Shortcut.Name} | Konto: {broker.Identity.Name}" +
                (broker.Identity.IsElevated ? " | Als Administrator" : "");
            broker = null;
        }
        catch (Exception ex)
        {
            Status.Text = ex.Message;
            DialogHelpers.Error(this, ex);
        }
        finally
        {
            if (broker is not null) await broker.DisposeAsync();
            starting = false;
            if (!closing) StartButton.IsEnabled = ShortcutsList.Items.Count > 0;
            if (!closing && item.Shortcut.Kind != ShortcutKind.Application)
                QueueWorkspaceFocus();
        }
    }

    private void AddTab(ShortcutItem item, UserControl view, string identity)
    {
        if (view is TerminalView terminal)
        {
            terminal.ShortcutRequested += number =>
            {
                if (LaunchNumberedShortcutCommand.CanExecute(number, this))
                    LaunchNumberedShortcutCommand.Execute(number, this);
            };
            terminal.TabSwitchRequested += SwitchWorkspaceTab;
        }
        var tab = new TabItem { Content = view, ToolTip = $"{item.Shortcut.Name} | {identity}" };
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(new Image { Source = item.Icon, Width = 16, Height = 16, Margin = new Thickness(0, 0, 6, 0) });
        header.Children.Add(new TextBlock { Text = $"{item.Shortcut.Name} [{identity}]", VerticalAlignment = VerticalAlignment.Center });
        var close = new Button { Content = "x", Padding = new Thickness(5, 0, 5, 1), Margin = new Thickness(8, 0, 0, 0) };
        close.Click += async (_, _) =>
        {
            if ((view is TerminalView || view is ExecutionView { IsRunning: true }) &&
                !DialogHelpers.Confirm(this, "Tab schliessen und seine laufenden Prozesse beenden?")) return;
            close.IsEnabled = false;
            Workspace.Items.Remove(tab);
            await ((IAsyncDisposable)view).DisposeAsync();
        };
        header.Children.Add(close);
        tab.Header = header;
        Workspace.Items.Add(tab);
        Workspace.SelectedItem = tab;
        QueueWorkspaceFocus();
    }

    private void Workspace_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.Source, Workspace)) QueueWorkspaceFocus();
    }

    private void QueueWorkspaceFocus() => Dispatcher.InvokeAsync(() =>
    {
        if (closing || !IsActive) return;
        switch ((Workspace.SelectedItem as TabItem)?.Content)
        {
            case FileBrowserView browser: browser.FocusInput(); break;
            case TerminalView terminal: terminal.FocusInput(); break;
            case ExecutionView execution: execution.FocusInput(); break;
        }
    }, DispatcherPriority.ContextIdle);

    private void SwitchWorkspaceTab(int direction)
    {
        if (closing || Workspace.Items.Count == 0) return;
        Workspace.SelectedIndex = (Workspace.SelectedIndex + direction + Workspace.Items.Count) % Workspace.Items.Count;
        ((TabItem)Workspace.SelectedItem).Focus();
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (canClose) return;
        e.Cancel = true;
        if (closing) return;
        var views = Workspace.Items.OfType<TabItem>().Select(t => t.Content).OfType<IAsyncDisposable>().ToList();
        if (views.Any(v => v is TerminalView or ExecutionView { IsRunning: true }) &&
            !DialogHelpers.Confirm(this, "Application Starter schliessen? Laufende Terminal- und Ausfuehrungsprozesse werden beendet. GUI-Anwendungen bleiben geoeffnet.")) return;
        closing = true;
        IsEnabled = false;
        try
        {
            await Task.WhenAll(views.Select(v => v.DisposeAsync().AsTask())
                .Concat(applicationBrokers.Select(b => b.DisposeAsync().AsTask())));
        }
        finally { canClose = true; _ = Dispatcher.InvokeAsync(Close); }
    }
}