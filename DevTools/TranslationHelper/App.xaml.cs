using System.Windows;
using TranslationHelper.Services;
using TranslationHelper.ViewModels;

namespace TranslationHelper;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settingsService = new SettingsService();
        var settings = settingsService.Load();

        var clipboard = new ClipboardService();
        var markdown = new MarkdownService();

        var viewModel = new MainViewModel(clipboard, markdown, settingsService, settings);

        var mainWindow = new MainWindow(viewModel);
        mainWindow.Show();
    }
}
