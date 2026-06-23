namespace OneTimeShare.Services;

public interface IStorageService
{
    Task UploadAsync(string blobName, byte[] encryptedData, CancellationToken ct = default);
    Task<byte[]> DownloadAsync(string blobName, CancellationToken ct = default);
    Task DeleteAsync(string blobName, CancellationToken ct = default);
}
