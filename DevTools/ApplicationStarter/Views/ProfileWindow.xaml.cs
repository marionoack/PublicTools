using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using ApplicationStarter.Models;
using ApplicationStarter.Services;

namespace ApplicationStarter.Views;

public partial class ProfileWindow : Window
{
    private readonly Configuration config;
    private readonly SettingsStore store;
    private UserProfile? selected;

    public ProfileWindow(Configuration config, SettingsStore store)
    {
        InitializeComponent();
        this.config = config;
        this.store = store;
        Refresh();
    }

    private void Refresh(string? id = null)
    {
        ProfilesList.ItemsSource = null;
        ProfilesList.ItemsSource = config.Profiles;
        ProfilesList.SelectedItem = config.Profiles.FirstOrDefault(p => p.Id == id) ?? config.Profiles[0];
    }

    private void Selection_Changed(object sender, SelectionChangedEventArgs e)
    {
        selected = ProfilesList.SelectedItem as UserProfile;
        NameInput.Text = selected?.Name ?? "";
        UserInput.Text = selected?.UserName ?? "";
        PasswordInput.Clear();
        var enabled = selected?.IsCurrent != true;
        NameInput.IsEnabled = UserInput.IsEnabled = PasswordInput.IsEnabled = SaveButton.IsEnabled = RemovePasswordButton.IsEnabled = enabled;
        PasswordStatus.Text = !enabled ? "Dieses Konto verwendet die bestehende Windows-Anmeldung."
            : selected is not null && store.HasPassword(selected.Id) ? "Passwort ist gespeichert." : "Kein Passwort gespeichert.";
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        ProfilesList.SelectedItem = null;
        selected = null;
        NameInput.Clear(); UserInput.Clear(); PasswordInput.Clear();
        NameInput.IsEnabled = UserInput.IsEnabled = PasswordInput.IsEnabled = SaveButton.IsEnabled = true;
        RemovePasswordButton.IsEnabled = false;
        PasswordStatus.Text = "Neues lokales Konto; das Windows-Konto muss bereits existieren.";
        NameInput.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var name = NameInput.Text.Trim();
            var user = UserInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(user) || user.Contains('\\') || user.Contains('@'))
                throw new ArgumentException("Anzeigename und lokaler Benutzername ohne Domaene sind erforderlich.");
            var sid = (SecurityIdentifier)new NTAccount(Environment.MachineName, user).Translate(typeof(SecurityIdentifier));
            if (sid == WindowsIdentity.GetCurrent().User)
                throw new ArgumentException("Fuer das aktuelle Windows-Konto bitte das vorhandene Standardprofil verwenden.");
            if (config.Profiles.Any(p => p.Id != selected?.Id && p.UserName.Equals(user, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Dieses lokale Konto ist bereits konfiguriert.");
            var profile = selected ?? new UserProfile();
            if (PasswordInput.Password.Length == 0 &&
                (!store.HasPassword(profile.Id) || !string.Equals(profile.UserName, user, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Fuer ein neues oder geaendertes Konto bitte das Passwort eingeben.");
            if (PasswordInput.Password.Length > 0) store.SavePassword(profile.Id, PasswordInput.Password);
            profile.Name = name;
            profile.UserName = user;
            if (selected is null) config.Profiles.Add(profile);
            store.Save(config);
            Refresh(profile.Id);
        }
        catch (Exception ex) { DialogHelpers.Error(this, ex); }
        finally { PasswordInput.Clear(); }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (selected is null || selected.IsCurrent) return;
        try
        {
            if (config.Shortcuts.Any(s => s.ProfileId == selected.Id))
                throw new InvalidOperationException("Dieses Konto wird noch von Verknuepfungen verwendet. Zuerst deren Kontozuordnung aendern.");
            if (!DialogHelpers.Confirm(this, $"Konto '{selected.Name}' und gespeichertes Passwort aus der Anwendung entfernen? Das Windows-Konto bleibt bestehen.")) return;
            config.Profiles.Remove(selected);
            store.Save(config);
            store.DeletePassword(selected.Id);
            Refresh();
        }
        catch (Exception ex) { DialogHelpers.Error(this, ex); }
    }

    private void RemovePassword_Click(object sender, RoutedEventArgs e)
    {
        if (selected is null || selected.IsCurrent) return;
        if (!DialogHelpers.Confirm(this, "Gespeichertes Passwort entfernen? Verknuepfungen mit diesem Konto funktionieren erst nach erneuter Passworteingabe.")) return;
        try { store.DeletePassword(selected.Id); Refresh(selected.Id); }
        catch (Exception ex) { DialogHelpers.Error(this, ex); }
    }
}
