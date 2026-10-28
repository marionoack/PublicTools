using SqlTransfer.Infrastructure;
using SqlTransfer.Models;
using SqlTransfer.ViewModels;

namespace SqlTransfer.Services;

public enum SourceMode { NewBackup, ExistingBackup }

/// <summary>Source/SourceDb gelten im Modus NewBackup, BackupFile im Modus ExistingBackup.</summary>
public sealed record TransferRequest(
    SourceMode Mode, ServerEntry? Source, string SourceDb, string? BackupFile,
    ServerEntry Target, string TargetDb, bool Overwrite, bool DeleteSourceBackup, bool DeleteTargetBackup);

public enum TransferOutcome { Success, Failed, Canceled }

public sealed record TransferResult(TransferOutcome Outcome, string? Error = null);

public sealed record TransferSteps(
    StepViewModel Prepare, StepViewModel Backup, StepViewModel Copy, StepViewModel Restore, StepViewModel Cleanup)
{
    public IEnumerable<StepViewModel> All => [Prepare, Backup, Copy, Restore, Cleanup];
}

/// <summary>Führt Prüfung, Backup, Kopie, Restore und Aufräumen nacheinander aus. Muss im UI-Thread aufgerufen werden.</summary>
public sealed class TransferOrchestrator(SqlService sql, FileCopyService copier)
{
    public static string BuildFileName(string dataSource, string database, DateTime stamp)
    {
        var parts = dataSource.Trim().Split('\\', 2);
        var segments = new[] { parts[0], parts.Length > 1 ? parts[1] : "", database, stamp.ToString("yyyyMMdd-HHmmss") };
        return string.Join("-", segments.Where(s => s.Length > 0).Select(SafeFileNamePart)) + ".bak";
    }

    /// <summary>Leitet den DB-Namen aus "Server-Instanz-DB-yyyyMMdd-HHmmss.bak" ab (Instanz fehlt bei der Default-Instanz).</summary>
    public static string DeriveDatabaseName(string backupFile)
    {
        var name = Path.GetFileNameWithoutExtension(backupFile.Trim());
        var match = System.Text.RegularExpressions.Regex.Match(name, @"^(?<head>.+)-\d{8}-\d{6}$");
        if (!match.Success)
            return name;
        var parts = match.Groups["head"].Value.Split('-');
        return parts.Length >= 3 ? string.Join("-", parts.Skip(2)) : parts[^1];
    }

    public async Task<TransferResult> RunAsync(TransferRequest r, TransferSteps steps, Action<string> log, CancellationToken ct)
    {
        StepViewModel? current = null;
        string? sourceFile = null, targetFile = null;
        var backupCompleted = false;
        var restoreStarted = false;
        var existing = r.Mode == SourceMode.ExistingBackup;
        TransferResult result;

        try
        {
            log(existing
                ? Loc.T("Log_StartExisting", r.BackupFile, r.Target.DisplayName, r.TargetDb)
                : Loc.T("Log_Start", r.Source!.DisplayName, r.SourceDb, r.Target.DisplayName, r.TargetDb));

            current = steps.Prepare;
            current.Start();
            await PrepareAsync(r, log, ct);
            current.Complete(Loc.T("Prep_Ok"));

            string fileName;
            if (existing)
            {
                sourceFile = r.BackupFile!.Trim();
                fileName = Path.GetFileName(sourceFile);
            }
            else
            {
                fileName = BuildFileName(r.Source!.DataSource, r.SourceDb, DateTime.Now);
                sourceFile = Path.Combine(r.Source.BackupPath.Trim(), fileName);
            }
            targetFile = Path.Combine(r.Target.BackupPath.Trim(), fileName);

            current = steps.Backup;
            if (existing)
            {
                var detail = Loc.T("Backup_Existing", sourceFile);
                current.Skip(detail);
                log(detail);
                backupCompleted = true;
            }
            else
            {
                current.Start(Loc.T("Backup_File", sourceFile));
                log(Loc.T("Backup_File", sourceFile));
                await sql.BackupAsync(r.Source!, r.SourceDb, sourceFile,
                    new Progress<int>(p => steps.Backup.Report(p, $"{p} %")), ct);
                backupCompleted = true;
                var size = await Task.Run(() => new FileInfo(sourceFile).Length, ct);
                var backupDone = Loc.T("Backup_Done", fileName, Fmt.Bytes(size));
                current.Complete(backupDone);
                log(backupDone);
            }

            current = steps.Copy;
            await CopyAsync(sourceFile, targetFile, steps.Copy, log, ct);

            current = steps.Restore;
            restoreStarted = true;
            await RestoreAsync(r, targetFile, steps.Restore, log, ct);

            current = steps.Cleanup;
            Cleanup(r, sourceFile, targetFile, steps.Cleanup, log);

            result = new TransferResult(TransferOutcome.Success);
        }
        catch (OperationCanceledException)
        {
            current?.Cancel();
            result = new TransferResult(TransferOutcome.Canceled);
            if (restoreStarted && current == steps.Restore)
                log(Loc.T("Restore_CancelNote", r.TargetDb));
        }
        catch (Exception ex)
        {
            current?.Fail(ex.Message);
            log(ex.Message);
            result = new TransferResult(TransferOutcome.Failed, ex.Message);
        }

        foreach (var step in steps.All.Where(s => s.Status == StepStatus.Pending))
            step.Skip();

        // Unvollständiges Backup auf der Quelle entfernen
        if (result.Outcome != TransferOutcome.Success && sourceFile is not null && current == steps.Backup && !backupCompleted)
            await TryDeleteAsync(sourceFile, log);

        return result;
    }

