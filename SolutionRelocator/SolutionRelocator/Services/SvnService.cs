using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace SolutionRelocator.Services;

public class SvnService
{
    public async Task<List<string>> FindWorkingCopiesAsync(string rootPath, int maxDepth)
    {
        var results = new List<string>();
        await ScanDirectoryAsync(rootPath, maxDepth, 0, results);
        return results;
    }

    private async Task ScanDirectoryAsync(string path, int maxDepth, int currentDepth, List<string> results)
    {
        if (currentDepth > maxDepth) return;

        try
        {
            var svnDir = Path.Combine(path, ".svn");
            if (Directory.Exists(svnDir))
            {
                results.Add(path);
                return; // Don't scan subdirectories of a working copy
            }

            foreach (var dir in Directory.GetDirectories(path))
            {
                await ScanDirectoryAsync(dir, maxDepth, currentDepth + 1, results);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
    }

    public async Task<(string url, string revision)> GetSvnInfoAsync(string localPath)
    {
        var output = await RunSvnCommandAsync($"info \"{localPath}\"");
        var url = ParseField(output, "URL");
        var revision = ParseField(output, "Revision");
        return (url, revision);
    }

    public async Task<(string revision, bool exists)> GetRemoteRevisionAsync(string svnUrl)
    {
        try
        {
            var output = await RunSvnCommandAsync($"info \"{svnUrl}\"");
            var revision = ParseField(output, "Revision");
            return (revision, !string.IsNullOrEmpty(revision));
        }
        catch
        {
            return (string.Empty, false);
        }
    }

    public async Task<(bool success, string message)> RelocateAsync(string localPath, string newUrl)
    {
        try
        {
            await RunSvnCommandAsync($"relocate \"{newUrl}\" \"{localPath}\"");
            return (true, "Erfolgreich");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<(bool success, string message)> SwitchAsync(string localPath, string newUrl)
    {
        try
        {
            await RunSvnCommandAsync($"switch \"{newUrl}\" \"{localPath}\"");
            return (true, "Erfolgreich");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static string ParseField(string output, string fieldName)
    {
        var match = Regex.Match(output, $@"^{Regex.Escape(fieldName)}:\s*(.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
    }

    private static async Task<string> RunSvnCommandAsync(string arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "svn",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error) ? $"svn exited with code {process.ExitCode}" : error.Trim());
        }

        return output;
    }
}
