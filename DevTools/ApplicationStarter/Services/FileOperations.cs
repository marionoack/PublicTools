using System.Diagnostics;
using System.Text;
using ApplicationStarter.Models;

namespace ApplicationStarter.Services;

public static class FileOperations
{
    public static string Expand(string path) => Environment.ExpandEnvironmentVariables(path);

    public static DirectoryListing List(string path)
    {
        path = string.IsNullOrWhiteSpace(path) ? Environment.GetEnvironmentVariable("USERPROFILE") ?? @"C:\" : Expand(path);
        path = Path.GetFullPath(path);
        var entries = new DirectoryInfo(path).EnumerateFileSystemInfos().Select(item => new FileEntry(
            item.Name, item.FullName, (item.Attributes & FileAttributes.Directory) != 0,
            (item.Attributes & FileAttributes.Hidden) != 0,
            item is FileInfo file ? file.Length : null, item.LastWriteTime))
            .OrderByDescending(item => item.IsDirectory).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        return new DirectoryListing(path, entries);
    }

    public static List<FileEntry> Drives() => Directory.GetLogicalDrives()
        .Select(path => new FileEntry(path, path, true, false, null, DateTime.MinValue)).ToList();

    public static string Executable(Shortcut shortcut)
    {
        var path = shortcut.IsPowerShellScript ? PowerShellScripts.ExecutablePath(shortcut.Kind) : Expand(shortcut.FileName);
        if (!Path.IsPathFullyQualified(path))
            throw new IOException("Bitte einen absoluten EXE-Pfad konfigurieren; Umgebungsvariablen sind erlaubt.");
        if (!File.Exists(path)) throw new FileNotFoundException(
            $"Programm fuer das ausgewaehlte Konto nicht gefunden: {path}. Installation und Zugriff pruefen.", path);
        if (!string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Als Programm ist eine EXE-Datei erforderlich.");
        return path;
    }

    public static string WorkingDirectory(Shortcut shortcut)
    {
        var path = string.IsNullOrWhiteSpace(shortcut.WorkingDirectory)
            ? Environment.GetEnvironmentVariable("USERPROFILE") ?? @"C:\"
            : Expand(shortcut.WorkingDirectory);
        path = Path.GetFullPath(path);
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"Arbeitsverzeichnis nicht gefunden: {path}");
        return path;
    }

    public static Process Launch(Shortcut shortcut)
    {
        return Process.Start(new ProcessStartInfo(Executable(shortcut), Arguments(shortcut))
        {
            WorkingDirectory = WorkingDirectory(shortcut),
            UseShellExecute = false
        }) ?? throw new IOException("Der Prozess konnte nicht gestartet werden.");
    }

    public static string Arguments(Shortcut shortcut) => shortcut.IsPowerShellScript
        ? PowerShellScripts.Arguments(shortcut) : Expand(shortcut.Arguments);

