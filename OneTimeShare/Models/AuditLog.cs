namespace OneTimeShare.Models;

public enum AuditEventType
{
    Login,
    LoginFailed,
    Upload,
    Download,
    ShareDeleted,
    ShareExpired,
    ShareLimitReached,
    ShareInvalidPassword,
    ShareNotFound
}

public class AuditLog
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? AssetToken { get; set; }
    public string? AssetName { get; set; }
    public AuditEventType EventType { get; set; }
    public bool Success { get; set; }
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
