namespace OneTimeShare.Services;

/// <summary>Local filesystem fallback for development – stores blobs in App_Data/blobs.</summary>
public class LocalStorageService : IStorageService
{
    private readonly string _basePath;

    public LocalStorageService(IWebHostEnvironment env)
    {
        _basePath = Path.Combine(env.ContentRootPath, "App_Data", "blobs");
        Directory.CreateDirectory(_basePath);
    }

    public Task UploadAsync(string blobName, byte[] encryptedData, CancellationToken ct = default)
    {
        var path = SafePath(blobName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return File.WriteAllBytesAsync(path, encryptedData, ct);
    }

    public async Task<byte[]> DownloadAsync(string blobName, CancellationToken ct = default)
    {
        return await File.ReadAllBytesAsync(SafePath(blobName), ct);
    }

    public Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        var path = SafePath(blobName);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string SafePath(string blobName)
    {
        // Sanitize to prevent directory traversal
        var safe = Path.GetFileName(blobName);
        return Path.Combine(_basePath, safe);
    }
}