    public static Process? Execute(FileRequest request)
    {
        var source = Path.GetFullPath(Expand(request.Path));
        switch (request.Operation)
        {
            case "mkdir": Directory.CreateDirectory(source); break;
            case "newfile": using (File.Open(source, FileMode.CreateNew, FileAccess.Write)) { } break;
            case "delete":
                RejectRoot(source);
                if (Directory.Exists(source)) Directory.Delete(source, true);
                else File.Delete(source);
                break;
            case "rename":
                RejectRoot(source);
                var renamed = Destination(request);
                if (!string.Equals(Path.GetDirectoryName(source), Path.GetDirectoryName(renamed), StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Umbenennen ist nur innerhalb desselben Ordners erlaubt.");
                if (Directory.Exists(source)) Directory.Move(source, renamed);
                else File.Move(source, renamed);
                break;
            case "copy":
            case "move":
                RejectRoot(source);
                var target = Destination(request);
                RejectNested(source, target);
                if (Directory.Exists(source))
                {
                    if (Directory.Exists(target) && !request.Overwrite) throw new IOException("Der Zielordner existiert bereits.");
                    CopyDirectory(source, target, request.Overwrite);
                    if (request.Operation == "move") Directory.Delete(source, true);
                }
                else
                {
                    RejectReparsePoint(source);
                    if (File.Exists(target)) RejectReparsePoint(target);
                    File.Copy(source, target, request.Overwrite);
                    if (request.Operation == "move") File.Delete(source);
                }
                break;
            case "open": return OpenFile(source);
            default: throw new InvalidOperationException("Unbekannte Dateioperation.");
        }
        return null;
    }

    private static string Destination(FileRequest request)
    {
        var path = Expand(request.Destination ?? throw new IOException("Zielpfad fehlt."));
        if (!Path.IsPathFullyQualified(path)) throw new IOException("Bitte einen vollstaendigen absoluten Zielpfad angeben.");
        return Path.GetFullPath(path);
    }

    private static void RejectRoot(string path)
    {
        if (string.Equals(Path.TrimEndingDirectorySeparator(path),
            Path.TrimEndingDirectorySeparator(Path.GetPathRoot(path)!), StringComparison.OrdinalIgnoreCase))
            throw new IOException("Diese Operation ist fuer ein Laufwerkswurzelverzeichnis nicht erlaubt.");
    }

    private static void RejectNested(string source, string destination)
    {
        var sourcePath = Path.TrimEndingDirectorySeparator(source);
        var destinationPath = Path.TrimEndingDirectorySeparator(destination);
        if (destinationPath.Equals(sourcePath, StringComparison.OrdinalIgnoreCase) ||
            destinationPath.StartsWith(sourcePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            sourcePath.StartsWith(destinationPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Quelle und Ziel sind gleich oder ineinander verschachtelt.");
        // Parent junctions can make different path strings refer to the same directory.
        for (var parent = Path.GetDirectoryName(destinationPath); parent is not null; parent = Path.GetDirectoryName(parent))
            if (Directory.Exists(parent)) RejectReparsePoint(parent);
        for (var parent = Path.GetDirectoryName(sourcePath); parent is not null; parent = Path.GetDirectoryName(parent))
            if (Directory.Exists(parent)) RejectReparsePoint(parent);
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Verknuepfungen/Junctions werden beim Kopieren und Verschieben nicht verfolgt.");
    }

    private static void CopyDirectory(string source, string target, bool overwrite)
    {
        RejectReparsePoint(source);
        if (Directory.Exists(target)) RejectReparsePoint(target);
        Directory.CreateDirectory(target);
        foreach (var entry in new DirectoryInfo(source).EnumerateFileSystemInfos())
        {
            RejectReparsePoint(entry.FullName);
            var destination = Path.Combine(target, entry.Name);
            if (entry is DirectoryInfo) CopyDirectory(entry.FullName, destination, overwrite);
            else
            {
                if (File.Exists(destination)) RejectReparsePoint(destination);
                File.Copy(entry.FullName, destination, overwrite);
            }
        }
    }

    private static Process OpenFile(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Datei nicht gefunden.", path);
        var extension = Path.GetExtension(path);
        if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return Process.Start(new ProcessStartInfo(path) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(path)! })
                ?? throw new IOException("Die Datei konnte nicht gestartet werden.");
        }
        uint length = 0;
        Native.AssocQueryStringW(0, 2, extension, "open", null, ref length);
        if (length == 0) throw new IOException("Unter diesem Konto ist keine direkt startbare Anwendung fuer diesen Dateityp zugeordnet.");
        var buffer = new StringBuilder((int)length);
        var result = Native.AssocQueryStringW(0, 2, extension, "open", buffer, ref length);
        if (result != 0) throw new IOException("Die Dateizuordnung des ausgewaehlten Kontos konnte nicht ermittelt werden.");
        // Do not use ShellExecute/DDE: they can delegate to another user's existing shell.
        return Process.Start(new ProcessStartInfo(buffer.ToString(), Native.Quote(path))
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(path)!
        }) ?? throw new IOException("Die Anwendung konnte nicht gestartet werden.");
    }
}
