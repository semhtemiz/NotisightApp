namespace Notisight.Api.Domain.Entities;

public sealed class Tag
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public User User { get; set; } = null!;
    public ICollection<NoteTag> NoteTags { get; set; } = [];
}
