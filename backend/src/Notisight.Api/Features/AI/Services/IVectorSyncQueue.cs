namespace Notisight.Api.Features.AI.Services;

public interface IVectorSyncQueue
{
    void EnqueueUpsert(Guid noteId);
    void EnqueueDelete(Guid noteId);
    ValueTask<VectorSyncJob> DequeueAsync(CancellationToken cancellationToken);
}
