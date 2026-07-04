using System.ComponentModel.DataAnnotations;

namespace Notisight.Api.Features.Folders.Contracts;

public sealed record FolderRequest(
    [Required]
    [MaxLength(160)]
    string Name,

    Guid? ParentFolderId);
