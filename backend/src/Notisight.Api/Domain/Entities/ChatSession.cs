namespace Notisight.Api.Domain.Entities;

public sealed class ChatSession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = "Yeni Sohbet";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public ICollection<ChatMessage> Messages { get; set; } = [];
}
