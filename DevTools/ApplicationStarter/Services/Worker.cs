using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using ApplicationStarter.Models;

namespace ApplicationStarter.Services;

internal static class Worker
{
    public static async Task<int> RunAsync(string pipeName, int parentId)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous,
                TokenImpersonationLevel.Identification);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await pipe.ConnectAsync(timeout.Token);
            Native.Check(Native.GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var serverId));
            if (serverId != parentId) return 2;
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
            using var sendLock = new SemaphoreSlim(1, 1);
            using var terminal = new PseudoTerminal();
            using var execution = new ExecutionProcess();
            var applications = new List<Process>();
            var terminalStarted = false;
            async Task Send(WireMessage message)
            {
                await sendLock.WaitAsync();
                try { await writer.WriteLineAsync(JsonSerializer.Serialize(message)); }
                finally { sendLock.Release(); }
            }
            try
            {
                while (await reader.ReadLineAsync() is { } line)
                {
                    var message = JsonSerializer.Deserialize<WireMessage>(line)!;
                    try
                    {
                        object result = true;
                        switch (message.Type)
                        {
                            case "identity":
                                if (message.Read<bool>()) Native.RefreshUserEnvironment();
                                result = ReadIdentity();
                                break;
                            case "elevate":
                                if (!ReadIdentity().IsAdministrator)
                                    throw new InvalidOperationException("Mit UAC ist nur fuer das ausgewaehlte Administratorkonto moeglich; ein Kontowechsel ist nicht erlaubt.");
                                var elevation = message.Read<ElevationRequest>();
                                try
                                {
                                    var elevatedProcess = Process.Start(Native.WorkerStartInfo(elevation.PipeName, elevation.ParentId, true))
                                        ?? throw new IOException("Der UAC-Prozess konnte nicht gestartet werden.");
                                    applications.Add(elevatedProcess);
                                    result = elevatedProcess.Id;
                                }
                                catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
                                {
                                    throw new IOException("Der UAC-Start wurde abgebrochen.", ex);
                                }
                                break;
                            case "launch":
                                applications.Add(FileOperations.Launch(message.Read<StartRequest>().Shortcut));
                                break;
                            case "terminal":
                                if (terminalStarted) throw new InvalidOperationException("Das Terminal wurde bereits gestartet.");
                                var start = message.Read<StartRequest>();
                                RequireElevation(start.Shortcut);
                                terminal.Start(FileOperations.Executable(start.Shortcut), FileOperations.Arguments(start.Shortcut),
                                    FileOperations.WorkingDirectory(start.Shortcut), start.Columns, start.Rows,
                                    data => Send(WireMessage.Create(0, "output", data)),
                                    () => Send(WireMessage.Create(0, "exit")));
                                terminalStarted = true;
                                break;
                            case "input": await terminal.WriteAsync(message.Read<string>()); break;
                            case "execute":
                                var executionShortcut = message.Read<StartRequest>().Shortcut;
                                RequireElevation(executionShortcut);
                                execution.Start(executionShortcut,
                                    data => Send(WireMessage.Create(0, "log", data)),
                                    code => Send(WireMessage.Create(0, "executionExit", code)));
                                break;
                            case "stopExecution": execution.Stop(); break;
                            case "resize":
                                var size = message.Read<SizeRequest>();
                                terminal.Resize(size.Columns, size.Rows);
                                break;
                            case "list": result = FileOperations.List(message.Read<string>()); break;
                            case "drives": result = FileOperations.Drives(); break;
                            case "icon": result = IconService.TargetIcon(message.Read<StartRequest>().Shortcut); break;
                            case "exists":
                                var path = FileOperations.Expand(message.Read<string>());
                                result = File.Exists(path) || Directory.Exists(path);
                                break;
                            case "file":
                                var application = FileOperations.Execute(message.Read<FileRequest>());
                                if (application is not null) applications.Add(application);
                                break;
                            default: throw new InvalidOperationException("Unbekannter Prozessauftrag.");
                        }
                        if (message.Id != 0) await Send(WireMessage.Create(message.Id, "response", result));
                    }
                    catch (Exception ex)
                    {
                        await Send(WireMessage.Create(message.Id, message.Id == 0 ? "error" : "response", null, ex.Message));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
            finally
            {
                terminal.Dispose();
                execution.Dispose();
                // Keep a loaded user hive alive while GUI processes started by this worker still use it.
                foreach (var application in applications)
                {
                    try { await application.WaitForExitAsync(); }
                    finally { application.Dispose(); }
                }
            }
            return 0;
        }
        catch { return 1; }
    }

    private static void RequireElevation(Shortcut shortcut)
    {
        if (!shortcut.RequiresElevation) return;
        using var identity = WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
            throw new InvalidOperationException("Diese PowerShell-Verknuepfung erfordert einen mit UAC erhoehten Benutzerprozess.");
    }

    private static WorkerIdentity ReadIdentity()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        var administratorSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null).Value;
        // WindowsIdentity.Groups omits deny-only SIDs in UAC-filtered tokens.
        var administrator = elevated || identity.Claims.Any(claim => claim.Value == administratorSid &&
            (claim.Type == ClaimTypes.GroupSid || claim.Type == ClaimTypes.DenyOnlySid));
        return new WorkerIdentity(identity.Name,
            Environment.GetEnvironmentVariable("USERPROFILE") ?? "",
            Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "", identity.User!.Value,
            administrator,
            elevated);
    }
}
