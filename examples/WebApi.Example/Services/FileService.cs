using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using WebApi.Example.Configuration;

namespace WebApi.Example.Services;

/// <summary>
/// Service implementation for file management operations using Azure Blob Storage.
/// </summary>
public class FileService : IFileService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly WebApiConfiguration _config;
    private readonly ILogger<FileService> _logger;

    public FileService(
        WebApiConfiguration config,
        ILogger<FileService> logger)
    {
        _config = config;
        _logger = logger;
        _blobServiceClient = new BlobServiceClient(config.Storage.ConnectionString);
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_config.Storage.DefaultContainer);
            await containerClient.CreateIfNotExistsAsync();

            var blobClient = containerClient.GetBlobClient(fileName);

            var blobHttpHeaders = new BlobHttpHeaders
            {
                ContentType = contentType
            };

            await blobClient.UploadAsync(fileStream, blobHttpHeaders);

            _logger.LogInformation("Uploaded file: {FileName} to container: {Container}",
                fileName, _config.Storage.DefaultContainer);

            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file: {FileName}", fileName);
            throw;
        }
    }

    public async Task<Stream?> DownloadFileAsync(string fileName)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_config.Storage.DefaultContainer);
            var blobClient = containerClient.GetBlobClient(fileName);

            if (!await blobClient.ExistsAsync())
            {
                return null;
            }

            var response = await blobClient.DownloadAsync();
            return response.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download file: {FileName}", fileName);
            throw;
        }
    }

    public async Task<bool> DeleteFileAsync(string fileName)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_config.Storage.DefaultContainer);
            var blobClient = containerClient.GetBlobClient(fileName);

            if (!await blobClient.ExistsAsync())
            {
                return false;
            }

            await blobClient.DeleteAsync();

            _logger.LogInformation("Deleted file: {FileName} from container: {Container}",
                fileName, _config.Storage.DefaultContainer);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {FileName}", fileName);
            throw;
        }
    }

    public async Task<bool> FileExistsAsync(string fileName)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_config.Storage.DefaultContainer);
            var blobClient = containerClient.GetBlobClient(fileName);

            return await blobClient.ExistsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if file exists: {FileName}", fileName);
            return false;
        }
    }

    public async Task<long> GetFileSizeAsync(string fileName)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_config.Storage.DefaultContainer);
            var blobClient = containerClient.GetBlobClient(fileName);

            if (!await blobClient.ExistsAsync())
            {
                return 0;
            }

            var properties = await blobClient.GetPropertiesAsync();
            return properties.Value.ContentLength;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file size: {FileName}", fileName);
            return 0;
        }
    }

    public async Task<string> GetFileUrlAsync(string fileName)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_config.Storage.DefaultContainer);
            var blobClient = containerClient.GetBlobClient(fileName);

            if (!await blobClient.ExistsAsync())
            {
                return string.Empty;
            }

            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file URL: {FileName}", fileName);
            return string.Empty;
        }
    }
}
