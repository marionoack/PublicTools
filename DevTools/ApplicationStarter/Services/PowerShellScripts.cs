using System.Text;
using ApplicationStarter.Models;

namespace ApplicationStarter.Services;

public static class PowerShellScripts
{
    public static string ExecutablePath(ShortcutKind kind) => kind switch
    {
        ShortcutKind.PowerShell5 => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            @"System32\WindowsPowerShell\v1.0\powershell.exe"),
        ShortcutKind.PowerShell7 => Path.Combine(Environment.GetEnvironmentVariable("ProgramW6432")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"PowerShell\7\pwsh.exe"),
        _ => throw new ArgumentException("Keine PowerShell-Skriptverknuepfung.")
    };

    public static string Arguments(Shortcut shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut.Script)) throw new InvalidOperationException("Bitte einen PowerShell-Skripttext hinterlegen.");
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(shortcut.Script));
        if (shortcut.Interactive)
            return "-NoLogo -NoProfile -NoExit -EncodedCommand " + encoded;
        // Windows PowerShell uses OEM text by default and emits CLIXML errors with -EncodedCommand.
        var bootstrap = "[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)\r\n" +
            ". ([scriptblock]::Create([Text.Encoding]::Unicode.GetString([Convert]::FromBase64String('" + encoded + "'))))";
        return "-NoLogo -NoProfile -OutputFormat Text -NonInteractive -Command " + Native.Quote(bootstrap);
    }
}
