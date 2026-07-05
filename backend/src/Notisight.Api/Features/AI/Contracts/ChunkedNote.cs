namespace Notisight.Api.Features.AI.Contracts;

public sealed record ChunkedNote(
    Guid ChunkId,
    Guid NoteId,
    string Title,
    string Content,
    int Index,
    string SourceType = "note",
    string SourceLabel = "",
    string FolderPath = "",
    Guid? FolderId = null,
    double? DurationSeconds = null)
{
    public Guid UserId { get; init; }
}
