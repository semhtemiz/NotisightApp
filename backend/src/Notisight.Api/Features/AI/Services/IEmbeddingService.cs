namespace Notisight.Api.Features.AI.Services;

public interface IEmbeddingService
{
    Task<IReadOnlyList<float>> EmbedDocumentAsync(string text, CancellationToken cancellationToken);
    Task<IReadOnlyList<float>> EmbedQueryAsync(string text, CancellationToken cancellationToken);
}
