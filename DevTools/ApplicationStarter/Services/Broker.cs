using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using ApplicationStarter.Models;

namespace ApplicationStarter.Services;

public sealed class Broker : IAsyncDisposable
{
    private readonly NamedPipeServerStream pipe;
    private readonly StreamReader reader;
    private readonly StreamWriter writer;
    private readonly Process? process;
    private Broker? elevationOwner;
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<WireMessage>> requests = new();
    private int sequence;
    private bool disposed;
    public event Action<string, JsonElement>? EventReceived;
    public event Action<Exception>? Disconnected;
    public WorkerIdentity Identity { get; private set; } = new("", "", "");

    private Broker(NamedPipeServerStream pipe, Process? process)
    {
        this.pipe = pipe;
        this.process = process;
        reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, true);
        writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
        _ = ReadLoopAsync();
    }

    public static async Task<Broker> StartAsync(UserProfile profile, bool loadProfile, SettingsStore store, bool elevate = false)
    {
        var pipe = CreatePipe(profile, out var pipeName);
        Process? process = null;
        try
        {
            var startInfo = Native.WorkerStartInfo(pipeName, Environment.ProcessId);
            if (profile.IsCurrent)
            {
                process = Process.Start(startInfo)!;
            }
            else
            {
                var password = store.ReadPassword(profile.Id);
                try { process = Native.StartAsUser(startInfo.FileName, startInfo.Arguments, profile.UserName, password, loadProfile); }
                finally { CryptographicOperations.ZeroMemory(password); }
            }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var exited = new CancellationTokenSource();
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => { try { exited.Cancel(); } catch (ObjectDisposedException) { } };
            if (process.HasExited) exited.Cancel();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, exited.Token);
            while (true)
            {
                await pipe.WaitForConnectionAsync(linked.Token);
                Native.Check(Native.GetNamedPipeClientProcessId(pipe.SafePipeHandle, out var client));
                if (client == process.Id) break;
                pipe.Disconnect();
            }
            var broker = new Broker(pipe, process);
            try
            {
                using var identityTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                broker.Identity = await broker.RequestAsync<WorkerIdentity>("identity", !profile.IsCurrent, identityTimeout.Token);
                if (!elevate) return broker;
                if (!broker.Identity.IsAdministrator)
                    throw new InvalidOperationException("Mit UAC ist nur fuer ein Administratorkonto moeglich. " +
                        $"Das ausgewaehlte Konto '{broker.Identity.Name}' ist kein Administrator. Ein Kontowechsel ist nicht erlaubt.");
                return broker.Identity.IsElevated ? broker : await broker.ElevateAsync(profile);
            }
            catch { await broker.DisposeAsync(); throw; }
        }
        catch (Exception ex)
        {
            pipe.Dispose();
            if (process is not null)
            {
                try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { }
                process.Dispose();
            }
            if (ex is OperationCanceledException)
                throw new IOException("Der Benutzerprozess konnte nicht verbunden werden. Pruefen Sie Anmelderechte und den Zugriff des Zielkontos auf den Programmordner.", ex);
            throw;
        }
    }

    private static NamedPipeServerStream CreatePipe(UserProfile profile, out string pipeName)
    {
        pipeName = "ApplicationStarter-" + Guid.NewGuid().ToString("N");
        using var identity = WindowsIdentity.GetCurrent();
        var owner = identity.User!;
        var target = profile.IsCurrent ? owner :
            (SecurityIdentifier)new NTAccount(Environment.MachineName, profile.UserName).Translate(typeof(SecurityIdentifier));
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(true, false);
        security.SetOwner(owner);
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier(WellKnownSidType.NetworkSid, null),
            PipeAccessRights.FullControl, AccessControlType.Deny));
        security.AddAccessRule(new PipeAccessRule(owner, PipeAccessRights.FullControl, AccessControlType.Allow));
        if (target != owner)
            security.AddAccessRule(new PipeAccessRule(target, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        return NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous, 65536, 65536, security);
    }

    private async Task<Broker> ElevateAsync(UserProfile profile)
    {
        var elevatedPipe = CreatePipe(profile, out var pipeName);
        Broker? elevated = null;
        try
        {
            var elevatedId = await RequestAsync<int>("elevate", new ElevationRequest(pipeName, Environment.ProcessId));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (true)
            {
                await elevatedPipe.WaitForConnectionAsync(timeout.Token);
                Native.Check(Native.GetNamedPipeClientProcessId(elevatedPipe.SafePipeHandle, out var client));
                if (client == elevatedId) break;
                elevatedPipe.Disconnect();
            }
            elevated = new Broker(elevatedPipe, null) { elevationOwner = this };
            elevated.Identity = await elevated.RequestAsync<WorkerIdentity>("identity", !profile.IsCurrent, timeout.Token);
            elevated.Identity.EnsureElevatedFor(Identity.Sid);
            return elevated;
        }
        catch (Exception ex)
        {
            if (elevated is not null) await elevated.DisposeAsync();
            else elevatedPipe.Dispose();
            if (ex is OperationCanceledException)
                throw new IOException("Der UAC-Prozess konnte nicht verbunden werden. Im UAC-Dialog muss dasselbe ausgewaehlte Administratorkonto bestaetigt werden.", ex);
            throw;
        }
    }

    public async Task<T> RequestAsync<T>(string type, object? data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var id = Interlocked.Increment(ref sequence);
        var completion = new TaskCompletionSource<WireMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        requests[id] = completion;
        try
        {
            await SendAsync(WireMessage.Create(id, type, data));
            var result = await completion.Task.WaitAsync(cancellationToken);
            if (result.Error is not null) throw new IOException(result.Error);
            return result.Read<T>();
        }
        finally { requests.TryRemove(id, out _); }
    }

    public async Task NotifyAsync(string type, object? data)
    {
        if (!disposed) await SendAsync(WireMessage.Create(0, type, data));
    }

    private async Task SendAsync(WireMessage message)
    {
        await writeLock.WaitAsync();
        try { await writer.WriteLineAsync(JsonSerializer.Serialize(message)); }
        finally { writeLock.Release(); }
    }

    private async Task ReadLoopAsync()
    {
        Exception failure = new EndOfStreamException("Der Benutzerprozess wurde beendet.");
        try
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                var message = JsonSerializer.Deserialize<WireMessage>(line)!;
                if (message.Id != 0 && requests.TryRemove(message.Id, out var pending)) pending.TrySetResult(message);
                else EventReceived?.Invoke(message.Type, message.Error is null ? message.Data : JsonSerializer.SerializeToElement(message.Error));
            }
        }
        catch (Exception ex) { failure = ex; }
        finally
        {
            foreach (var pending in requests.Values) pending.TrySetException(failure);
            if (!disposed) Disconnected?.Invoke(failure);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        pipe.Dispose();
        if (elevationOwner is not null)
        {
            await elevationOwner.DisposeAsync();
            return;
        }
        if (process is null) return;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) { /* GUI applications keep their logon/profile owner alive. */ }
        process.Dispose();
    }
}
