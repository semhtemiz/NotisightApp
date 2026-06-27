namespace Notisight.Api.Features.AI.Contracts;

public sealed record ChunkedNote(
    Guid ChunkId,
    Guid NoteId,
    string Title,
    string Content,
    int Index,
    string SourceType = "note",
    string SourceLabel = "")
{
    public Guid UserId { get; init; }
}
