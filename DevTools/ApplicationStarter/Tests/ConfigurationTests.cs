using System.Security.Cryptography;
using System.Text;
using ApplicationStarter.Models;
using ApplicationStarter.Services;

namespace ApplicationStarter.Tests;

public sealed class ConfigurationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ApplicationStarterTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void PasswordIsEncryptedBoundToProfileAndDetectsTampering()
    {
        var store = new SettingsStore(root);
        var id = Guid.NewGuid().ToString("N");
        const string password = "Local-Test-Password-987!";
        store.SavePassword(id, password);
        var encrypted = File.ReadAllBytes(Path.Combine(root, id + ".credential"));
        Assert.DoesNotContain(password, Encoding.Unicode.GetString(encrypted));
        var decrypted = store.ReadPassword(id);
        try { Assert.Equal(password, Encoding.Unicode.GetString(decrypted)); }
        finally { CryptographicOperations.ZeroMemory(decrypted); }
        encrypted[encrypted.Length / 2] ^= 0x80;
        File.WriteAllBytes(Path.Combine(root, id + ".credential"), encrypted);
        Assert.Throws<CryptographicException>(() => store.ReadPassword(id));
        store.DeletePassword(id);
        Assert.False(store.HasPassword(id));
    }

    [Fact]
    public void LoadProfileIconAndElevationBelongToEachShortcutNotTheAccount()
    {
        var store = new SettingsStore(root);
        var config = Configuration.CreateDefault();
        var profile = new UserProfile { Name = "Lokales Testkonto", UserName = "Testkonto" };
        config.Profiles.Add(profile);
        config.Shortcuts.Add(new Shortcut
        {
            ProfileId = profile.Id,
            Name = "Mit Profil",
            LoadProfile = true,
            IconPath = @"C:\test.ico",
            Kind = ShortcutKind.PowerShell5,
            Script = "Write-Output 'Test'",
            RunAsAdministrator = true
        });
        config.Shortcuts.Add(new Shortcut { ProfileId = profile.Id, Name = "Ohne Profil", LoadProfile = false });
        store.Save(config);
        var loaded = store.Load();
        Assert.True(loaded.Shortcuts.Single(s => s.Name == "Mit Profil").LoadProfile);
        Assert.False(loaded.Shortcuts.Single(s => s.Name == "Ohne Profil").LoadProfile);
        Assert.Equal(@"C:\test.ico", loaded.Shortcuts.Single(s => s.Name == "Mit Profil").IconPath);
        Assert.True(loaded.Shortcuts.Single(s => s.Name == "Mit Profil").RunAsAdministrator);
        Assert.False(loaded.Shortcuts.Single(s => s.Name == "Ohne Profil").RunAsAdministrator);
        Assert.Single(loaded.Profiles, p => p.IsCurrent);
        Assert.DoesNotContain("Password", File.ReadAllText(Path.Combine(root, "configuration.json")), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CorruptConfigurationIsNotSilentlyReplaced()
    {
        var store = new SettingsStore(root);
        var path = Path.Combine(root, "configuration.json");
        File.WriteAllText(path, "{broken");
        Assert.Throws<System.Text.Json.JsonException>(() => store.Load());
        Assert.Equal("{broken", File.ReadAllText(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
