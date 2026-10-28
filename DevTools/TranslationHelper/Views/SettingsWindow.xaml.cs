using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using TranslationHelper.Models;
using TranslationHelper.Services;

namespace TranslationHelper.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly ScopeService _scopeService;
    private readonly ObservableCollection<Scope> _scopes;

    public SettingsWindow(AppSettings settings, SettingsService settingsService, ScopeService scopeService)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;
        _scopeService = scopeService;

        AccessKeyBox.Text = settings.AwsAccessKeyId;
        SecretKeyBox.Password = settings.AwsSecretKey;
        RegionBox.Text = settings.AwsRegion;
        ModelIdBox.Text = settings.ModelId;
        ModelIdPreciseBox.Text = settings.ModelIdPrecise;

        DefaultQualityBox.ItemsSource = new[]
        {
            new KeyValuePair<ModelQuality, string>(ModelQuality.Simple, "Einfach"),
            new KeyValuePair<ModelQuality, string>(ModelQuality.Precise, "Genau")
        };
        DefaultQualityBox.DisplayMemberPath = "Value";
        DefaultQualityBox.SelectedValuePath = "Key";
        DefaultQualityBox.SelectedValue = settings.DefaultQuality;

        // Arbeitskopie: Abbrechen verwirft Änderungen an Scopes und Wörterbuch.
        _scopes = _scopeService.Load();
        ScopeGrid.ItemsSource = _scopes;
        if (_scopes.Count > 0)
            ScopeGrid.SelectedIndex = 0;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Laufende Zelleneingaben übernehmen, bevor gespeichert wird.
        ScopeGrid.CommitEdit(DataGridEditingUnit.Row, true);
        GlossaryGrid.CommitEdit(DataGridEditingUnit.Row, true);

        _settings.AwsAccessKeyId = AccessKeyBox.Text.Trim();
        _settings.AwsSecretKey = SecretKeyBox.Password;
        _settings.AwsRegion = RegionBox.Text.Trim();
        _settings.ModelId = ModelIdBox.Text.Trim();
        _settings.ModelIdPrecise = ModelIdPreciseBox.Text.Trim();
        if (DefaultQualityBox.SelectedValue is ModelQuality quality)
            _settings.DefaultQuality = quality;
        _settingsService.Save(_settings);
        _scopeService.Save(_scopes);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
