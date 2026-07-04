using System.ComponentModel.DataAnnotations;

namespace Notisight.Api.Features.Notes.Contracts;

public sealed record NoteRequest(
    [Required]
    [MaxLength(200)]
    string Title,

    string? Content,

    Guid? FolderId,

    IReadOnlyList<Guid>? TagIds);
