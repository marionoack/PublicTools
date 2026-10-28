using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;

namespace SqlTransfer.Models;

public sealed class ServerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    /// <summary>Host\Instanz</summary>
    public string DataSource { get; set; } = "";

    public bool UseSqlAuth { get; set; }

    public string UserName { get; set; } = "";

    /// <summary>Passwort, verschlüsselt mit Windows DPAPI (CurrentUser), Base64.</summary>
    public string? ProtectedPassword { get; set; }

    /// <summary>UNC-Pfad für Backups; gilt für SQL-Server (BACKUP/RESTORE) und die App.</summary>
    public string BackupPath { get; set; } = "";

    [JsonIgnore]
    public string DisplayName =>
        string.IsNullOrWhiteSpace(Name) ? DataSource : $"{Name} ({DataSource})";

    public string? GetPassword()
    {
        if (string.IsNullOrEmpty(ProtectedPassword))
            return null;
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(ProtectedPassword), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }

    public void SetPassword(string? plain)
    {
        ProtectedPassword = string.IsNullOrEmpty(plain)
            ? null
            : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser));
    }

    public string BuildConnectionString()
    {
        var b = new SqlConnectionStringBuilder
        {
            DataSource = DataSource.Trim(),
            InitialCatalog = "master",
            Encrypt = true,
            TrustServerCertificate = true,
            ConnectTimeout = 15,
            ApplicationName = "SqlTransfer",
        };
        if (UseSqlAuth)
        {
            b.UserID = UserName;
            b.Password = GetPassword() ?? "";
        }
        else
        {
            b.IntegratedSecurity = true;
        }
        return b.ConnectionString;
    }
}
