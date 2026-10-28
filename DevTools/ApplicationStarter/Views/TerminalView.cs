using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ApplicationStarter.Models;
using ApplicationStarter.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ApplicationStarter.Views;

public sealed class TerminalView : UserControl, IAsyncDisposable
{
    private const string Origin = "https://terminal.applicationstarter.local";
    private readonly Broker broker;
    private readonly Shortcut shortcut;
    private readonly string dataFolder;
    private readonly WebView2 web = new();
    private readonly TextBlock status = new() { Margin = new Thickness(8), Text = "Terminal wird vorbereitet..." };
    private bool closed, started;
    public event Action<int>? ShortcutRequested;
    public event Action<int>? TabSwitchRequested;

    public TerminalView(Broker broker, Shortcut shortcut, string dataFolder)
    {
        this.broker = broker;
        this.shortcut = shortcut;
        this.dataFolder = dataFolder;
        var panel = new DockPanel();
        DockPanel.SetDock(status, Dock.Bottom);
        panel.Children.Add(status);
        panel.Children.Add(web);
        Content = panel;
        broker.EventReceived += OnWorkerEvent;
        broker.Disconnected += OnDisconnected;
        Loaded += Initialize;
    }

    private async void Initialize(object sender, RoutedEventArgs e)
    {
        Loaded -= Initialize;
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(null, System.IO.Path.Combine(dataFolder, "WebView2"));
            if (closed) return;
            await web.EnsureCoreWebView2Async(environment);
            if (closed) return;
            var core = web.CoreWebView2;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.SetVirtualHostNameToFolderMapping("terminal.applicationstarter.local",
                System.IO.Path.Combine(AppContext.BaseDirectory, "Terminal"), CoreWebView2HostResourceAccessKind.Deny);
            core.NavigationStarting += (_, navigation) =>
                navigation.Cancel = navigation.Uri != Origin + "/index.html";
            core.NewWindowRequested += (_, window) => window.Handled = true;
            core.PermissionRequested += (_, permission) => permission.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, download) => download.Cancel = true;
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += (_, request) =>
            {
                if (!request.Request.Uri.StartsWith(Origin + "/", StringComparison.Ordinal))
                    request.Response = environment.CreateWebResourceResponse(null, 403, "Forbidden", "");
            };
            core.WebMessageReceived += OnWebMessage;
            core.Navigate(Origin + "/index.html");
        }
        catch (Exception ex)
        {
            if (!closed) status.Text = "Terminal konnte nicht initialisiert werden. Microsoft Edge WebView2 Runtime pruefen. " + ex.Message;
        }
    }

    private async void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (closed || e.Source != Origin + "/index.html") return;
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            switch (root.GetProperty("type").GetString())
            {
                case "switchTab":
                    TabSwitchRequested?.Invoke(root.GetProperty("reverse").GetBoolean() ? -1 : 1);
                    break;
                case "shortcut":
                    var number = root.GetProperty("number").GetInt32();
                    if (number is >= 1 and <= 9) ShortcutRequested?.Invoke(number);
                    break;
                case "ready" when !started:
                    started = true;
                    await broker.RequestAsync<bool>("terminal", new StartRequest(shortcut,
                        root.GetProperty("cols").GetInt32(), root.GetProperty("rows").GetInt32()));
                    if (!closed) status.Text = $"Konto: {broker.Identity.Name} | Strg+C: Abbruch | Tab schliessen: Terminalprozesse beenden";
                    _ = Dispatcher.InvokeAsync(FocusInput, System.Windows.Threading.DispatcherPriority.ContextIdle);
                    break;
                case "input" when started:
                    await broker.NotifyAsync("input", root.GetProperty("data").GetString());
                    break;
                case "resize" when started:
                    await broker.NotifyAsync("resize", new SizeRequest(root.GetProperty("cols").GetInt32(), root.GetProperty("rows").GetInt32()));
                    break;
            }
        }
        catch (Exception ex) { if (!closed) status.Text = ex.Message; }
    }

    private void OnWorkerEvent(string type, JsonElement data)
    {
        if (type == "error")
        {
            Dispatcher.InvokeAsync(() => { if (!closed) status.Text = data.GetString(); });
            return;
        }
        if (type is not ("output" or "exit")) return;
        Dispatcher.InvokeAsync(() =>
        {
            if (closed || web.CoreWebView2 is null) return;
            web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
            {
                type,
                data = type == "output" ? data.GetString() : ""
            }));
        });
    }

    private void OnDisconnected(Exception exception) =>
        Dispatcher.InvokeAsync(() => { if (!closed) status.Text = exception.Message; });

    public void FocusInput()
    {
        if (closed || !IsLoaded || !IsVisible || Window.GetWindow(this)?.IsActive != true) return;
        web.Focus();
        if (started && web.CoreWebView2 is not null)
            web.CoreWebView2.PostWebMessageAsJson("{\"type\":\"focus\"}");
    }

    public async ValueTask DisposeAsync()
    {
        if (closed) return;
        closed = true;
        broker.EventReceived -= OnWorkerEvent;
        broker.Disconnected -= OnDisconnected;
        web.Dispose();
        await broker.DisposeAsync();
    }
}
