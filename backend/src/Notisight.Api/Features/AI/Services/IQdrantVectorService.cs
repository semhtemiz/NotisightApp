using Notisight.Api.Features.AI.Contracts;

namespace Notisight.Api.Features.AI.Services;

public interface IQdrantVectorService
{
    Task EnsureCollectionAsync(CancellationToken cancellationToken);
    Task UpsertChunksAsync(
        IReadOnlyList<ChunkedNote> chunks,
        IReadOnlyList<IReadOnlyList<float>> vectors,
        CancellationToken cancellationToken);
    Task DeleteByNoteIdAsync(Guid noteId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SearchChunkResult>> SearchAsync(
        Guid userId,
        IReadOnlyList<float> queryVector,
        int topK,
        CancellationToken cancellationToken);
}
