using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ApplicationStarter.Services;

internal sealed class PseudoTerminal : IDisposable
{
    private IntPtr console;
    private FileStream? input, output;
    private Process? process;
    private Task? pump;
    private bool disposed;
    private readonly SemaphoreSlim inputLock = new(1, 1);

    public void Start(string executable, string arguments, string directory, int columns, int rows,
        Func<string, Task> onOutput, Func<Task> onExit)
    {
        IntPtr inputRead = IntPtr.Zero, inputWrite = IntPtr.Zero, outputRead = IntPtr.Zero, outputWrite = IntPtr.Zero;
        IntPtr attributes = IntPtr.Zero;
        var initialized = false;
        try
        {
            Native.Check(Native.CreatePipe(out inputRead, out inputWrite, IntPtr.Zero, 0));
            Native.Check(Native.CreatePipe(out outputRead, out outputWrite, IntPtr.Zero, 0));
            Marshal.ThrowExceptionForHR(Native.CreatePseudoConsole(new Native.Coord(columns, rows), inputRead, outputWrite, 0, out console));
            var size = IntPtr.Zero;
            Native.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
            attributes = Marshal.AllocHGlobal(size);
            Native.Check(Native.InitializeProcThreadAttributeList(attributes, 1, 0, ref size));
            initialized = true;
            Native.Check(Native.UpdateProcThreadAttribute(attributes, 0, (IntPtr)0x00020016, console,
                (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero));
            var startup = new Native.StartupInfoEx
            {
                StartupInfo = new Native.StartupInfo { Size = Marshal.SizeOf<Native.StartupInfoEx>() },
                AttributeList = attributes
            };
            Native.Check(Native.CreateProcessW(executable, new StringBuilder(Native.Quote(executable) + " " + arguments),
                IntPtr.Zero, IntPtr.Zero, false, 0x00080000, IntPtr.Zero, directory, ref startup, out var info));
            try { process = Process.GetProcessById(info.ProcessId); }
            finally { Native.CloseHandle(info.Thread); Native.CloseHandle(info.Process); }
            input = new FileStream(new SafeFileHandle(inputWrite, true), FileAccess.Write);
            inputWrite = IntPtr.Zero;
            output = new FileStream(new SafeFileHandle(outputRead, true), FileAccess.Read);
            outputRead = IntPtr.Zero;
            pump = Task.Run(async () =>
            {
                var buffer = new byte[16384];
                try
                {
                    int count;
                    while ((count = await output.ReadAsync(buffer)) > 0)
                    {
                        // Keep draining ConPTY even if the UI disconnects, so closing it cannot deadlock.
                        try { await onOutput(Convert.ToBase64String(buffer, 0, count)); }
                        catch (IOException) { }
                        catch (ObjectDisposedException) { }
                    }
                }
                catch (IOException) { }
                catch (ObjectDisposedException) { }
            });
            _ = Task.Run(async () =>
            {
                try { await process.WaitForExitAsync(); await onExit(); }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException) { }
            });
        }
        catch { Dispose(); throw; }
        finally
        {
            if (initialized) Native.DeleteProcThreadAttributeList(attributes);
            if (attributes != IntPtr.Zero) Marshal.FreeHGlobal(attributes);
            foreach (var handle in new[] { inputRead, inputWrite, outputRead, outputWrite })
                if (handle != IntPtr.Zero) Native.CloseHandle(handle);
        }
    }

    public async Task WriteAsync(string text)
    {
        await inputLock.WaitAsync();
        try
        {
            if (input is not null)
            {
                await input.WriteAsync(Encoding.UTF8.GetBytes(text));
                await input.FlushAsync();
            }
        }
        finally { inputLock.Release(); }
    }

    public void Resize(int columns, int rows)
    {
        if (console != IntPtr.Zero) Marshal.ThrowExceptionForHR(Native.ResizePseudoConsole(console, new Native.Coord(columns, rows)));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { if (process is { HasExited: false }) process.Kill(true); } catch (InvalidOperationException) { }
        input?.Dispose();
        if (console != IntPtr.Zero) { Native.ClosePseudoConsole(console); console = IntPtr.Zero; }
        output?.Dispose();
        process?.Dispose();
    }
}
