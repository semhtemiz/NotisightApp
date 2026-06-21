using System.Threading.Channels;

namespace Notisight.Api.Features.AI.Services;

public sealed class InMemoryVectorSyncQueue : IVectorSyncQueue
{
    private readonly Channel<VectorSyncJob> _channel = Channel.CreateUnbounded<VectorSyncJob>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public void EnqueueUpsert(Guid noteId) =>
        _channel.Writer.TryWrite(new VectorSyncJob(VectorSyncJobType.Upsert, noteId));

    public void EnqueueDelete(Guid noteId) =>
        _channel.Writer.TryWrite(new VectorSyncJob(VectorSyncJobType.Delete, noteId));

    public async ValueTask<VectorSyncJob> DequeueAsync(CancellationToken cancellationToken) =>
        await _channel.Reader.ReadAsync(cancellationToken);
}
