namespace Notisight.Api.Domain.Entities;

public class NoteAttachment
{
    public Guid Id { get; set; }
    public Guid NoteId { get; set; }
    public Note Note { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
