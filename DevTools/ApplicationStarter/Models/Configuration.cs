using System.Security.Principal;
using System.Text.Json.Serialization;

namespace ApplicationStarter.Models;

public enum ShortcutKind { Application, Terminal, FileBrowser, Execution, PowerShell5, PowerShell7 }

public sealed class UserProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string UserName { get; set; } = "";
    public bool IsCurrent { get; set; }
    [JsonIgnore] public string DisplayName => IsCurrent ? $"{Name} (aktuelles Konto)" : $"{Name} ({Environment.MachineName}\\{UserName})";
}

public sealed class Shortcut
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public ShortcutKind Kind { get; set; }
    public string ProfileId { get; set; } = "current";
    public string FileName { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public string IconPath { get; set; } = "";
    public bool LoadProfile { get; set; }
    public string Script { get; set; } = "";
    public bool Interactive { get; set; }
    public bool RunAsAdministrator { get; set; }
    [JsonIgnore] public bool IsPowerShellScript => Kind is ShortcutKind.PowerShell5 or ShortcutKind.PowerShell7;
    [JsonIgnore] public bool RequiresElevation => IsPowerShellScript && RunAsAdministrator;
    [JsonIgnore] public bool OpensTerminal => Kind == ShortcutKind.Terminal || IsPowerShellScript && Interactive;
    [JsonIgnore]
    public string KindName => Kind switch
    {
        ShortcutKind.Terminal => "Terminal",
        ShortcutKind.FileBrowser => "Dateibrowser",
        ShortcutKind.Execution => "Ausfuehrung",
        ShortcutKind.PowerShell5 => "PowerShell 5",
        ShortcutKind.PowerShell7 => "PowerShell 7",
        _ => "Anwendung"
    };
}

public sealed class Configuration
{
    public int Version { get; set; } = 1;
    public List<UserProfile> Profiles { get; set; } = [];
    public List<Shortcut> Shortcuts { get; set; } = [];

    public void UpdateCurrentProfile()
    {
        Profiles.RemoveAll(p => p.IsCurrent || p.Id == "current");
        Profiles.Insert(0, new UserProfile
        {
            Id = "current",
            IsCurrent = true,
            Name = WindowsIdentity.GetCurrent().Name
        });
    }

    public static Configuration CreateDefault()
    {
        var config = new Configuration();
        config.UpdateCurrentProfile();
        config.Shortcuts =
        [
            new() { Name = "PowerShell 7", Kind = ShortcutKind.Terminal,
                FileName = @"%ProgramW6432%\PowerShell\7\pwsh.exe", Arguments = "-NoLogo" },
            new() { Name = "Claude Desktop", FileName = @"%LOCALAPPDATA%\Microsoft\WindowsApps\claude-desktop.exe" },
            new() { Name = "Dateibrowser", Kind = ShortcutKind.FileBrowser, WorkingDirectory = "%USERPROFILE%" }
        ];
        return config;
    }
}
