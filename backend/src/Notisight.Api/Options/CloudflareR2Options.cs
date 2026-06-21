namespace Notisight.Api.Options;

public class CloudflareR2Options
{
    public const string SectionName = "CloudflareR2";
    
    public string BucketName { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string EndpointUrl { get; set; } = string.Empty;
    public string PublicUrlPrefix { get; set; } = string.Empty;
}
