namespace Notisight.Api.Options;

public sealed class QdrantOptions
{
    public const string SectionName = "Qdrant";
    public string Url { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string CollectionName { get; set; } = "notisight_chunks";
    public int VectorSize { get; set; } = 768;

    public string EffectiveUrl => string.IsNullOrWhiteSpace(Url) ? Endpoint : Url;
}
