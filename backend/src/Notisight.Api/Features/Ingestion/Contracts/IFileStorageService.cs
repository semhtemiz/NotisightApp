namespace Notisight.Api.Features.Ingestion.Contracts;

public interface IFileStorageService
{
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken);
    Task DeleteFileAsync(string fileUrl, CancellationToken cancellationToken);
    Task<(Stream Stream, string ContentType)> GetFileStreamAsync(string fileUrl, CancellationToken cancellationToken);
    Task<Stream> DownloadFileAsync(string fileUrl, CancellationToken cancellationToken);
}
