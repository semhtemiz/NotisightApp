using Notisight.Api.Features.AI.Contracts;
using Notisight.Api.Features.AI.Services;

namespace Notisight.Api.Tests.Infrastructure;

public sealed class RecordingQdrantVectorService : IQdrantVectorService
{
    private readonly object _gate = new();
    private readonly List<Guid> _deletedNoteIds = [];
    private readonly List<IReadOnlyList<ChunkedNote>> _upserts = [];
    private readonly List<SearchChunkResult> _searchResults = [];

    public bool ThrowOnUpsert { get; set; }
    public bool ThrowOnDelete { get; set; }

    public IReadOnlyList<Guid> DeletedNoteIds
    {
        get
        {
            lock (_gate)
            {
                return _deletedNoteIds.ToList();
            }
        }
    }

    public IReadOnlyList<IReadOnlyList<ChunkedNote>> Upserts
    {
        get
        {
            lock (_gate)
            {
                return _upserts.Select(x => x.ToList()).ToList();
            }
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _deletedNoteIds.Clear();
            _upserts.Clear();
            _searchResults.Clear();
            ThrowOnUpsert = false;
            ThrowOnDelete = false;
        }
    }

    public void SetSearchResults(params SearchChunkResult[] results)
    {
        lock (_gate)
        {
            _searchResults.Clear();
            _searchResults.AddRange(results);
        }
    }

    public Task EnsureCollectionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task UpsertChunksAsync(
        IReadOnlyList<ChunkedNote> chunks,
        IReadOnlyList<IReadOnlyList<float>> vectors,
        CancellationToken cancellationToken)
    {
        if (ThrowOnUpsert)
        {
            throw new InvalidOperationException("Synthetic Qdrant upsert failure.");
        }

        lock (_gate)
        {
            _upserts.Add(chunks.ToList());
        }

        return Task.CompletedTask;
    }

    public Task DeleteByNoteIdAsync(Guid noteId, CancellationToken cancellationToken)
    {
        if (ThrowOnDelete)
        {
            throw new InvalidOperationException("Synthetic Qdrant delete failure.");
        }

        lock (_gate)
        {
            _deletedNoteIds.Add(noteId);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SearchChunkResult>> SearchAsync(
        Guid userId,
        IReadOnlyList<float> queryVector,
        int topK,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<SearchChunkResult>>(_searchResults.Take(topK).ToList());
        }
    }
}
