using System.ComponentModel;
using System.IO;
using System.Windows;
using SolutionRelocator.Services;
using SolutionRelocator.ViewModels;

namespace SolutionRelocator;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var settings = SettingsService.Load();
        Width = settings.WindowWidth;
        Height = settings.WindowHeight;

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var pngPath = Path.Combine(baseDir, "svn_replace_base.png");
        var iconPath = Path.Combine(baseDir, "app.ico");
        IconGenerator.EnsureIconExists(iconPath, pngPath);
        try
        {
            Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath, UriKind.Absolute));
        }
        catch { }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.SaveSettings(Width, Height);
        }
    }
}
