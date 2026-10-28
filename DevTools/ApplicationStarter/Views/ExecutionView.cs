using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ApplicationStarter.Models;
using ApplicationStarter.Services;

namespace ApplicationStarter.Views;

public sealed class ExecutionView : UserControl, IAsyncDisposable
{
    private readonly Broker broker;
    private readonly Shortcut shortcut;
    private readonly TextBox log = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        FontFamily = new FontFamily("Consolas")
    };
    private readonly TextBlock status = new() { Margin = new Thickness(6), Text = "Ausfuehrung wird gestartet..." };
    private readonly Button stop = new() { Content = "Abbrechen" };
    private bool closed;
    public bool IsRunning { get; private set; } = true;

    public ExecutionView(Broker broker, Shortcut shortcut)
    {
        this.broker = broker; this.shortcut = shortcut;
        System.Windows.Automation.AutomationProperties.SetName(log, "Ausfuehrungslog");
        var root = new DockPanel();
        var toolbar = new DockPanel();
        DockPanel.SetDock(stop, Dock.Right); toolbar.Children.Add(stop); toolbar.Children.Add(status);
        DockPanel.SetDock(toolbar, Dock.Top); root.Children.Add(toolbar); root.Children.Add(log);
        Content = root;
        stop.Click += async (_, _) =>
        {
            try { await broker.RequestAsync<bool>("stopExecution", null); }
            catch (Exception ex) { status.Text = ex.Message; }
        };
        broker.EventReceived += OnEvent;
        broker.Disconnected += OnDisconnected;
        Loaded += Initialize;
    }

    private async void Initialize(object sender, RoutedEventArgs e)
    {
        Loaded -= Initialize;
        try
        {
            await broker.RequestAsync<bool>("execute", new StartRequest(shortcut));
            if (!closed && IsRunning) status.Text = $"Laeuft unter {broker.Identity.Name} (nicht interaktiv)";
        }
        catch (Exception ex)
        {
            if (!closed) { status.Text = ex.Message; IsRunning = false; stop.IsEnabled = false; }
        }
    }

    private void OnEvent(string type, JsonElement data)
    {
        if (type is not ("log" or "executionExit")) return;
        Dispatcher.InvokeAsync(() =>
        {
            if (closed) return;
            if (type == "log")
            {
                var output = data.Deserialize<ExecutionOutput>()!;
                log.AppendText(output.Stream == "stderr" ? "[stderr] " + output.Text : output.Text);
                log.ScrollToEnd();
            }
            else
            {
                IsRunning = false; stop.IsEnabled = false;
                status.Text = $"Beendet | Exitcode: {data.GetInt32()} | Konto: {broker.Identity.Name}";
                log.AppendText($"\r\n[Exitcode: {data.GetInt32()}]\r\n");
            }
        });
    }

    private void OnDisconnected(Exception ex) => Dispatcher.InvokeAsync(() =>
    {
        if (!closed) { status.Text = ex.Message; IsRunning = false; stop.IsEnabled = false; }
    });

    public void FocusInput() => log.Focus();

    public async ValueTask DisposeAsync()
    {
        if (closed) return;
        closed = true;
        broker.EventReceived -= OnEvent;
        broker.Disconnected -= OnDisconnected;
        await broker.DisposeAsync();
    }
}
