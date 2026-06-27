using Notisight.Api.Features.Ingestion.Contracts;

namespace Notisight.Api.Tests.Infrastructure;

public sealed class RecordingFileStorageService : IFileStorageService
{
    private readonly object _gate = new();
    private readonly Dictionary<string, (byte[] Bytes, string ContentType)> _files = [];

    public IReadOnlyList<string> UploadedUrls
    {
        get
        {
            lock (_gate)
            {
                return _files.Keys.ToList();
            }
        }
    }

    public Task<string> UploadFileAsync(
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        fileStream.CopyTo(memoryStream);

        var safeName = Uri.EscapeDataString(fileName);
        var url = $"https://storage.test/{Guid.NewGuid():N}-{safeName}";

        lock (_gate)
        {
            _files[url] = (memoryStream.ToArray(), contentType);
        }

        return Task.FromResult(url);
    }

    public Task DeleteFileAsync(string fileUrl, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _files.Remove(fileUrl);
        }

        return Task.CompletedTask;
    }

    public Task<(Stream Stream, string ContentType)> GetFileStreamAsync(
        string fileUrl,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_files.TryGetValue(fileUrl, out var stored))
            {
                return Task.FromResult<(Stream Stream, string ContentType)>(
                    (new MemoryStream(stored.Bytes), stored.ContentType));
            }
        }

        return Task.FromResult<(Stream Stream, string ContentType)>(
            (new MemoryStream(), "application/octet-stream"));
    }

    public async Task<Stream> DownloadFileAsync(string fileUrl, CancellationToken cancellationToken)
    {
        var (stream, _) = await GetFileStreamAsync(fileUrl, cancellationToken);
        return stream;
    }
}
