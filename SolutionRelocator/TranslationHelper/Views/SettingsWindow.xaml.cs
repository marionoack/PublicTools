using System.Windows;
using TranslationHelper.Models;
using TranslationHelper.Services;

namespace TranslationHelper.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;

    public SettingsWindow(AppSettings settings, SettingsService settingsService)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;

        AccessKeyBox.Text = settings.AwsAccessKeyId;
        SecretKeyBox.Password = settings.AwsSecretKey;
        RegionBox.Text = settings.AwsRegion;
        ModelIdBox.Text = settings.ModelId;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.AwsAccessKeyId = AccessKeyBox.Text.Trim();
        _settings.AwsSecretKey = SecretKeyBox.Password;
        _settings.AwsRegion = RegionBox.Text.Trim();
        _settings.ModelId = ModelIdBox.Text.Trim();
        _settingsService.Save(_settings);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
