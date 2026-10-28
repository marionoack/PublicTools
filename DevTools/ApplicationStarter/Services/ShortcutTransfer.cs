using System.Text.Json;
using System.Text.Json.Serialization;
using ApplicationStarter.Models;

namespace ApplicationStarter.Services;

public enum ImportMode { Overwrite, SkipExisting }
public sealed record ImportResult(Configuration Configuration, int Added, int Replaced, int Skipped, int AccountsAdded);

public sealed class ShortcutArchive
{
    public string Format { get; set; } = "ApplicationStarter.Shortcuts";
    public int Version { get; set; } = 1;
    public List<ExportedShortcut> Shortcuts { get; set; } = [];
}

public sealed class ExportedShortcut
{
    public string Account { get; set; } = "";
    public string AccountName { get; set; } = "";
    public bool CurrentAccount { get; set; }
    public Shortcut Shortcut { get; set; } = new();
}

public static class ShortcutTransfer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Export(Configuration config, IEnumerable<string> selectedIds)
    {
        var ids = selectedIds.ToHashSet(StringComparer.Ordinal);
        var archive = new ShortcutArchive();
        foreach (var shortcut in config.Shortcuts.Where(shortcut => ids.Contains(shortcut.Id)))
        {
            var profile = config.Profiles.Single(profile => profile.Id == shortcut.ProfileId);
            archive.Shortcuts.Add(new ExportedShortcut
            {
                Account = AccountKey(profile),
                AccountName = profile.Name,
                CurrentAccount = profile.IsCurrent,
                Shortcut = Clone(shortcut)
            });
        }
        return JsonSerializer.Serialize(archive, Options);
    }

    public static ShortcutArchive Parse(string json)
    {
        var archive = JsonSerializer.Deserialize<ShortcutArchive>(json, Options)
            ?? throw new InvalidDataException("Leere Importdatei.");
        if (archive.Format != "ApplicationStarter.Shortcuts" || archive.Version != 1 || archive.Shortcuts is null)
            throw new InvalidDataException("Unbekanntes oder unvollstaendiges Verknuepfungsformat.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Shortcuts)
        {
            if (entry is null || entry.Shortcut is null || string.IsNullOrWhiteSpace(entry.Account) ||
                string.IsNullOrWhiteSpace(entry.Shortcut.Name) || !Enum.IsDefined(entry.Shortcut.Kind) ||
                entry.Shortcut.FileName is null || entry.Shortcut.Arguments is null ||
                entry.Shortcut.WorkingDirectory is null || entry.Shortcut.IconPath is null || entry.Shortcut.Script is null ||
                (entry.Shortcut.IsPowerShellScript
                    ? string.IsNullOrWhiteSpace(entry.Shortcut.Script)
                    : entry.Shortcut.Kind != ShortcutKind.FileBrowser && string.IsNullOrWhiteSpace(entry.Shortcut.FileName)))
                throw new InvalidDataException("Eine importierte Verknuepfung ist unvollstaendig oder hat einen unbekannten Typ.");
            if (entry.CurrentAccount && !entry.Account.Contains('\\'))
                throw new InvalidDataException("Das aktuelle Konto muss als Domaene\\Benutzer angegeben sein.");
            if (!entry.CurrentAccount && (entry.Account.IndexOfAny(['\\', '/', '@', '\0']) >= 0))
                throw new InvalidDataException("Zusaetzliche Konten muessen lokale Benutzernamen ohne Domaene enthalten.");
            if (!seen.Add(entry.Shortcut.Name.Trim() + "\0" + entry.Account))
                throw new InvalidDataException("Die Importdatei enthaelt doppelte Verknuepfungsnamen fuer dasselbe Konto.");
        }
        return archive;
    }

    public static ImportResult Import(Configuration original, ShortcutArchive archive, ImportMode mode)
    {
        archive = Parse(JsonSerializer.Serialize(archive, Options));
        var config = JsonSerializer.Deserialize<Configuration>(JsonSerializer.Serialize(original))!;
        var added = 0;
        var replaced = 0;
        var skipped = 0;
        var accountsAdded = 0;
        foreach (var entry in archive.Shortcuts)
        {
            var profile = config.Profiles.FirstOrDefault(profile =>
                AccountKey(profile).Equals(entry.Account, StringComparison.OrdinalIgnoreCase));
            if (profile is null && entry.CurrentAccount)
            {
                var separator = entry.Account.IndexOf('\\');
                var domain = entry.Account[..separator];
                var user = entry.Account[(separator + 1)..];
                if (!domain.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Konto '{entry.Account}' kann hier nicht als lokales Konto wiederhergestellt werden.");
                profile = config.Profiles.FirstOrDefault(p => !p.IsCurrent && p.UserName.Equals(user, StringComparison.OrdinalIgnoreCase));
                entry.Account = user;
            }
            if (profile is null)
            {
                var localKey = Environment.MachineName + "\\" + entry.Account;
                profile = config.Profiles.FirstOrDefault(p => p.IsCurrent &&
                    p.Name.Equals(localKey, StringComparison.OrdinalIgnoreCase));
                if (profile is null)
                {
                    profile = new UserProfile
                    {
                        UserName = entry.Account,
                        Name = string.IsNullOrWhiteSpace(entry.AccountName) ? entry.Account : entry.AccountName
                    };
                    config.Profiles.Add(profile);
                    accountsAdded++;
                }
            }
            var imported = Clone(entry.Shortcut);
            imported.Name = imported.Name.Trim();
            imported.ProfileId = profile.Id;
            if (profile.IsCurrent) imported.LoadProfile = false;
            var existing = config.Shortcuts.FindIndex(shortcut => shortcut.ProfileId == profile.Id &&
                shortcut.Name.Equals(imported.Name, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                if (mode == ImportMode.SkipExisting) { skipped++; continue; }
                imported.Id = config.Shortcuts[existing].Id;
                config.Shortcuts[existing] = imported;
                replaced++;
            }
            else
            {
                imported.Id = Guid.NewGuid().ToString("N");
                config.Shortcuts.Add(imported);
                added++;
            }
        }
        return new ImportResult(config, added, replaced, skipped, accountsAdded);
    }

    private static string AccountKey(UserProfile profile) => profile.IsCurrent ? profile.Name : profile.UserName;
    private static Shortcut Clone(Shortcut shortcut) => JsonSerializer.Deserialize<Shortcut>(JsonSerializer.Serialize(shortcut))!;
}
