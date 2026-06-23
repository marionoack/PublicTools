using OneTimeShare.Data;
using OneTimeShare.Models;

namespace OneTimeShare.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;

    public AuditService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(AuditEventType eventType, bool success,
        string? userId = null, string? username = null,
        string? assetToken = null, string? assetName = null,
        string? details = null, string? ipAddress = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            EventType = eventType,
            Success = success,
            UserId = userId,
            Username = username,
            AssetToken = assetToken,
            AssetName = assetName,
            Details = details,
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}
