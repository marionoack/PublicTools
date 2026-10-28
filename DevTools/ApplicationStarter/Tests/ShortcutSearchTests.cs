using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ApplicationStarter.Models;
using ApplicationStarter.Services;
using ApplicationStarter.Views;
using Microsoft.Web.WebView2.Wpf;

namespace ApplicationStarter.Tests;

[CollectionDefinition("Desktop UI", DisableParallelization = true)]
public sealed class DesktopUiCollection;

[Collection("Desktop UI")]
public sealed class ShortcutSearchTests
{
    [Fact]
    public async Task ShortcutListAndBrowserSupportFilteringDuplicationAndKeyboardNavigation()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            Dispatcher.CurrentDispatcher.BeginInvoke(new Action(async () =>
            {
                var root = Path.Combine(Path.GetTempPath(), "ApplicationStarterTests-" + Guid.NewGuid().ToString("N"));
                App? app = null;
                MainWindow? window = null;
                Exception? failure = null;
                try
                {
                    app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    app.InitializeComponent();
                    var store = new SettingsStore(root);
                    var navigation = Path.Combine(root, "navigation");
                    Directory.CreateDirectory(Path.Combine(navigation, "Alpha"));
                    Directory.CreateDirectory(Path.Combine(navigation, "Beta"));
                    var config = Configuration.CreateDefault();
                    config.Profiles.Add(new UserProfile { Name = "Testkonto", UserName = "Testkonto" });
                    config.Shortcuts = Enumerable.Range(1, 12).Select(number => new Shortcut
                    {
                        Name = $"Tool {number:00}",
                        Kind = ShortcutKind.FileBrowser,
                        WorkingDirectory = navigation,
                        IconPath = Path.Combine(AppContext.BaseDirectory, "TestAssets", "Launcher.ico")
                    }).ToList();
                    config.Shortcuts[11].Kind = ShortcutKind.Terminal;
                    config.Shortcuts[11].FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"PowerShell\7\pwsh.exe");
                    config.Shortcuts[11].Arguments = "-NoLogo -NoProfile";
                    config.Shortcuts[8].Kind = ShortcutKind.Execution;
                    config.Shortcuts[8].FileName = config.Shortcuts[11].FileName;
                    config.Shortcuts[8].Arguments = "-NoProfile -Command \"Write-Output 'LOG-OK'; [Console]::Error.WriteLine('ERR-OK'); exit 4\"";
                    store.Save(config);
                    window = new MainWindow(store);
                    window.Show();
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    var filter = (TextBox)window.FindName("ShortcutFilter");
                    var list = (ListBox)window.FindName("ShortcutsList");
                    var workspace = (TabControl)window.FindName("Workspace");
                    Assert.Same(filter, FocusManager.GetFocusedElement(window));
                    var importButton = (Button)window.FindName("ImportButton");
                    var exportButton = (Button)window.FindName("ExportButton");
                    var accountButton = Descendants<Button>(window).Single(button => Equals(button.Content, "Benutzerkonten..."));
                    Assert.Same(accountButton.Parent, importButton.Parent);
                    Assert.Same(accountButton.Parent, exportButton.Parent);
                    Assert.Equal(12, list.Items.Count);
                    Assert.Equal("[1] Tool 01", DisplayName(list.Items[0]));
                    Assert.Equal("[9] Tool 09", DisplayName(list.Items[8]));
                    Assert.Equal("Tool 10", DisplayName(list.Items[9]));
                    ((Button)window.FindName("MoveDownButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal("[1] Tool 02", DisplayName(list.Items[0]));
                    Assert.Equal("[2] Tool 01", DisplayName(list.Items[1]));
                    Assert.Equal("Tool 02", store.Load().Shortcuts[0].Name);
                    ((Button)window.FindName("MoveUpButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal("[1] Tool 01", DisplayName(list.Items[0]));

                    filter.Text = " tOoL 1 ";
                    Assert.Equal(3, list.Items.Count);
                    Assert.Equal("[1] Tool 10", DisplayName(list.Items[0]));
                    Assert.Equal("[3] Tool 12", DisplayName(list.Items[2]));
                    ((Button)window.FindName("MoveDownButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal("[1] Tool 11", DisplayName(list.Items[0]));
                    Assert.Equal("[2] Tool 10", DisplayName(list.Items[1]));
                    ((Button)window.FindName("MoveUpButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Equal(0, list.SelectedIndex);
                    Assert.False(MainWindow.LaunchNumberedShortcutCommand.CanExecute(4, window));
                    Assert.True(MainWindow.LaunchNumberedShortcutCommand.CanExecute(1, window));
                    MainWindow.LaunchNumberedShortcutCommand.Execute(1, window);
                    using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                        while (workspace.Items.Count < 2) await Task.Delay(25, timeout.Token);
                    var tab = Assert.IsType<TabItem>(workspace.SelectedItem);
                    Assert.StartsWith("Tool 10 |", tab.ToolTip?.ToString());
                    await VerifyBrowserKeyboardNavigation(window, Assert.IsType<FileBrowserView>(tab.Content), navigation);

                    filter.Text = "kein-treffer";
                    Assert.Empty(list.Items);
                    Assert.Null(list.SelectedItem);
                    Assert.False(((Button)window.FindName("StartButton")).IsEnabled);
                    Assert.False(MainWindow.LaunchNumberedShortcutCommand.CanExecute(1, window));
                    filter.Clear();
                    Assert.Equal(12, list.Items.Count);
                    Assert.Equal("[1] Tool 01", DisplayName(list.Items[0]));
                    VerifyDuplication(window, store);
                    await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                    list.ScrollIntoView(list.SelectedItem);
                    list.UpdateLayout();
                    var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem);
                    var lines = Descendants<TextBlock>(row).ToList();
                    Assert.Equal(2, lines.Count);
                    Assert.Equal("Tool 01 Kopie", lines[0].Text);
                    Assert.Equal($"{Environment.MachineName}\\Testkonto", lines[1].Text);
                    VerifyTransferDialogs(window, store.Load());
                    VerifyScriptEditor(window, store.Load());
                    await VerifyNewTabFocusAndExecution(window, filter, workspace);
                }
                catch (Exception ex) { failure = ex; }
                finally
                {
                    if (window is not null)
                    {
                        var tabs = (TabControl)window.FindName("Workspace");
                        foreach (var tab in tabs.Items.Cast<TabItem>().Where(tab => tab.Content is IAsyncDisposable).ToList())
                        {
                            tabs.Items.Remove(tab);
                            await ((IAsyncDisposable)tab.Content).DisposeAsync();
                        }
                        window.Close();
                        while (window.IsVisible) await Task.Delay(25);
                    }
                    app?.Shutdown();
                    for (var attempt = 0; Directory.Exists(root); attempt++)
                    {
                        try { Directory.Delete(root, true); }
                        catch (IOException ex)
                        {
                            if (attempt >= 100) { failure ??= ex; break; }
                            await Task.Delay(50);
                        }
                    }
                    if (failure is null) completion.TrySetResult();
                    else completion.TrySetException(failure);
                    Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }
            }));
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Der WPF-Test muss Fenster und Benutzerprozess beenden.");
    }

    private static string? DisplayName(object item) =>
        TypeDescriptor.GetProperties(item)["DisplayName"]?.GetValue(item) as string;

    private static void VerifyScriptEditor(MainWindow owner, Configuration config)
    {
        foreach (var kind in new[] { ShortcutKind.PowerShell5, ShortcutKind.PowerShell7 })
        {
            var source = new Shortcut
            {
                Name = "Skript",
                Kind = kind,
                RunAsAdministrator = true,
                Script = "Write-Output 'Unveraendert %PATH%'",
                IconPath = config.Shortcuts[0].IconPath
            };
            var dialog = new ShortcutWindow(config, source, true) { Owner = owner };
            Exception? failure = null;
            owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    Assert.Equal(Visibility.Visible, ((StackPanel)dialog.FindName("ScriptPanel")).Visibility);
                    Assert.Equal(Visibility.Collapsed, ((StackPanel)dialog.FindName("ProgramPanel")).Visibility);
                    Assert.Equal(source.Script, ((TextBox)dialog.FindName("ScriptInput")).Text);
                    Assert.True(((CheckBox)dialog.FindName("UacInput")).IsChecked);
                    ((TextBox)dialog.FindName("ScriptInput")).Text += "\r\nWrite-Output 'Zweite Zeile'";
                    ((ComboBox)dialog.FindName("ScriptModeInput")).SelectedIndex = 1;
                    ((Button)dialog.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                catch (Exception ex) { failure = ex; dialog.Close(); }
            }));
            Assert.True(dialog.ShowDialog());
            Assert.Null(failure);
            Assert.NotEqual(source.Id, dialog.Result.Id);
            Assert.True(dialog.Result.Interactive);
            Assert.True(dialog.Result.RunAsAdministrator);
            Assert.Equal(kind, dialog.Result.Kind);
            Assert.Equal(source.Script + "\r\nWrite-Output 'Zweite Zeile'", dialog.Result.Script);
            Assert.Empty(dialog.Result.FileName);
            Assert.Equal("Write-Output 'Unveraendert %PATH%'", source.Script);

            var edit = new ShortcutWindow(config, dialog.Result) { Owner = owner };
            owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                ((CheckBox)edit.FindName("UacInput")).IsChecked = false;
                ((Button)edit.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
            Assert.True(edit.ShowDialog());
            Assert.False(edit.Result.RunAsAdministrator);
            Assert.True(dialog.Result.RunAsAdministrator);
        }
    }

    private static void VerifyTransferDialogs(MainWindow owner, Configuration config)
    {
        var export = new ExportWindow(config) { Owner = owner };
        export.Show();
        export.UpdateLayout();
        var checks = Descendants<CheckBox>(export).ToList();
        var all = checks.Single(check => Equals(check.Content, "Alle auswaehlen"));
        all.IsChecked = false;
        all.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
        Assert.Empty(export.SelectedIds);
        checks[2].IsChecked = true;
        Assert.Equal(config.Shortcuts[1].Id, Assert.Single(export.SelectedIds));
        export.Close();

        foreach (var overwrite in new[] { false, true })
        {
            var import = new ImportWindow(1) { Owner = owner };
            owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                if (overwrite) Descendants<RadioButton>(import).Last().IsChecked = true;
                Descendants<Button>(import).Single(button => button.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }));
            Assert.True(import.ShowDialog());
            Assert.Equal(overwrite ? ImportMode.Overwrite : ImportMode.SkipExisting, import.Mode);
        }
    }

    private static async Task VerifyNewTabFocusAndExecution(MainWindow window, TextBox filter, TabControl workspace)
    {
        window.Activate();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        filter.Focus();
        filter.Text = "Tool 12";
        ((Button)window.FindName("StartButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitUntilAsync(() => (workspace.SelectedItem as TabItem)?.Content is TerminalView);
        var terminalTab = (TabItem)workspace.SelectedItem;
        var terminal = (TerminalView)terminalTab.Content;
        await WaitUntilAsync(() => Descendants<TextBlock>(terminal).Any(text => text.Text.Contains("Tab schliessen:")));
        var web = Descendants<WebView2>(terminal).Single();
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            try
            {
                while (await web.ExecuteScriptAsync("document.hasFocus() && document.activeElement.classList.contains('xterm-helper-textarea')") != "true")
                    await Task.Delay(50, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                Assert.Fail($"Terminalfokus fehlt: aktiv={window.IsActive}, sichtbar={terminal.IsVisible}, WPF={Keyboard.FocusedElement?.GetType().Name}; " +
                    await web.ExecuteScriptAsync("JSON.stringify({focus:document.hasFocus(),element:document.activeElement.className})"));
            }
        }
        workspace.Items.Remove(terminalTab);
        await terminal.DisposeAsync();

        filter.Text = "Tool 09";
        ((Button)window.FindName("StartButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await WaitUntilAsync(() => (workspace.SelectedItem as TabItem)?.Content is ExecutionView);
        var execution = (ExecutionView)((TabItem)workspace.SelectedItem).Content;
        await WaitUntilAsync(() => !execution.IsRunning);
        var log = Descendants<TextBox>(execution).Single();
        Assert.True(log.IsReadOnly);
        Assert.Contains("LOG-OK", log.Text);
        Assert.Contains("[stderr] ERR-OK", log.Text);
        Assert.Contains("[Exitcode: 4]", log.Text);
    }

    private static void VerifyDuplication(MainWindow window, SettingsStore store)
    {
        var source = store.Load().Shortcuts[0];
        Exception? dialogFailure = null;
        window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = window.OwnedWindows.OfType<ShortcutWindow>().Single();
            try
            {
                Assert.Equal(source.Name + " (Kopie)", ((TextBox)dialog.FindName("NameInput")).Text);
                ((TextBox)dialog.FindName("NameInput")).Text = "Tool 01 Kopie";
                ((ComboBox)dialog.FindName("ProfileInput")).SelectedIndex = 1;
                ((CheckBox)dialog.FindName("LoadProfileInput")).IsChecked = true;
                ((Button)dialog.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            catch (Exception ex) { dialogFailure = ex; dialog.Close(); }
        }));
        ((Button)window.FindName("DuplicateButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Null(dialogFailure);
        var saved = store.Load();
        var copy = Assert.Single(saved.Shortcuts, shortcut => shortcut.Name == "Tool 01 Kopie");
        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal(source.WorkingDirectory, copy.WorkingDirectory);
        Assert.Equal(source.IconPath, copy.IconPath);
        Assert.Equal(saved.Profiles[1].Id, copy.ProfileId);
        Assert.True(copy.LoadProfile);
        var unchanged = saved.Shortcuts.Single(shortcut => shortcut.Id == source.Id);
        Assert.Equal(source.Name, unchanged.Name);
        Assert.Equal(source.ProfileId, unchanged.ProfileId);
        Assert.Equal(source.LoadProfile, unchanged.LoadProfile);

        window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            window.OwnedWindows.OfType<ShortcutWindow>().Single().Close()));
        ((Button)window.FindName("DuplicateButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Equal(saved.Shortcuts.Count, store.Load().Shortcuts.Count);
    }

    private static async Task VerifyBrowserKeyboardNavigation(MainWindow window, FileBrowserView browser, string navigation)
    {
        var files = (DataGrid)browser.FindName("Files");
        var path = (TextBox)browser.FindName("PathInput");
        await WaitUntilAsync(() => path.Text == navigation && files.IsKeyboardFocusWithin);
        Assert.Equal("Alpha", Assert.IsType<FileEntry>(files.SelectedItem).Name);
        Assert.True(Press(files, Key.Enter));
        await WaitUntilAsync(() => path.Text == Path.Combine(navigation, "Alpha") && files.IsKeyboardFocusWithin);
        Assert.Empty(files.Items);
        Assert.True(Press(files, Key.Back));
        await WaitUntilAsync(() => path.Text == navigation && files.IsKeyboardFocusWithin);
        Assert.Equal("Alpha", Assert.IsType<FileEntry>(files.SelectedItem).Name);
        File.WriteAllText(Path.Combine(navigation, "neu.txt"), "neu");
        Assert.True(Press(files, Key.F5));
        await WaitUntilAsync(() => files.Items.Cast<FileEntry>().Any(entry => entry.Name == "neu.txt") && files.IsKeyboardFocusWithin);
        Assert.Equal("Alpha", Assert.IsType<FileEntry>(files.SelectedItem).Name);
        Assert.True(Press(files, Key.Tab));
        Assert.False(files.IsKeyboardFocusWithin);
        files.Focus();

        _ = window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            var dialog = window.OwnedWindows.Cast<Window>().Single(w => w.Title == "Umbenennen");
            Descendants<TextBox>(dialog).Single().Text = "Gamma";
            Descendants<Button>(dialog).Single(button => button.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }));
        Assert.True(Press(files, Key.F2));
        await WaitUntilAsync(() => Directory.Exists(Path.Combine(navigation, "Gamma")) &&
            files.SelectedItem is FileEntry { Name: "Gamma" } && files.IsKeyboardFocusWithin);
        Assert.False(Directory.Exists(Path.Combine(navigation, "Alpha")));
    }

    private static bool Press(UIElement target, Key key)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(target), 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };
        target.RaiseEvent(args);
        return args.Handled;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition()) await Task.Delay(25, timeout.Token);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
