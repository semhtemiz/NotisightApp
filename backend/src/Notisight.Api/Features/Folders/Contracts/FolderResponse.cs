namespace Notisight.Api.Features.Folders.Contracts;

public sealed record FolderResponse(
    Guid Id,
    string Name,
    Guid? ParentFolderId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
