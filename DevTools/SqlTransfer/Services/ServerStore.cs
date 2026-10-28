using System.Text.Json;
using SqlTransfer.Models;

namespace SqlTransfer.Services;

/// <summary>Persistiert die Server-Pickliste (servers.json) und die App-Einstellungen (settings.json) in %AppData%\SqlTransfer.</summary>
public sealed class ServerStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _dir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SqlTransfer");

    private string ServersFile => Path.Combine(_dir, "servers.json");

    private string SettingsFile => Path.Combine(_dir, "settings.json");

    public List<ServerEntry> Servers { get; private set; } = [];

    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        Servers = Read<List<ServerEntry>>(ServersFile) ?? [];
        Settings = Read<AppSettings>(SettingsFile) ?? new AppSettings();
    }

    public void SaveServers(IEnumerable<ServerEntry> servers)
    {
        Servers = servers.ToList();
        Write(ServersFile, Servers);
    }

    public void SaveSettings() => Write(SettingsFile, Settings);

    private static T? Read<T>(string file)
    {
        try
        {
            return File.Exists(file) ? JsonSerializer.Deserialize<T>(File.ReadAllText(file)) : default;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return default;
        }
    }

    private void Write<T>(string file, T value)
    {
        Directory.CreateDirectory(_dir);
        var tmp = file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
        File.Move(tmp, file, overwrite: true);
    }
}
