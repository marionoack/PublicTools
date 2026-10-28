using System.Windows;
using System.Windows.Controls;
using ApplicationStarter.Models;
using ApplicationStarter.Services;

namespace ApplicationStarter.Views;

public sealed class ExportWindow : Window
{
    private readonly List<(CheckBox Check, string Id)> choices = [];
    public List<string> SelectedIds => choices.Where(choice => choice.Check.IsChecked == true).Select(choice => choice.Id).ToList();

    public ExportWindow(Configuration config)
    {
        Title = "Verknuepfungen exportieren";
        Width = 620; Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(18) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var export = new Button { Content = "Auswahl exportieren...", IsDefault = true };
        export.Click += (_, _) =>
        {
            if (SelectedIds.Count == 0) { MessageBox.Show(this, "Bitte mindestens eine Verknuepfung auswaehlen."); return; }
            DialogResult = true;
        };
        buttons.Children.Add(export);
        buttons.Children.Add(new Button { Content = "Abbrechen", IsCancel = true });
        DockPanel.SetDock(buttons, Dock.Bottom); root.Children.Add(buttons);
        var header = new StackPanel();
        header.Children.Add(new TextBlock { Text = "Auswahl fuer JSON-Export (ohne Passwoerter)", FontSize = 18, Margin = new Thickness(0, 0, 0, 12) });
        var all = new CheckBox { Content = "Alle auswaehlen", IsChecked = true, Margin = new Thickness(4, 8, 4, 12) };
        all.Click += (_, _) => { foreach (var choice in choices) choice.Check.IsChecked = all.IsChecked; };
        header.Children.Add(all);
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var list = new StackPanel();
        foreach (var shortcut in config.Shortcuts)
        {
            var profile = config.Profiles.Single(profile => profile.Id == shortcut.ProfileId);
            var check = new CheckBox { Content = $"{shortcut.Name} - {profile.DisplayName}", IsChecked = true, Margin = new Thickness(4, 6, 4, 6) };
            choices.Add((check, shortcut.Id)); list.Children.Add(check);
        }
        root.Children.Add(new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
    }
}

public sealed class ImportWindow : Window
{
    public ImportMode Mode { get; private set; } = ImportMode.SkipExisting;

    public ImportWindow(int count)
    {
        Title = "Verknuepfungen importieren";
        Width = 590; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(new TextBlock
        {
            Text = $"{count} Verknuepfungen importieren. Gleicher Name und gleiches Konto gelten als vorhanden. Fehlende Kontokonfigurationen werden ohne Passwort angelegt.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16)
        });
        var skip = new RadioButton { Content = "Vorhandene ueberspringen (Skip Existing)", IsChecked = true, Margin = new Thickness(0, 6, 0, 6) };
        var overwrite = new RadioButton { Content = "Vorhandene ueberschreiben (Overwrite)", Margin = new Thickness(0, 6, 0, 12) };
        root.Children.Add(skip); root.Children.Add(overwrite);
        root.Children.Add(new TextBlock
        {
            Text = "Nur vertrauenswuerdige Dateien importieren. Importierte Programme werden nicht automatisch gestartet. Icon-Dateien werden nicht mitgeliefert.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var import = new Button { Content = "Importieren", IsDefault = true };
        import.Click += (_, _) => { Mode = overwrite.IsChecked == true ? ImportMode.Overwrite : ImportMode.SkipExisting; DialogResult = true; };
        buttons.Children.Add(import); buttons.Children.Add(new Button { Content = "Abbrechen", IsCancel = true });
        root.Children.Add(buttons); Content = root;
    }
}
