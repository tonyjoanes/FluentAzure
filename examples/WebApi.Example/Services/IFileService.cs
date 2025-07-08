namespace WebApi.Example.Services;

/// <summary>
/// Service interface for file management operations.
/// </summary>
public interface IFileService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);
    Task<Stream?> DownloadFileAsync(string fileName);
    Task<bool> DeleteFileAsync(string fileName);
    Task<bool> FileExistsAsync(string fileName);
    Task<long> GetFileSizeAsync(string fileName);
    Task<string> GetFileUrlAsync(string fileName);
}
