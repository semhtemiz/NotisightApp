namespace Notisight.Api.Domain.Entities;

public sealed class Folder
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ParentFolderId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public User User { get; set; } = null!;
    public Folder? ParentFolder { get; set; }
    public ICollection<Folder> Children { get; set; } = [];
    public ICollection<Note> Notes { get; set; } = [];
}
