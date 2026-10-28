using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using TranslationHelper.Models;

namespace TranslationHelper.Services;

/// <summary>Persistiert Scopes inkl. Fachwörterbuch in %APPDATA%\TranslationHelper\scopes.json.</summary>
public class ScopeService
{
    private static readonly string _dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TranslationHelper");
    private static readonly string _file = Path.Combine(_dir, "scopes.json");

    private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public static string[] DefaultScopeNames => ["PLT Planung", "Allgemein", "Software-Installation", "Kommunikation"];

    public ObservableCollection<Scope> Load()
    {
        try
        {
            if (File.Exists(_file))
            {
                var json = File.ReadAllText(_file);
                var scopes = JsonSerializer.Deserialize<List<Scope>>(json, _jsonOptions);
                if (scopes != null)
                    return new ObservableCollection<Scope>(scopes);
            }
        }
        catch { }

        return new ObservableCollection<Scope>(DefaultScopeNames.Select(n => new Scope { Name = n }));
    }

    public void Save(IEnumerable<Scope> scopes)
    {
        var cleaned = scopes
            .Where(s => !string.IsNullOrWhiteSpace(s.Name))
            .Select(s => new Scope
            {
                Name = s.Name.Trim(),
                Entries = new ObservableCollection<GlossaryEntry>(s.Entries
                    .Where(e => !string.IsNullOrWhiteSpace(e.German) && !string.IsNullOrWhiteSpace(e.English))
                    .Select(e => new GlossaryEntry { German = e.German.Trim(), English = e.English.Trim() }))
            })
            .ToList();

        try
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(_file, JsonSerializer.Serialize(cleaned, _jsonOptions));
        }
        catch { }
    }
}
