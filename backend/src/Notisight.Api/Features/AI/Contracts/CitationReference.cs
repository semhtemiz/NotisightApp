namespace Notisight.Api.Features.AI.Contracts;

public sealed record CitationReference(
    string RefId,
    Guid NoteId,
    string Title,
    string SourceType,
    string SourceLabel,
    string Snippet,
    string FolderPath = "",
    double? DurationSeconds = null);
