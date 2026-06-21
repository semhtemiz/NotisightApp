namespace Notisight.Api.Features.AI.Services;

public enum VectorSyncJobType
{
    Upsert,
    Delete
}

public sealed record VectorSyncJob(VectorSyncJobType Type, Guid NoteId);
