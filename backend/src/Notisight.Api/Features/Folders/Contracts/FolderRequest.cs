namespace Notisight.Api.Features.Folders.Contracts;

public sealed record FolderRequest(
    string Name,
    Guid? ParentFolderId);
