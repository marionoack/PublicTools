using System.Security.Principal;
using System.Text;
using System.Text.Json;
using ApplicationStarter.Models;
using ApplicationStarter.Services;

namespace ApplicationStarter.Tests;

public sealed class ProcessAndFileTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ApplicationStarterTests-" + Guid.NewGuid().ToString("N"));
    private SettingsStore Store => new(Path.Combine(root, "settings"));
    private static UserProfile Current => Configuration.CreateDefault().Profiles[0];
    private static string PowerShell => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"PowerShell\7\pwsh.exe");

    private sealed class AlternateAccountFactAttribute : FactAttribute
    {
        public AlternateAccountFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPLICATIONSTARTER_TEST_ACCOUNT")))
                Skip = "Explizit mit APPLICATIONSTARTER_TEST_ACCOUNT und einem bereits gespeicherten lokalen Konto ausfuehren.";
        }
    }

    [AlternateAccountFact]
    public async Task AlternateAccountWorkerRetainsProcessHandleAndSupportsBothProfileModes()
    {
        var store = new SettingsStore();
        var account = Environment.GetEnvironmentVariable("APPLICATIONSTARTER_TEST_ACCOUNT");
        var profile = Assert.Single(store.Load().Profiles,
            item => !item.IsCurrent && string.Equals(item.UserName, account, StringComparison.OrdinalIgnoreCase));
        foreach (var loadProfile in new[] { false, true })
        {
            await using var broker = await Broker.StartAsync(profile, loadProfile, store);
            Assert.Equal($"{Environment.MachineName}\\{profile.UserName}", broker.Identity.Name, ignoreCase: true);
            var folder = Path.Combine(broker.Identity.LocalAppData, "Temp", "ApplicationStarterTests-" + Guid.NewGuid().ToString("N"));
            await broker.RequestAsync<bool>("file", new FileRequest("mkdir", folder));
            try
            {
                var source = Path.Combine(folder, "source.txt");
                var renamed = Path.Combine(folder, "renamed.txt");
                var copied = Path.Combine(folder, "copied.txt");
                var moved = Path.Combine(folder, "moved.txt");
                await broker.RequestAsync<bool>("file", new FileRequest("newfile", source));
                await broker.RequestAsync<bool>("file", new FileRequest("rename", source, renamed));
                await broker.RequestAsync<bool>("file", new FileRequest("copy", renamed, copied));
                await broker.RequestAsync<bool>("file", new FileRequest("move", copied, moved));
                await broker.RequestAsync<bool>("file", new FileRequest("delete", renamed));
                var listing = await broker.RequestAsync<DirectoryListing>("list", folder);
                Assert.Equal("moved.txt", Assert.Single(listing.Entries).Name);
            }
            finally { await broker.RequestAsync<bool>("file", new FileRequest("delete", folder)); }
        }
    }

    [Fact]
    public async Task CurrentAccountWorkerPerformsFileOperationsWithItsOwnIdentity()
    {
        await using var broker = await Broker.StartAsync(Current, false, Store);
        Assert.Equal(WindowsIdentity.GetCurrent().Name, broker.Identity.Name);
        var icon = await broker.RequestAsync<byte[]>("icon", new StartRequest(new Shortcut { FileName = PowerShell }));
        Assert.True(icon.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71 }));
        var work = Path.Combine(root, "files");
        await broker.RequestAsync<bool>("file", new FileRequest("mkdir", work));
        var source = Path.Combine(work, "source.txt");
        await broker.RequestAsync<bool>("file", new FileRequest("newfile", source));
        File.WriteAllText(source, "Inhalt");
        var hidden = Path.Combine(work, "hidden.txt");
        File.WriteAllText(hidden, "versteckt");
        File.SetAttributes(hidden, FileAttributes.Hidden);
        var listing = await broker.RequestAsync<DirectoryListing>("list", work);
        Assert.Contains(listing.Entries, entry => entry.Name == "source.txt" && entry.Size == 6);
        Assert.Contains(listing.Entries, entry => entry.Name == "hidden.txt" && entry.Hidden);
        var copy = Path.Combine(work, "copy.txt");
        await broker.RequestAsync<bool>("file", new FileRequest("copy", source, copy));
        Assert.Equal("Inhalt", File.ReadAllText(copy));
        await Assert.ThrowsAsync<IOException>(() => broker.RequestAsync<bool>("file", new FileRequest("copy", source, copy)));
        var renamed = Path.Combine(work, "renamed.txt");
        await broker.RequestAsync<bool>("file", new FileRequest("rename", copy, renamed));
        var moved = Path.Combine(work, "moved.txt");
        await broker.RequestAsync<bool>("file", new FileRequest("move", renamed, moved));
        Assert.False(File.Exists(renamed));
        Assert.True(File.Exists(moved));
        await broker.RequestAsync<bool>("file", new FileRequest("delete", moved));
        Assert.False(File.Exists(moved));
        Assert.NotEmpty(await broker.RequestAsync<List<FileEntry>>("drives", null));
    }

    [Theory]
    [InlineData("copy", "same")]
    [InlineData("move", "same")]
    [InlineData("copy", "child")]
    [InlineData("move", "parent")]
    public void CopyAndMoveRejectOverlappingPathsBeforeChangingFiles(string operation, string direction)
    {
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        var file = Path.Combine(source, "keep.txt");
        File.WriteAllText(file, "behalten");
        var target = direction == "same" ? source : direction == "child" ? Path.Combine(source, "nested") : root;
        Assert.Throws<IOException>(() => FileOperations.Execute(new FileRequest(operation, source, target, true)));
        Assert.Equal("behalten", File.ReadAllText(file));
        Assert.False(Directory.Exists(Path.Combine(source, "nested")));
    }

    [Fact]
    public void DirectoryCopyMoveAndExplicitOverwritePreserveContents()
    {
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        File.WriteAllText(Path.Combine(source, "nested", "file.txt"), "alt");
        var copied = Path.Combine(root, "copied");
        FileOperations.Execute(new FileRequest("copy", source, copied));
        File.WriteAllText(Path.Combine(source, "nested", "file.txt"), "neu");
        Assert.Throws<IOException>(() => FileOperations.Execute(new FileRequest("copy", source, copied)));
        FileOperations.Execute(new FileRequest("copy", source, copied, true));
        Assert.Equal("neu", File.ReadAllText(Path.Combine(copied, "nested", "file.txt")));
        var moved = Path.Combine(root, "moved");
        FileOperations.Execute(new FileRequest("move", copied, moved));
        Assert.False(Directory.Exists(copied));
        Assert.Equal("neu", File.ReadAllText(Path.Combine(moved, "nested", "file.txt")));
        FileOperations.Execute(new FileRequest("delete", moved));
        Assert.False(Directory.Exists(moved));
    }

    [Fact]
    public async Task ExternalProcessKeepsRunningAfterBrokerCloses()
    {
        var marker = Path.Combine(root, "launched.txt");
        await using (var broker = await Broker.StartAsync(Current, false, Store))
        {
            var shortcut = new Shortcut
            {
                FileName = PowerShell,
                WorkingDirectory = root,
                Arguments = $"-NoLogo -NoProfile -Command \"Start-Sleep -Milliseconds 700; [Security.Principal.WindowsIdentity]::GetCurrent().Name | Set-Content -LiteralPath '{marker.Replace("'", "''")}'\""
            };
            await broker.RequestAsync<bool>("launch", new StartRequest(shortcut));
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (!File.Exists(marker)) await Task.Delay(50, timeout.Token);
        Assert.Equal(WindowsIdentity.GetCurrent().Name, File.ReadAllText(marker).Trim());
    }

    [Fact]
    public async Task ExecutionCapturesBothStreamsExitCodeAndClosesStandardInput()
    {
        await using var broker = await Broker.StartAsync(Current, false, Store);
        var output = new List<ExecutionOutput>();
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        broker.EventReceived += (type, data) =>
        {
            if (type == "log") { lock (output) output.Add(data.Deserialize<ExecutionOutput>()!); }
            if (type == "executionExit") exited.TrySetResult(data.GetInt32());
        };
        var script = "[Console]::OutputEncoding=[Text.Encoding]::UTF8; [Console]::WriteLine('OUT-' + (20+2)); " +
            "[Console]::Error.WriteLine('ERR-' + (4+5)); if ([Console]::ReadLine() -eq $null) { [Console]::WriteLine('EOF') }; exit 7";
        await broker.RequestAsync<bool>("execute", new StartRequest(new Shortcut
        {
            Kind = ShortcutKind.Execution,
            FileName = PowerShell,
            WorkingDirectory = root,
            Arguments = "-NoProfile -EncodedCommand " + Convert.ToBase64String(Encoding.Unicode.GetBytes(script))
        }));
        Assert.Equal(7, await exited.Task.WaitAsync(TimeSpan.FromSeconds(15)));
        lock (output)
        {
            Assert.Contains("OUT-22", string.Concat(output.Where(item => item.Stream == "stdout").Select(item => item.Text)));
            Assert.Contains("EOF", string.Concat(output.Where(item => item.Stream == "stdout").Select(item => item.Text)));
            Assert.Contains("ERR-9", string.Concat(output.Where(item => item.Stream == "stderr").Select(item => item.Text)));
        }
    }

    [Fact]
    public async Task RunningExecutionCanBeStoppedWhileOutputIsBeingCaptured()
    {
        await using var broker = await Broker.StartAsync(Current, false, Store);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        broker.EventReceived += (type, data) =>
        {
            if (type == "log" && data.Deserialize<ExecutionOutput>()!.Text.Contains("READY")) ready.TrySetResult();
            if (type == "executionExit") exited.TrySetResult(data.GetInt32());
        };
        await broker.RequestAsync<bool>("execute", new StartRequest(new Shortcut
        {
            FileName = PowerShell,
            WorkingDirectory = root,
            Arguments = "-NoProfile -Command \"Write-Output 'READY'; Start-Sleep -Seconds 60\""
        }));
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await broker.RequestAsync<bool>("stopExecution", null);
        Assert.NotEqual(0, await exited.Task.WaitAsync(TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task ScriptLogCapturesNativePowerShell5And7WithoutAnExecutablePath()
    {
        foreach (var kind in new[] { ShortcutKind.PowerShell5, ShortcutKind.PowerShell7 })
        {
            await using var broker = await Broker.StartAsync(Current, false, Store);
            var output = new List<ExecutionOutput>();
            var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            broker.EventReceived += (type, data) =>
            {
                if (type == "log") { lock (output) output.Add(data.Deserialize<ExecutionOutput>()!); }
                if (type == "executionExit") exited.TrySetResult(data.GetInt32());
            };
            await broker.RequestAsync<bool>("execute", new StartRequest(new Shortcut
            {
                Kind = kind,
                WorkingDirectory = root,
                Script = "using namespace System.Text\r\nparam([string]$text = 'Gr\u00fc\u00dfe \u6f22\u5b57')\r\n" +
                    "Write-Output ($text + '|%LOCALAPPDATA%|\"quoted\"|VERSION-' + $PSVersionTable.PSVersion.Major)\r\n" +
                    "Write-Output ([StringBuilder]::new().Append('USING-OK').ToString())\r\n" +
                    "Write-Error 'SCRIPT-ERROR'\r\nexit 6"
            }));
            Assert.Equal(6, await exited.Task.WaitAsync(TimeSpan.FromSeconds(20)));
            lock (output)
            {
                var stdout = string.Concat(output.Where(item => item.Stream == "stdout").Select(item => item.Text));
                var stderr = string.Concat(output.Where(item => item.Stream == "stderr").Select(item => item.Text));
                Assert.Contains("Gr\u00fc\u00dfe \u6f22\u5b57|%LOCALAPPDATA%|\"quoted\"|VERSION-" + (kind == ShortcutKind.PowerShell5 ? 5 : 7), stdout);
                Assert.Contains("USING-OK", stdout);
                Assert.Contains("SCRIPT-ERROR", stderr);
                Assert.DoesNotContain("CLIXML", stderr);
            }
        }
    }

    [Theory]
    [InlineData(ShortcutKind.PowerShell5, 5)]
    [InlineData(ShortcutKind.PowerShell7, 7)]
    public async Task ScriptTerminalRunsScriptAndRemainsInteractiveWithItsVariables(ShortcutKind kind, int version)
    {
        await using var broker = await Broker.StartAsync(Current, false, Store);
        var received = new StringBuilder();
        var initial = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        broker.EventReceived += (type, data) =>
        {
            if (type != "output") return;
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(data.GetString()!));
            lock (received)
            {
                received.Append(text);
                if (text.Contains("\u001b[6n")) _ = broker.NotifyAsync("input", "\u001b[1;1R");
                if (received.ToString().Contains("SCRIPT-" + version)) initial.TrySetResult();
                if (received.ToString().Contains("AFTER-42")) continued.TrySetResult();
            }
        };
        await broker.RequestAsync<bool>("terminal", new StartRequest(new Shortcut
        {
            Kind = kind,
            Interactive = true,
            WorkingDirectory = root,
            Script = "$scriptValue = 40\r\nWrite-Output ('SCRIPT-' + $PSVersionTable.PSVersion.Major)"
        }));
        await initial.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await broker.NotifyAsync("input", "Write-Output ('AFTER-' + ($scriptValue + 2))\r");
        try { await continued.Task.WaitAsync(TimeSpan.FromSeconds(20)); }
        catch (TimeoutException) { Assert.Fail("Interaktives Skriptterminal: " + received); }
    }

    [Fact]
    public async Task ConPtySupportsPowerShellInputOutputAndResize()
    {
        await using var broker = await Broker.StartAsync(Current, false, Store);
        var received = new StringBuilder();
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        broker.EventReceived += (type, data) =>
        {
            if (type != "output") return;
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(data.GetString()!));
            lock (received)
            {
                received.Append(text);
                if (text.Contains("\u001b[6n")) _ = broker.NotifyAsync("input", "\u001b[1;1R");
                if (received.ToString().Contains("RESULT-15") && received.ToString().Contains("WIDTH-112"))
                    result.TrySetResult();
            }
        };
        await broker.RequestAsync<bool>("terminal", new StartRequest(new Shortcut
        {
            FileName = PowerShell,
            Arguments = "-NoLogo -NoProfile",
            WorkingDirectory = root
        }, 80, 24));
        await broker.RequestAsync<bool>("resize", new SizeRequest(112, 35));
        await broker.NotifyAsync("input", "Write-Output ('RESULT-' + (10+5)); Write-Output ('WIDTH-' + [Console]::WindowWidth)\r");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        try { await result.Task.WaitAsync(timeout.Token); }
        catch (OperationCanceledException) { Assert.Fail("ConPTY-Ausgabe: " + received); }
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
