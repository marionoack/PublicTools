using System.Diagnostics;
using System.Text;
using ApplicationStarter.Models;

namespace ApplicationStarter.Services;

internal sealed class ExecutionProcess : IDisposable
{
    private Process? process;
    private Task? completion;

    public void Start(Shortcut shortcut, Func<ExecutionOutput, Task> onOutput, Func<int, Task> onExit)
    {
        if (process is not null) throw new InvalidOperationException("Eine Ausfuehrung wurde bereits gestartet.");
        process = Process.Start(new ProcessStartInfo(FileOperations.Executable(shortcut), FileOperations.Arguments(shortcut))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = FileOperations.WorkingDirectory(shortcut),
            StandardOutputEncoding = shortcut.IsPowerShellScript ? Encoding.UTF8 : null,
            StandardErrorEncoding = shortcut.IsPowerShellScript ? Encoding.UTF8 : null,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        }) ?? throw new IOException("Die Ausfuehrung konnte nicht gestartet werden.");
        process.StandardInput.Close();
        completion = ObserveAsync(process, onOutput, onExit);
    }

    private static async Task ObserveAsync(Process process, Func<ExecutionOutput, Task> onOutput, Func<int, Task> onExit)
    {
        async Task Pump(StreamReader reader, string stream)
        {
            var buffer = new char[4096];
            try
            {
                int count;
                while ((count = await reader.ReadAsync(buffer)) > 0)
                {
                    try { await onOutput(new ExecutionOutput(stream, new string(buffer, 0, count))); }
                    catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
                }
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException) { }
        }
        try
        {
            await Task.WhenAll(Pump(process.StandardOutput, "stdout"), Pump(process.StandardError, "stderr"), process.WaitForExitAsync());
            await onExit(process.ExitCode);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException) { }
    }

    public void Stop()
    {
        try { if (process is { HasExited: false }) process.Kill(true); }
        catch (InvalidOperationException) { }
    }

    public void Dispose()
    {
        Stop();
        process?.Dispose();
    }
}
