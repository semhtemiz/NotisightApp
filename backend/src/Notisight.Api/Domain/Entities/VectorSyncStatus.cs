namespace Notisight.Api.Domain.Entities;

public static class VectorSyncStatus
{
    public const string Pending = "pending";
    public const string Indexing = "indexing";
    public const string Synced = "synced";
    public const string Failed = "failed";
}