    private async Task PrepareAsync(TransferRequest r, Action<string> log, CancellationToken ct)
    {
        var existing = r.Mode == SourceMode.ExistingBackup;
        if (existing ? string.IsNullOrWhiteSpace(r.BackupFile) : r.Source is null || string.IsNullOrWhiteSpace(r.SourceDb))
            throw new InvalidOperationException(Loc.T(existing ? "Err_NoBackupFile" : "Err_NoSelection"));
        if (string.IsNullOrWhiteSpace(r.TargetDb))
            throw new InvalidOperationException(Loc.T("Err_NoTargetName"));
        foreach (var server in existing ? [r.Target] : new[] { r.Source!, r.Target })
        {
            if (string.IsNullOrWhiteSpace(server.BackupPath))
                throw new InvalidOperationException(Loc.T("Err_NoPath", server.DisplayName));
        }

        ServerInfo? sourceInfo = null;
        if (existing)
        {
            log(Loc.T("Prep_ExistingFile", r.BackupFile!.Trim()));
        }
        else
        {
            sourceInfo = await sql.TestAsync(r.Source!, ct);
            log(Loc.T("Prep_Source", $"{r.Source!.DisplayName} – {sourceInfo.Display}"));
        }
        var targetInfo = await sql.TestAsync(r.Target, ct);
        log(Loc.T("Prep_Target", $"{r.Target.DisplayName} – {targetInfo.Display}"));

        if (sourceInfo is not null)
        {
            if (r.Source!.Id == r.Target.Id || string.Equals(sourceInfo.ServerName, targetInfo.ServerName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(Loc.T("Err_SameServer", sourceInfo.ServerName));

            if (sourceInfo.Major > targetInfo.Major)
                throw new InvalidOperationException(Loc.T("Err_Version", sourceInfo.MarketingName, targetInfo.MarketingName));

            if (await sql.GetDatabaseStateAsync(r.Source, r.SourceDb, ct) != 0)
                throw new InvalidOperationException(Loc.T("Err_SourceDbMissing", r.SourceDb));
        }

        if (await sql.GetDatabaseStateAsync(r.Target, r.TargetDb, ct) is not null)
        {
            if (!r.Overwrite)
                throw new InvalidOperationException(Loc.T("Err_TargetExists", r.TargetDb, r.Target.DisplayName));
            log(Loc.T("Prep_Overwrite", r.TargetDb));
        }

        if (existing)
        {
            if (!await FileCopyService.FileExistsAsync(r.BackupFile!.Trim(), ct))
                throw new FileNotFoundException(Loc.T("Err_BackupFileMissing", r.BackupFile.Trim()));
        }
        else
        {
            await EnsureDirectoryAsync(r.Source!.BackupPath.Trim(), ct);
        }
        await EnsureDirectoryAsync(r.Target.BackupPath.Trim(), ct);
    }

    private async Task CopyAsync(string sourceFile, string targetFile, StepViewModel step, Action<string> log, CancellationToken ct)
    {
        if (string.Equals(Path.GetFullPath(sourceFile), Path.GetFullPath(targetFile), StringComparison.OrdinalIgnoreCase))
        {
            step.Skip(Loc.T("Copy_Same"));
            log(Loc.T("Copy_Same"));
            return;
        }

        step.Start(Loc.T("Copy_Start", targetFile));
        log(Loc.T("Copy_Start", targetFile));
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var progress = new Progress<CopyProgress>(p =>
        {
            var percent = p.Total > 0 ? p.Copied * 100.0 / p.Total : 100;
            var remaining = p.Remaining is { } t ? Fmt.Duration(t) : "–";
            step.Report(percent, Loc.T("Copy_Progress", Fmt.Bytes(p.Copied), Fmt.Bytes(p.Total), Fmt.Bytes((long)p.BytesPerSecond), remaining));
        });

        var total = await copier.CopyAsync(sourceFile, targetFile, progress, ct);
        var targetSize = await Task.Run(() => new FileInfo(targetFile).Length, ct);
        if (targetSize != total)
            throw new IOException(Loc.T("Err_CopySize", Fmt.Bytes(targetSize), Fmt.Bytes(total)));

        var rate = clock.Elapsed.TotalSeconds > 0 ? (long)(total / clock.Elapsed.TotalSeconds) : total;
        var done = Loc.T("Copy_Done", Fmt.Bytes(total), Fmt.Duration(clock.Elapsed), Fmt.Bytes(rate));
        step.Complete(done);
        log(done);
    }

    private async Task RestoreAsync(TransferRequest r, string targetFile, StepViewModel step, Action<string> log, CancellationToken ct)
    {
        step.Start(Loc.T("Restore_Reading"));
        var files = await sql.GetBackupFilesAsync(r.Target, targetFile, ct);
        var paths = await sql.GetDefaultPathsAsync(r.Target, ct);
        var moves = files.Select(f =>
        {
            var folder = f.Type == "L" ? paths.Log : paths.Data;
            var name = SafeFileNamePart($"{r.TargetDb}_{f.LogicalName}") + Path.GetExtension(f.PhysicalName);
            return (f.LogicalName, folder.TrimEnd('\\') + "\\" + name);
        }).ToList();

        var running = Loc.T("Restore_Running", r.TargetDb);
        step.SetDetail(running);
        log(running);
        await sql.RestoreAsync(r.Target, r.TargetDb, targetFile, r.Overwrite, moves,
            new Progress<int>(p => step.Report(p, $"{p} %")), ct);

        var done = Loc.T("Restore_Done", r.TargetDb, r.Target.DisplayName);
        step.Complete(done);
        log(done);
    }

    private void Cleanup(TransferRequest r, string sourceFile, string targetFile, StepViewModel step, Action<string> log)
    {
        var existing = r.Mode == SourceMode.ExistingBackup;
        var deleteSource = !existing && r.DeleteSourceBackup;
        var deleteTarget = r.DeleteTargetBackup;
        if (!deleteSource && !deleteTarget)
        {
            step.Skip(Loc.T("Cleanup_Off"));
            return;
        }

        step.Start();
        var files = new List<string>();
        var notes = new List<string>();
        if (string.Equals(Path.GetFullPath(sourceFile), Path.GetFullPath(targetFile), StringComparison.OrdinalIgnoreCase))
        {
            // Eine einzelne Datei dient als Quelle und Ziel: nur löschen, wenn beides gewünscht ist (nie ein vorhandenes Original)
            if (deleteSource && deleteTarget)
                files.Add(targetFile);
            else
                notes.Add(Loc.T(existing ? "Cleanup_KeptExisting" : "Cleanup_SameFile"));
        }
        else
        {
            if (deleteSource)
                files.Add(sourceFile);
            if (deleteTarget)
                files.Add(targetFile);
        }

        var deleted = 0;
        var failed = new List<string>();
        foreach (var file in files)
        {
            try
            {
                File.Delete(file);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failed.Add(file);
                log(Loc.T("Cleanup_Warn", $"{file} ({ex.Message})"));
            }
        }

        var parts = new List<string>();
        if (files.Count > 0)
            parts.Add(Loc.T("Cleanup_Done", deleted));
        parts.AddRange(notes);
        if (failed.Count > 0)
            parts.Add(Loc.T("Cleanup_Warn", string.Join(", ", failed)));
        var detail = string.Join(" · ", parts);
        step.Complete(detail, warning: failed.Count > 0);
        log(detail);
    }

    private static async Task EnsureDirectoryAsync(string path, CancellationToken ct)
    {
        if (!await FileCopyService.DirectoryExistsAsync(path, ct))
            throw new IOException(Loc.T("Err_PathUnreachable", path));
    }

    private static async Task TryDeleteAsync(string file, Action<string> log)
    {
        // SQL Server kann die Datei kurz nach dem Abbruch noch halten
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (!File.Exists(file))
                    return;
                File.Delete(file);
                log(Loc.T("Partial_Deleted", file));
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(1000);
            }
        }
        log(Loc.T("Partial_DeleteFailed", file));
    }

    private static string SafeFileNamePart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(c => invalid.Contains(c) || c == ',' ? '_' : c).ToArray());
    }
}
