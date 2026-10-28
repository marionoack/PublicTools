using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using ApplicationStarter.Models;
using ApplicationStarter.Services;

namespace ApplicationStarter.Tests;

public sealed class UacTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "ApplicationStarterTests-" + Guid.NewGuid().ToString("N"));

    private sealed class NonAdministratorFactAttribute : FactAttribute
    {
        public NonAdministratorFactAttribute()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var administratorSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Value;
            if (identity.Claims.Any(claim => claim.Value == administratorSid &&
                (claim.Type == ClaimTypes.GroupSid || claim.Type == ClaimTypes.DenyOnlySid)))
                Skip = "Dieser Test benoetigt ein Nichtadministratorkonto.";
        }
    }

    private sealed class UacFactAttribute : FactAttribute
    {
        public UacFactAttribute()
        {
            if (Environment.GetEnvironmentVariable("APPLICATIONSTARTER_TEST_UAC") != "1")
                Skip = "Explizit mit APPLICATIONSTARTER_TEST_UAC=1 aktivieren; erfordert eine echte Windows-UAC-Bestaetigung.";
        }
    }

    private sealed class SavedAdministratorFactAttribute : FactAttribute
    {
        public SavedAdministratorFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("APPLICATIONSTARTER_TEST_ADMIN_ACCOUNT")))
                Skip = "Explizit mit APPLICATIONSTARTER_TEST_ADMIN_ACCOUNT aktivieren; prueft ein gespeichertes Administratorkonto ohne UAC-Aufruf.";
        }
    }

    [SavedAdministratorFact]
    public async Task SavedAdministratorIsRecognizedBeforeUacRegardlessOfProfileLoading()
    {
        var store = new SettingsStore();
        var account = Environment.GetEnvironmentVariable("APPLICATIONSTARTER_TEST_ADMIN_ACCOUNT");
        var profile = Assert.Single(store.Load().Profiles, item => !item.IsCurrent &&
            string.Equals(item.UserName, account, StringComparison.OrdinalIgnoreCase));
        var expectedSid = ((SecurityIdentifier)new NTAccount(Environment.MachineName, profile.UserName)
            .Translate(typeof(SecurityIdentifier))).Value;
        foreach (var loadProfile in new[] { false, true })
        {
            await using var broker = await Broker.StartAsync(profile, loadProfile, store);
            Assert.Equal(expectedSid, broker.Identity.Sid);
            Assert.True(broker.Identity.IsAdministrator,
                $"Administratorkonto falsch erkannt: IsElevated={broker.Identity.IsElevated}, LoadProfile={loadProfile}.");
        }
    }

    [Theory]
    [InlineData(true, "S-1-5-21-1", "S-1-5-21-1", true)]
    [InlineData(false, "S-1-5-21-1", "S-1-5-21-1", false)]
    [InlineData(true, "S-1-5-21-1", "S-1-5-21-2", false)]
    [InlineData(true, "", "", false)]
    public void ElevationRequiresAnElevatedTokenAndTheOriginalAccountSid(bool elevated, string actualSid, string expectedSid, bool allowed)
    {
        var identity = new WorkerIdentity("Gleicher Anzeigename", "", "", actualSid, true, elevated);
        if (allowed) identity.EnsureElevatedFor(expectedSid);
        else Assert.Throws<IOException>(() => identity.EnsureElevatedFor(expectedSid));
    }

    [NonAdministratorFact]
    public async Task NonAdministratorCannotElevateOrRunUacScriptsInAnOrdinaryWorker()
    {
        var store = new SettingsStore(root);
        var profile = Configuration.CreateDefault().Profiles[0];
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Broker.StartAsync(profile, false, store, true));
        Assert.Contains("kein Administrator", error.Message);
        await using var broker = await Broker.StartAsync(profile, false, store);
        var marker = Path.Combine(root, "must-not-run.txt");
        foreach (var kind in new[] { ShortcutKind.PowerShell5, ShortcutKind.PowerShell7 })
        {
            foreach (var interactive in new[] { false, true })
            {
                var shortcut = new Shortcut
                {
                    Kind = kind,
                    Interactive = interactive,
                    RunAsAdministrator = true,
                    Script = "Set-Content -LiteralPath '" + marker.Replace("'", "''") + "' -Value 'FEHLER'"
                };
                var failure = await Assert.ThrowsAsync<IOException>(() =>
                    broker.RequestAsync<bool>(interactive ? "terminal" : "execute", new StartRequest(shortcut)));
                Assert.Contains("mit UAC erhoehten", failure.Message);
            }
        }
        await Assert.ThrowsAsync<IOException>(() => broker.RequestAsync<int>("elevate", new ElevationRequest("unused", Environment.ProcessId)));
        Assert.False(File.Exists(marker));
    }

    [UacFact]
    public async Task ConfirmedUacPreservesAccountAndElevatesPowerShellLogAndInteractiveTerminal()
    {
        var store = new SettingsStore();
        var account = Environment.GetEnvironmentVariable("APPLICATIONSTARTER_TEST_UAC_ACCOUNT");
        var profile = string.IsNullOrWhiteSpace(account) ? Configuration.CreateDefault().Profiles[0] :
            Assert.Single(store.Load().Profiles, item => !item.IsCurrent &&
                string.Equals(item.UserName, account, StringComparison.OrdinalIgnoreCase));
        var expectedSid = profile.IsCurrent ? WindowsIdentity.GetCurrent().User!.Value :
            ((SecurityIdentifier)new NTAccount(Environment.MachineName, profile.UserName).Translate(typeof(SecurityIdentifier))).Value;
        await using var broker = await Broker.StartAsync(profile, !profile.IsCurrent, store, true);
        broker.Identity.EnsureElevatedFor(expectedSid);
        var log = new StringBuilder();
        var terminal = new StringBuilder();
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continued = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminalMarker = $"UAC-TERMINAL-{expectedSid}-True-7";
        broker.EventReceived += (type, data) =>
        {
            if (type == "log") { lock (log) log.Append(data.Deserialize<ExecutionOutput>()!.Text); }
            if (type == "executionExit") exited.TrySetResult(data.GetInt32());
            if (type != "output") return;
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(data.GetString()!));
            lock (terminal)
            {
                terminal.Append(text);
                if (text.Contains("\u001b[6n")) _ = broker.NotifyAsync("input", "\u001b[1;1R");
                if (terminal.ToString().Contains(terminalMarker)) ready.TrySetResult();
                if (terminal.ToString().Contains("UAC-CONTINUED-42")) continued.TrySetResult();
            }
        };
        var identityScript = "$me = [Security.Principal.WindowsIdentity]::GetCurrent()\r\n" +
            "$admin = ([Security.Principal.WindowsPrincipal]::new($me)).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)\r\n";
        await broker.RequestAsync<bool>("execute", new StartRequest(new Shortcut
        {
            Kind = ShortcutKind.PowerShell5,
            RunAsAdministrator = true,
            Script = identityScript + "Write-Output ('UAC-LOG-' + $me.User.Value + '-' + $admin + '-' + $PSVersionTable.PSVersion.Major)\r\nexit 0"
        }));
        Assert.Equal(0, await exited.Task.WaitAsync(TimeSpan.FromSeconds(20)));
        lock (log) Assert.Contains($"UAC-LOG-{expectedSid}-True-5", log.ToString());
        await broker.RequestAsync<bool>("terminal", new StartRequest(new Shortcut
        {
            Kind = ShortcutKind.PowerShell7,
            RunAsAdministrator = true,
            Interactive = true,
            Script = identityScript + "$uacValue = 41\r\nWrite-Output ('UAC-TERMINAL-' + $me.User.Value + '-' + $admin + '-' + $PSVersionTable.PSVersion.Major)"
        }));
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(20));
        await broker.NotifyAsync("input", "Write-Output ('UAC-CONTINUED-' + ($uacValue + 1))\r");
        await continued.Task.WaitAsync(TimeSpan.FromSeconds(20));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
