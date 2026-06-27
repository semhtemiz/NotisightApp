namespace Notisight.Api.Features.Ingestion.Contracts;

public sealed record NoteAttachmentResponse(
    Guid Id,
    Guid NoteId,
    string FileName,
    string ContentType,
    string FileUrl);
