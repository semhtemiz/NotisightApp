namespace Notisight.Api.Domain.Entities;

public sealed class ChatMessage
{
    public Guid Id { get; set; }
    public Guid ChatSessionId { get; set; }

    /// <summary>
    /// "user" veya "ai"
    /// </summary>
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// AI mesajlarındaki kaynaklar (sources) ve atıflar (citations) JSON formatında.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// "Standard" veya "Notisight"
    /// </summary>
    public string? Mode { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public ChatSession ChatSession { get; set; } = null!;
}
