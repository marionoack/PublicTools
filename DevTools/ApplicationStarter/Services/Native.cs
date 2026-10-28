using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace ApplicationStarter.Services;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct StartupInfo
    {
        public int Size;
        public IntPtr Reserved, Desktop, Title;
        public int X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public short ShowWindow, Reserved2Size;
        public IntPtr Reserved2, StdInput, StdOutput, StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr AttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ProcessInfo
    {
        public IntPtr Process, Thread;
        public int ProcessId, ThreadId;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Coord
    {
        public short X, Y;
        public Coord(int x, int y) { X = (short)Math.Clamp(x, 1, 500); Y = (short)Math.Clamp(y, 1, 300); }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool CreateProcessW(string application, StringBuilder command, IntPtr processAttributes,
        IntPtr threadAttributes, bool inheritHandles, int flags, IntPtr environment,
        string directory, ref StartupInfoEx startup, out ProcessInfo process);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CreatePipe(out IntPtr read, out IntPtr write, IntPtr attributes, int size);
    [DllImport("kernel32.dll")]
    internal static extern int CreatePseudoConsole(Coord size, IntPtr input, IntPtr output, int flags, out IntPtr console);
    [DllImport("kernel32.dll")]
    internal static extern int ResizePseudoConsole(IntPtr console, Coord size);
    [DllImport("kernel32.dll")]
    internal static extern void ClosePseudoConsole(IntPtr console);
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value,
        IntPtr size, IntPtr previous, IntPtr returnSize);
    [DllImport("kernel32.dll")]
    internal static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint pid);
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint pid);
    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr block, IntPtr token, bool inherit);
    [DllImport("userenv.dll")]
    private static extern bool DestroyEnvironmentBlock(IntPtr block);
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    internal static extern uint AssocQueryStringW(uint flags, uint type, string association, string? extra,
        StringBuilder? output, ref uint length);

    internal static void Check(bool success)
    {
        if (!success) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    internal static string Quote(string value)
    {
        var result = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var c in value)
        {
            if (c == '\\') { backslashes++; continue; }
            if (c == '"') result.Append('\\', backslashes * 2 + 1).Append('"');
            else result.Append('\\', backslashes).Append(c);
            backslashes = 0;
        }
        return result.Append('\\', backslashes * 2).Append('"').ToString();
    }

    internal static Process StartAsUser(string executable, string arguments, string user, byte[] password, bool loadProfile)
    {
        using var secret = new SecureString();
        for (var index = 0; index < password.Length; index += 2)
            secret.AppendChar((char)(password[index] | password[index + 1] << 8));
        secret.MakeReadOnly();
        // Preserve the returned handle: reopening another user's process can be denied.
        return Process.Start(new ProcessStartInfo(executable, arguments)
        {
            UserName = user,
            Domain = Environment.MachineName,
            Password = secret,
            LoadUserProfile = loadProfile,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        }) ?? throw new IOException("Der Benutzerprozess konnte nicht gestartet werden.");
    }

    internal static ProcessStartInfo WorkerStartInfo(string pipeName, int parentId, bool elevate = false)
    {
        var assembly = typeof(Native).Assembly.Location;
        var executable = Path.ChangeExtension(assembly, ".exe");
        var arguments = $"--worker {pipeName} {parentId}";
        if (!File.Exists(executable))
        {
            executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe");
            arguments = Quote(assembly) + " " + arguments;
        }
        return new ProcessStartInfo(executable, arguments)
        {
            UseShellExecute = elevate,
            Verb = elevate ? "runas" : "",
            CreateNoWindow = !elevate,
            WorkingDirectory = AppContext.BaseDirectory
        };
    }

    internal static void RefreshUserEnvironment()
    {
        using var identity = WindowsIdentity.GetCurrent();
        Check(CreateEnvironmentBlock(out var block, identity.AccessToken.DangerousGetHandle(), false));
        try
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var offset = 0;
            while (true)
            {
                var entry = Marshal.PtrToStringUni(IntPtr.Add(block, offset))!;
                if (entry.Length == 0) break;
                offset += (entry.Length + 1) * 2;
                var equals = entry.IndexOf('=');
                if (equals > 0) values[entry[..equals]] = entry[(equals + 1)..];
            }
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                if (!values.ContainsKey((string)entry.Key)) Environment.SetEnvironmentVariable((string)entry.Key, null);
            foreach (var entry in values) Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        }
        finally { DestroyEnvironmentBlock(block); }
    }
}
