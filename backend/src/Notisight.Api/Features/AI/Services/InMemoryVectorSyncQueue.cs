using System.Threading.Channels;
using System.Collections.Concurrent;

namespace Notisight.Api.Features.AI.Services;

public sealed class InMemoryVectorSyncQueue : IVectorSyncQueue
{
    private readonly ConcurrentDictionary<string, byte> _queuedJobs = new();
    private readonly Channel<VectorSyncJob> _channel = Channel.CreateUnbounded<VectorSyncJob>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public void EnqueueUpsert(Guid noteId) =>
        Enqueue(new VectorSyncJob(VectorSyncJobType.Upsert, noteId));

    public void EnqueueDelete(Guid noteId) =>
        Enqueue(new VectorSyncJob(VectorSyncJobType.Delete, noteId));

    public async ValueTask<VectorSyncJob> DequeueAsync(CancellationToken cancellationToken)
    {
        var job = await _channel.Reader.ReadAsync(cancellationToken);
        _queuedJobs.TryRemove(GetKey(job), out _);
        return job;
    }

    private void Enqueue(VectorSyncJob job)
    {
        var key = GetKey(job);
        if (!_queuedJobs.TryAdd(key, 0))
        {
            return;
        }

        if (!_channel.Writer.TryWrite(job))
        {
            _queuedJobs.TryRemove(key, out _);
        }
    }

    private static string GetKey(VectorSyncJob job) =>
        $"{job.Type}:{job.NoteId}";
}
