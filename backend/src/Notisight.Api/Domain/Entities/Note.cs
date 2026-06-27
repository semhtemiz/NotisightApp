namespace Notisight.Api.Domain.Entities;

public sealed class Note
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? FolderId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? FileType { get; set; }
    public string VectorSyncStatus { get; set; } = Domain.Entities.VectorSyncStatus.Pending;
    public string? VectorSyncError { get; set; }
    public DateTimeOffset? VectorSyncedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public User User { get; set; } = null!;
    public Folder? Folder { get; set; }
    public ICollection<NoteTag> NoteTags { get; set; } = [];
    public ICollection<NoteAttachment> NoteAttachments { get; set; } = [];
}
