using Azure.Storage.Blobs;

namespace OneTimeShare.Services;

public class AzureBlobStorageService : IStorageService
{
    private readonly BlobContainerClient _container;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["Storage:AzureBlobConnectionString"]
            ?? throw new InvalidOperationException("Storage:AzureBlobConnectionString is not configured.");
        var containerName = configuration["Storage:ContainerName"] ?? "onetimeshare-assets";
        _container = new BlobContainerClient(connectionString, containerName);
    }

    public async Task UploadAsync(string blobName, byte[] encryptedData, CancellationToken ct = default)
    {
        await _container.CreateIfNotExistsAsync(cancellationToken: ct);
        var blob = _container.GetBlobClient(blobName);
        using var stream = new MemoryStream(encryptedData);
        await blob.UploadAsync(stream, overwrite: true, cancellationToken: ct);
    }

    public async Task<byte[]> DownloadAsync(string blobName, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(blobName);
        var response = await blob.DownloadAsync(ct);
        using var ms = new MemoryStream();
        await response.Value.Content.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    public async Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        var blob = _container.GetBlobClient(blobName);
        await blob.DeleteIfExistsAsync(cancellationToken: ct);
    }
}
