namespace Notisight.Api.Features.Ingestion.Contracts;

public sealed record UploadedNoteResponse(
    Guid NoteId,
    string Title,
    string SourceType,
    int CharacterCount,
    string? FileUrl = null,
    string? FileType = null,
    double? DurationSeconds = null,
    string? VectorSyncStatus = null,
    string? VectorSyncError = null,
    DateTimeOffset? VectorSyncedAtUtc = null);
