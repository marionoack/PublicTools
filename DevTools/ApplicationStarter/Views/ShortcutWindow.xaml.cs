using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using ApplicationStarter.Models;
using ApplicationStarter.Services;
using Microsoft.Win32;

namespace ApplicationStarter.Views;

public partial class ShortcutWindow : Window
{
    public Shortcut Result { get; }

    public ShortcutWindow(Configuration config, Shortcut? shortcut = null, bool duplicate = false)
    {
        InitializeComponent();
        Result = shortcut is null ? new Shortcut() : JsonSerializer.Deserialize<Shortcut>(JsonSerializer.Serialize(shortcut))!;
        if (duplicate)
        {
            Result.Id = Guid.NewGuid().ToString("N");
            Result.Name += " (Kopie)";
            Title = "Verknuepfung duplizieren";
        }
        NameInput.Text = Result.Name;
        FileInput.Text = Result.FileName;
        ArgumentsInput.Text = Result.Arguments;
        DirectoryInput.Text = Result.WorkingDirectory;
        IconInput.Text = Result.IconPath;
        ScriptInput.Text = Result.Script;
        ScriptModeInput.SelectedIndex = Result.Interactive ? 1 : 0;
        UacInput.IsChecked = Result.RunAsAdministrator;
        LoadProfileInput.IsChecked = Result.LoadProfile;
        ProfileInput.ItemsSource = config.Profiles;
        ProfileInput.SelectedItem = config.Profiles.First(p => p.Id == Result.ProfileId);
        KindInput.SelectedIndex = (int)Result.Kind;
        Loaded += (_, _) => { NameInput.Focus(); NameInput.SelectAll(); };
    }

    private void Kind_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (FileInput is null || ScriptPanel is null) return;
        var kind = (ShortcutKind)KindInput.SelectedIndex;
        var script = kind is ShortcutKind.PowerShell5 or ShortcutKind.PowerShell7;
        ProgramPanel.Visibility = script ? Visibility.Collapsed : Visibility.Visible;
        ScriptPanel.Visibility = script ? Visibility.Visible : Visibility.Collapsed;
        FileInput.IsEnabled = ArgumentsInput.IsEnabled = kind != ShortcutKind.FileBrowser;
        if (script)
            InterpreterHint.Text = $"Interpreter: {PowerShellScripts.ExecutablePath(kind)}\nOhne PowerShell-Startskriptprofil. Windows-Profil laden wird getrennt eingestellt.";
    }

    private void Profile_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (LoadProfileInput is null) return;
        var current = (ProfileInput.SelectedItem as UserProfile)?.IsCurrent != false;
        LoadProfileInput.IsEnabled = !current;
        if (current) LoadProfileInput.IsChecked = false;
        ProfileHint.Text = current
            ? "Aktuelles Windows-Konto: Die bestehende Anmeldung wird verwendet; das Windows-Profil ist bereits geladen."
            : "Gilt nur fuer diese Verknuepfung, nicht fuer das Konto. Das Laden des Windows-Profils allein erhoeht keine Rechte.";
    }

    private void BrowseProgram_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Programme (*.exe)|*.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) FileInput.Text = dialog.FileName;
    }

    private void BrowseIcon_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Icon oder Programm|*.ico;*.exe", CheckFileExists = true };
        if (dialog.ShowDialog(this) == true) IconInput.Text = dialog.FileName;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(NameInput.Text) || ProfileInput.SelectedItem is not UserProfile profile)
                throw new ArgumentException("Name und Windows-Konto sind erforderlich.");
            var kind = (ShortcutKind)KindInput.SelectedIndex;
            if (!Enum.IsDefined(kind)) throw new ArgumentException("Bitte einen Typ auswaehlen.");
            var script = kind is ShortcutKind.PowerShell5 or ShortcutKind.PowerShell7;
            if (script && string.IsNullOrWhiteSpace(ScriptInput.Text))
                throw new ArgumentException("Bitte einen PowerShell-Skripttext eingeben.");
            if (!script && kind != ShortcutKind.FileBrowser && string.IsNullOrWhiteSpace(FileInput.Text))
                throw new ArgumentException("Bitte einen EXE-Pfad eingeben.");
            var icon = IconInput.Text.Trim();
            if (icon.Length > 0)
            {
                var expanded = Environment.ExpandEnvironmentVariables(icon);
                if (!System.IO.File.Exists(expanded) ||
                    !new[] { ".ico", ".exe" }.Contains(System.IO.Path.GetExtension(expanded).ToLowerInvariant()))
                    throw new ArgumentException("Das Icon muss eine vorhandene ICO- oder EXE-Datei sein.");
            }
            Result.Name = NameInput.Text.Trim();
            Result.Kind = kind;
            Result.ProfileId = profile.Id;
            Result.LoadProfile = !profile.IsCurrent && LoadProfileInput.IsChecked == true;
            Result.FileName = FileInput.Text.Trim();
            Result.Arguments = ArgumentsInput.Text;
            Result.WorkingDirectory = DirectoryInput.Text.Trim();
            Result.IconPath = icon;
            Result.Script = ScriptInput.Text;
            Result.Interactive = ScriptModeInput.SelectedIndex == 1;
            Result.RunAsAdministrator = script && UacInput.IsChecked == true;
            DialogResult = true;
        }
        catch (Exception ex) { DialogHelpers.Error(this, ex); }
    }
}
