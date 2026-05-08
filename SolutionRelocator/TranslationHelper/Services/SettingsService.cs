using System.IO;
using System.Text.Json;
using TranslationHelper.Models;

namespace TranslationHelper.Services;

public class SettingsService
{
    private static readonly string _settingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TranslationHelper");
    private static readonly string _settingsFile = Path.Combine(_settingsDir, "settings.json");

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsFile))
            {
                var json = File.ReadAllText(_settingsFile);
                return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(_settingsDir);
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(_settingsFile, json);
        }
        catch { }
    }
}
