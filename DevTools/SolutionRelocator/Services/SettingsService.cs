using System.IO;
using System.Text.Json;
using SolutionRelocator.Models;

namespace SolutionRelocator.Services;

public class AppSettings
{
    public string RootPath { get; set; } = string.Empty;
    public int MaxDepth { get; set; } = 3;
    public string SearchText { get; set; } = string.Empty;
    public string ReplaceText { get; set; } = string.Empty;
    public SearchMode SearchMode { get; set; } = SearchMode.Contains;
    public double WindowWidth { get; set; } = 1100;
    public double WindowHeight { get; set; } = 650;
}

public static class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }
}
