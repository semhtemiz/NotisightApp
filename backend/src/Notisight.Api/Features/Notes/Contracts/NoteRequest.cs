namespace Notisight.Api.Features.Notes.Contracts;

public sealed record NoteRequest(
    string Title,
    string Content,
    Guid? FolderId,
    IReadOnlyList<Guid>? TagIds);
