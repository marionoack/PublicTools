using System.Windows;
using ApplicationStarter.Services;

namespace ApplicationStarter;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--worker" && int.TryParse(args[2], out var parentId))
            return Worker.RunAsync(args[1], parentId).GetAwaiter().GetResult();

        var app = new App();
        app.InitializeComponent();
        app.DispatcherUnhandledException += (_, e) =>
        {
            MessageBox.Show(e.Exception.Message, "Application Starter", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
        try { return app.Run(new MainWindow()); }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Application Starter", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }
}
