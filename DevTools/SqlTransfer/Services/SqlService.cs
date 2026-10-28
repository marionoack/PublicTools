using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using SqlTransfer.Models;

namespace SqlTransfer.Services;

public sealed record BackupFile(string LogicalName, string PhysicalName, string Type);

public sealed record DefaultPaths(string Data, string Log);

public sealed class SqlService
{
    private const int ProgressMessageNumber = 3211; // "n percent processed" bei STATS

    public async Task<ServerInfo> TestAsync(ServerEntry server, CancellationToken ct = default)
    {
        await using var con = await OpenAsync(server, ct).ConfigureAwait(false);
        await using var cmd = new SqlCommand(
            """
            SELECT CAST(SERVERPROPERTY('ServerName') AS nvarchar(256)),
                   CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(50)),
                   CAST(SERVERPROPERTY('ProductLevel') AS nvarchar(50)),
                   CAST(SERVERPROPERTY('Edition') AS nvarchar(128))
            """, con);
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await r.ReadAsync(ct).ConfigureAwait(false);
        return new ServerInfo(r.GetString(0), r.GetString(1), r.IsDBNull(2) ? "" : r.GetString(2), r.GetString(3));
    }

    public async Task<List<string>> ListDatabasesAsync(ServerEntry server, CancellationToken ct = default)
    {
        await using var con = await OpenAsync(server, ct).ConfigureAwait(false);
        await using var cmd = new SqlCommand(
            "SELECT name FROM sys.databases WHERE state = 0 AND name <> N'tempdb' ORDER BY name", con);
        var result = new List<string>();
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await r.ReadAsync(ct).ConfigureAwait(false))
            result.Add(r.GetString(0));
        return result;
    }

    /// <summary>Liefert den Zustand (state) einer Datenbank oder null, wenn sie nicht existiert.</summary>
    public async Task<byte?> GetDatabaseStateAsync(ServerEntry server, string database, CancellationToken ct = default)
    {
        await using var con = await OpenAsync(server, ct).ConfigureAwait(false);
        await using var cmd = new SqlCommand("SELECT state FROM sys.databases WHERE name = @n", con);
        cmd.Parameters.AddWithValue("@n", database);
        var value = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToByte(value);
    }

    public async Task BackupAsync(ServerEntry server, string database, string file, IProgress<int> progress, CancellationToken ct)
    {
        var sql = $"BACKUP DATABASE {QuoteName(database)} TO DISK = {QuoteLiteral(file)} WITH COPY_ONLY, INIT, STATS = 1";
        await ExecuteWithProgressAsync(server, sql, progress, ct).ConfigureAwait(false);
    }

    public async Task<List<BackupFile>> GetBackupFilesAsync(ServerEntry server, string file, CancellationToken ct)
    {
        await using var con = await OpenAsync(server, ct).ConfigureAwait(false);
        await using var cmd = new SqlCommand($"RESTORE FILELISTONLY FROM DISK = {QuoteLiteral(file)}", con) { CommandTimeout = 0 };
        var result = new List<BackupFile>();
        try
        {
            await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await r.ReadAsync(ct).ConfigureAwait(false))
                result.Add(new BackupFile(r.GetString(0), r.GetString(1), r.GetString(2)));
        }
        catch (SqlException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        return result;
    }

    public async Task<DefaultPaths> GetDefaultPathsAsync(ServerEntry server, CancellationToken ct)
    {
        await using var con = await OpenAsync(server, ct).ConfigureAwait(false);
        await using var cmd = new SqlCommand(
            """
            SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(512)),
                   CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(512)),
                   (SELECT physical_name FROM sys.master_files WHERE database_id = 1 AND file_id = 1),
                   (SELECT physical_name FROM sys.master_files WHERE database_id = 1 AND file_id = 2)
            """, con);
        await using var r = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        await r.ReadAsync(ct).ConfigureAwait(false);
        var data = r.IsDBNull(0) ? DirectoryOf(r.GetString(2)) : r.GetString(0);
        var log = r.IsDBNull(1) ? DirectoryOf(r.GetString(3)) : r.GetString(1);
        return new DefaultPaths(data, log);
    }

    public async Task RestoreAsync(
        ServerEntry server, string database, string file, bool replace,
        IEnumerable<(string Logical, string Physical)> moves, IProgress<int> progress, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.Append($"RESTORE DATABASE {QuoteName(database)} FROM DISK = {QuoteLiteral(file)} WITH ");
        foreach (var (logical, physical) in moves)
            sb.Append($"MOVE {QuoteLiteral(logical)} TO {QuoteLiteral(physical)}, ");
        sb.Append("STATS = 1");
        if (replace)
            sb.Append(", REPLACE");
        await ExecuteWithProgressAsync(server, sb.ToString(), progress, ct).ConfigureAwait(false);
    }

    private static async Task ExecuteWithProgressAsync(ServerEntry server, string sql, IProgress<int> progress, CancellationToken ct)
    {
        await using var con = await OpenAsync(server, ct).ConfigureAwait(false);
        con.InfoMessage += (_, e) =>
        {
            foreach (SqlError err in e.Errors)
            {
                if (err.Number == ProgressMessageNumber && Regex.Match(err.Message, @"\d+") is { Success: true } m)
                    progress.Report(int.Parse(m.Value));
            }
        };
        await using var cmd = new SqlCommand(sql, con) { CommandTimeout = 0 };
        try
        {
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        catch (SqlException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
    }

    private static async Task<SqlConnection> OpenAsync(ServerEntry server, CancellationToken ct)
    {
        var con = new SqlConnection(server.BuildConnectionString());
        try
        {
            await con.OpenAsync(ct).ConfigureAwait(false);
            return con;
        }
        catch
        {
            await con.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static string QuoteName(string name) => "[" + name.Replace("]", "]]") + "]";

    private static string QuoteLiteral(string value) => "N'" + value.Replace("'", "''") + "'";

    private static string DirectoryOf(string physicalName)
    {
        var i = physicalName.LastIndexOf('\\');
        return i >= 0 ? physicalName[..(i + 1)] : physicalName;
    }
}
