namespace Notisight.Api.Features.AI.Contracts;

public sealed record AskSourceReference(
    Guid NoteId,
    string Title,
    double Score,
    string FolderPath = "");
