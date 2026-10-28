using System.Text.Json;
using ApplicationStarter.Models;
using ApplicationStarter.Services;

namespace ApplicationStarter.Tests;

public sealed class ShortcutTransferTests
{
    [Theory]
    [InlineData(ShortcutKind.PowerShell5, false)]
    [InlineData(ShortcutKind.PowerShell7, true)]
    public void ScriptTextAndModeRoundTripWithoutRequiringAnExecutable(ShortcutKind kind, bool interactive)
    {
        var config = Configuration.CreateDefault();
        var script = new Shortcut
        {
            Name = "Direktes Skript",
            Kind = kind,
            Interactive = interactive,
            RunAsAdministrator = true,
            Script = "# Kommentar\r\nWrite-Output 'Gr\u00fc\u00dfe %PATH%'\r\n$env:USERPROFILE",
            WorkingDirectory = "%USERPROFILE%"
        };
        config.Shortcuts.Add(script);
        var archive = ShortcutTransfer.Parse(ShortcutTransfer.Export(config, [script.Id]));
        var target = Configuration.CreateDefault();
        var result = ShortcutTransfer.Import(target, archive, ImportMode.SkipExisting);
        var imported = result.Configuration.Shortcuts[^1];
        Assert.Equal(kind, imported.Kind);
        Assert.Equal(interactive, imported.Interactive);
        Assert.True(imported.RunAsAdministrator);
        Assert.Equal(script.Script, imported.Script);
        Assert.Equal(script.WorkingDirectory, imported.WorkingDirectory);
        Assert.Empty(imported.FileName);
        archive.Shortcuts[0].Shortcut.Script = "  ";
        Assert.Throws<InvalidDataException>(() => ShortcutTransfer.Import(target, archive, ImportMode.Overwrite));
    }

    [Fact]
    public void SelectiveExportPreservesOrderAndSettingsWithoutCredentialStorage()
    {
        var config = Configuration.CreateDefault();
        var profile = new UserProfile { Name = "Agent", UserName = "local.agent" };
        config.Profiles.Add(profile);
        config.Shortcuts.Add(new Shortcut
        {
            Name = "Pruefung",
            Kind = ShortcutKind.Execution,
            ProfileId = profile.Id,
            FileName = @"C:\Program Files\PowerShell\7\pwsh.exe",
            Arguments = "-NoProfile",
            WorkingDirectory = "%USERPROFILE%",
            LoadProfile = true,
            IconPath = @"C:\test.ico"
        });
        var json = ShortcutTransfer.Export(config, [config.Shortcuts[3].Id, config.Shortcuts[1].Id]);
        var archive = ShortcutTransfer.Parse(json);
        Assert.Equal(["Claude Desktop", "Pruefung"], archive.Shortcuts.Select(entry => entry.Shortcut.Name));
        Assert.Equal("local.agent", archive.Shortcuts[1].Account);
        Assert.True(archive.Shortcuts[1].Shortcut.LoadProfile);
        Assert.Equal(ShortcutKind.Execution, archive.Shortcuts[1].Shortcut.Kind);
        Assert.DoesNotContain("Password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("credential", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ImportMode.SkipExisting)]
    [InlineData(ImportMode.Overwrite)]
    public void ImportMatchesAccountAndNameCaseInsensitivelyAndPreservesExistingIdAndPosition(ImportMode mode)
    {
        var config = Configuration.CreateDefault();
        var profile = new UserProfile { Name = "Agent lokal", UserName = "local.agent" };
        config.Profiles.Add(profile);
        var existing = new Shortcut { Name = "Werkzeug", ProfileId = profile.Id, FileName = @"C:\old.exe" };
        config.Shortcuts.Insert(1, existing);
        var imported = new Shortcut { Name = "WERKZEUG", FileName = @"C:\new.exe", LoadProfile = true };
        var archive = new ShortcutArchive
        {
            Shortcuts = [new ExportedShortcut { Account = "LOCAL.AGENT", AccountName = "Anderer Anzeigename", Shortcut = imported }]
        };
        var result = ShortcutTransfer.Import(config, archive, mode);
        Assert.Equal(0, result.Added);
        Assert.Equal(0, result.AccountsAdded);
        Assert.Equal(existing.Id, result.Configuration.Shortcuts[1].Id);
        Assert.Equal(profile.Id, result.Configuration.Shortcuts[1].ProfileId);
        Assert.Equal("Agent lokal", result.Configuration.Profiles[1].Name);
        Assert.Equal(mode == ImportMode.Overwrite ? @"C:\new.exe" : @"C:\old.exe", result.Configuration.Shortcuts[1].FileName);
        Assert.Equal(mode == ImportMode.Overwrite ? 1 : 0, result.Replaced);
        Assert.Equal(mode == ImportMode.SkipExisting ? 1 : 0, result.Skipped);
        Assert.Equal(@"C:\old.exe", config.Shortcuts[1].FileName);
    }

    [Fact]
    public void ImportCreatesMissingAccountWithoutCredentialsAndDoesNotConfuseSameNameForAnotherAccount()
    {
        var config = Configuration.CreateDefault();
        var archive = new ShortcutArchive
        {
            Shortcuts = [new ExportedShortcut
            {
                Account = "local.new", AccountName = "Neues Konto",
                Shortcut = new Shortcut { Name = config.Shortcuts[0].Name, FileName = @"C:\tool.exe", LoadProfile = true }
            }]
        };
        var result = ShortcutTransfer.Import(config, archive, ImportMode.Overwrite);
        Assert.Equal(1, result.AccountsAdded);
        Assert.Equal(1, result.Added);
        Assert.Equal(0, result.Replaced);
        var profile = result.Configuration.Profiles[1];
        Assert.Equal("local.new", profile.UserName);
        Assert.False(profile.IsCurrent);
        Assert.Equal(profile.Id, result.Configuration.Shortcuts[^1].ProfileId);
        Assert.NotEqual(archive.Shortcuts[0].Shortcut.Id, result.Configuration.Shortcuts[^1].Id);
        Assert.Single(config.Profiles);
    }

    [Fact]
    public void RoundTripRestoresCurrentAccountWithoutCreatingAnotherLogin()
    {
        var config = Configuration.CreateDefault();
        config.Shortcuts[0].LoadProfile = true;
        var archive = ShortcutTransfer.Parse(ShortcutTransfer.Export(config, [config.Shortcuts[0].Id]));
        var result = ShortcutTransfer.Import(config, archive, ImportMode.Overwrite);
        Assert.Equal(0, result.AccountsAdded);
        Assert.Equal("current", result.Configuration.Shortcuts[0].ProfileId);
        Assert.False(result.Configuration.Shortcuts[0].LoadProfile);
    }

    [Fact]
    public void InvalidArchiveDoesNotPartiallyMutateConfiguration()
    {
        var config = Configuration.CreateDefault();
        var before = JsonSerializer.Serialize(config);
        var archive = new ShortcutArchive
        {
            Shortcuts =
            [
                new() { Account = "valid", Shortcut = new() { Name = "A", FileName = @"C:\tool.exe" } },
                new() { Account = "domain\\not-local", Shortcut = new() { Name = "B", FileName = @"C:\tool.exe" } }
            ]
        };
        Assert.Throws<InvalidDataException>(() => ShortcutTransfer.Import(config, archive, ImportMode.Overwrite));
        Assert.Equal(before, JsonSerializer.Serialize(config));
        Assert.Throws<InvalidDataException>(() => ShortcutTransfer.Parse("{\"Format\":\"Other\",\"Version\":1}"));
    }
}
