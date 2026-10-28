using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using ApplicationStarter.Models;

namespace ApplicationStarter.Services;

public sealed class SettingsStore
{
    public string Root { get; }
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SettingsStore(string? root = null)
    {
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ApplicationStarter");
        Directory.CreateDirectory(Root);
        var sid = WindowsIdentity.GetCurrent().User!;
        var security = new DirectorySecurity();
        security.SetOwner(sid);
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(Root).SetAccessControl(security);
    }

    public Configuration Load()
    {
        var path = Path.Combine(Root, "configuration.json");
        if (!File.Exists(path)) return Configuration.CreateDefault();
        var config = JsonSerializer.Deserialize<Configuration>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Die Konfiguration ist leer.");
        if (config.Version != 1) throw new InvalidDataException("Unbekannte Konfigurationsversion.");
        if (config.Profiles is null || config.Shortcuts is null)
            throw new InvalidDataException("Die Konfiguration ist unvollstaendig.");
        config.UpdateCurrentProfile();
        if (config.Profiles.Select(p => p.Id).Distinct().Count() != config.Profiles.Count ||
            config.Shortcuts.Select(s => s.Id).Distinct().Count() != config.Shortcuts.Count ||
            config.Shortcuts.Any(s => !config.Profiles.Any(p => p.Id == s.ProfileId)))
            throw new InvalidDataException("Die Konfiguration enthaelt ungueltige Profilzuordnungen oder doppelte IDs.");
        return config;
    }

    public void Save(Configuration config) =>
        AtomicWrite(Path.Combine(Root, "configuration.json"), JsonSerializer.SerializeToUtf8Bytes(config, JsonOptions));

    private string SecretPath(string id)
    {
        if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Ungueltige Profil-ID.");
        return Path.Combine(Root, id + ".credential");
    }

    public bool HasPassword(string id) => File.Exists(SecretPath(id));

    public void SavePassword(string id, string password)
    {
        var bytes = Encoding.Unicode.GetBytes(password);
        try
        {
            var encrypted = ProtectedData.Protect(bytes, Encoding.UTF8.GetBytes(id), DataProtectionScope.CurrentUser);
            AtomicWrite(SecretPath(id), encrypted);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public byte[] ReadPassword(string id)
    {
        var path = SecretPath(id);
        if (!File.Exists(path)) throw new InvalidOperationException("Fuer dieses Konto ist kein Passwort gespeichert.");
        return ProtectedData.Unprotect(File.ReadAllBytes(path), Encoding.UTF8.GetBytes(id), DataProtectionScope.CurrentUser);
    }

    public void DeletePassword(string id) => File.Delete(SecretPath(id));

    private static void AtomicWrite(string path, byte[] bytes)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
