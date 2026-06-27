namespace Notisight.Api.Features.Notes.Contracts;

public sealed record NoteResponse(
    Guid Id,
    string Title,
    string Content,
    Guid? FolderId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<TagSummaryResponse> Tags,
    string? FileUrl = null,
    string? FileType = null,
    string? VectorSyncStatus = null,
    string? VectorSyncError = null,
    DateTimeOffset? VectorSyncedAtUtc = null);
