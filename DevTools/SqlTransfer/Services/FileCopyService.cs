using System.Diagnostics;

namespace SqlTransfer.Services;

public readonly record struct CopyProgress(long Copied, long Total, double BytesPerSecond)
{
    public TimeSpan? Remaining =>
        BytesPerSecond > 1 ? TimeSpan.FromSeconds((Total - Copied) / BytesPerSecond) : null;
}

public sealed class FileCopyService
{
    private const int BufferSize = 1 << 20;

    /// <summary>Prüft ein (evtl. langsam antwortendes) Netzwerkverzeichnis mit Timeout.</summary>
    public static Task<bool> DirectoryExistsAsync(string path, CancellationToken ct) =>
        ExistsAsync(() => Directory.Exists(path), ct);

    public static Task<bool> FileExistsAsync(string path, CancellationToken ct) =>
        ExistsAsync(() => File.Exists(path), ct);

    private static async Task<bool> ExistsAsync(Func<bool> check, CancellationToken ct)
    {
        try
        {
            return await Task.Run(check, ct).WaitAsync(TimeSpan.FromSeconds(20), ct);
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>Kopiert in eine .part-Datei und benennt am Ende um. Bei Abbruch oder Fehler wird die Teil-Datei gelöscht.</summary>
    public async Task<long> CopyAsync(string source, string target, IProgress<CopyProgress> progress, CancellationToken ct)
    {
        var temp = target + ".part";
        long total;
        try
        {
            await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize,
                             FileOptions.Asynchronous))
            {
                total = input.Length;
                var buffer = new byte[BufferSize];
                long copied = 0, lastBytes = 0;
                double speed = 0;
                var clock = Stopwatch.StartNew();
                var lastTick = TimeSpan.Zero;
                progress.Report(new CopyProgress(0, total, 0));

                int read;
                while ((read = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    copied += read;

                    var elapsed = clock.Elapsed - lastTick;
                    if (elapsed >= TimeSpan.FromMilliseconds(250))
                    {
                        var current = (copied - lastBytes) / elapsed.TotalSeconds;
                        speed = speed == 0 ? current : speed * 0.7 + current * 0.3;
                        lastBytes = copied;
                        lastTick = clock.Elapsed;
                        progress.Report(new CopyProgress(copied, total, speed));
                    }
                }

                await output.FlushAsync(ct).ConfigureAwait(false);
                progress.Report(new CopyProgress(copied, total, speed));
            }

            File.Move(temp, target, overwrite: true);
            return total;
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
