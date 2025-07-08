using Microsoft.Extensions.Logging;

namespace AzureFunctions.Example.Services;

public class StorageService : IStorageService
{
    private readonly ILogger<StorageService> _logger;
    private readonly string _connectionString;

    public StorageService(ILogger<StorageService> logger, string connectionString)
    {
        _logger = logger;
        _connectionString = connectionString;
    }

    public async Task<bool> IsConnectedAsync()
    {
        _logger.LogInformation("Checking Storage connection...");
        // Simulate connection check
        await Task.Delay(50);
        return true;
    }

    public async Task<string> GetContainerInfoAsync(string containerName)
    {
        _logger.LogInformation("Getting container info for: {ContainerName}", containerName);
        // Simulate getting container info
        await Task.Delay(100);
        return $"Container: {containerName}, Blob Count: {Random.Shared.Next(0, 1000)}";
    }

    public async Task UploadBlobAsync(string containerName, string blobName, string content)
    {
        _logger.LogInformation("Uploading blob {BlobName} to container {ContainerName}", blobName, containerName);
        // Simulate blob upload
        await Task.Delay(200);
        _logger.LogInformation("Blob uploaded successfully: {BlobName}", blobName);
    }
}
