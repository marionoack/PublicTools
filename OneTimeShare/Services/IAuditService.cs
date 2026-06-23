using OneTimeShare.Models;

namespace OneTimeShare.Services;

public interface IAuditService
{
    Task LogAsync(AuditEventType eventType, bool success,
        string? userId = null, string? username = null,
        string? assetToken = null, string? assetName = null,
        string? details = null, string? ipAddress = null);
}
