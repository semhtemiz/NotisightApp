using Amazon.S3;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Options;
using Notisight.Api.Features.Ingestion.Contracts;
using Notisight.Api.Options;

namespace Notisight.Api.Features.Ingestion.Services;

public class CloudflareR2StorageService : IFileStorageService
{
    private readonly AmazonS3Client _s3Client;
    private readonly string _bucketName;
    private readonly string _publicUrlPrefix;

    public CloudflareR2StorageService(IOptions<CloudflareR2Options> options)
    {
        var r2Options = options.Value;
        _bucketName = r2Options.BucketName;
        _publicUrlPrefix = r2Options.PublicUrlPrefix.TrimEnd('/');

        var config = new AmazonS3Config
        {
            ServiceURL = r2Options.EndpointUrl,
        };

        _s3Client = new AmazonS3Client(r2Options.AccessKey, r2Options.SecretKey, config);
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken)
    {
        var objectKey = $"{Guid.NewGuid()}-{fileName}";

        var uploadRequest = new TransferUtilityUploadRequest
        {
            InputStream = fileStream,
            Key = objectKey,
            BucketName = _bucketName,
            ContentType = contentType,
            DisablePayloadSigning = true,
            AutoCloseStream = false
        };

        using var transferUtility = new TransferUtility(_s3Client);
        await transferUtility.UploadAsync(uploadRequest, cancellationToken);

        return $"{_publicUrlPrefix}/{objectKey}";
    }

    public async Task DeleteFileAsync(string fileUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(fileUrl)) return;
        
        var uri = new Uri(fileUrl);
        var objectKey = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        
        await _s3Client.DeleteObjectAsync(_bucketName, objectKey, cancellationToken);
    }

    public async Task<(Stream Stream, string ContentType)> GetFileStreamAsync(string fileUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(fileUrl)) return (null!, null!);
        
        var uri = new Uri(fileUrl);
        var objectKey = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        
        var response = await _s3Client.GetObjectAsync(_bucketName, objectKey, cancellationToken);
        return (response.ResponseStream, response.Headers.ContentType);
    }

    public async Task<Stream> DownloadFileAsync(string fileUrl, CancellationToken cancellationToken)
    {
        var (stream, _) = await GetFileStreamAsync(fileUrl, cancellationToken);
        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, cancellationToken);
        await stream.DisposeAsync();
        memoryStream.Position = 0;
        return memoryStream;
    }
}
