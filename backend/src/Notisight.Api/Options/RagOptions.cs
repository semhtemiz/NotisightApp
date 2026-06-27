namespace Notisight.Api.Options;

public sealed class RagOptions
{
    public const string SectionName = "Rag";
    public int ChunkTokenTarget { get; set; } = 300;
    public int ChunkOverlapPercent { get; set; } = 20;
    public int TopK { get; set; } = 8;
    public double MinVectorScore { get; set; } = 0.25;
}
