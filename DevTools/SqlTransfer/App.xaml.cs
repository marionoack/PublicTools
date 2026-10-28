using System.Globalization;
using System.Windows;
using SqlTransfer.Infrastructure;
using SqlTransfer.Services;
using SqlTransfer.ViewModels;
using SqlTransfer.Views;

namespace SqlTransfer;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var store = new ServerStore();
        store.Load();

        var language = store.Settings.Language;
        if (string.IsNullOrEmpty(language))
            language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? "de" : "en";
        Loc.Instance.Language = language;
        Loc.Instance.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(Loc.Language))
            {
                store.Settings.Language = Loc.Instance.Language;
                store.SaveSettings();
            }
        };

        var window = new MainWindow(store, new MainViewModel(store));
        MainWindow = window;
        window.Show();
    }
}
