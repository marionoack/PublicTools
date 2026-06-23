namespace OneTimeShare.Models;

public enum AssetType { Secret, File }

public class SharedAsset
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public ApplicationUser? Owner { get; set; }
    public string PublicToken { get; set; } = string.Empty;
    public AssetType AssetType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? BlobName { get; set; }
    public string? ContentType { get; set; }
    /// <summary>Base64-encoded encrypted per-asset key: [12-byte nonce][ciphertext]</summary>
    public string? EncryptedKeyData { get; set; }
    public string? PasswordHash { get; set; }
    public int AccessCount { get; set; }
    public int? MaxAccessCount { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    public bool IsLimitReached => MaxAccessCount.HasValue && AccessCount >= MaxAccessCount.Value;
    public bool IsAvailable => !IsExpired && !IsLimitReached;
}
