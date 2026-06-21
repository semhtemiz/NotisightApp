using Notisight.Api.Features.Settings.Enums;

namespace Notisight.Api.Features.AI.Contracts;

public sealed record ChatHistoryMessage(string Role, string Text);

/// <summary>
/// Hangi modda çalışılacağı. Frontend buton durumundan gelir.
/// </summary>
public enum ChatMode
{
    /// <summary>
    /// Serbest AI sohbeti. RAG çalışmaz, sistem istemi enjekte edilmez.
    /// </summary>
    Standard = 0,

    /// <summary>
    /// Kişisel not asistanı modu. 4 katmanlı pipeline çalışır.
    /// </summary>
    Notisight = 1
}

public enum PersonalityTone
{
    /// <summary>
    /// Samimi ve kısa. Arkadaş gibi konuşur, gereksiz detaydan kaçınır.
    /// </summary>
    Casual = 0,

    /// <summary>
    /// Teknik ve hassas. Jargon kullanır, detay verir, kaynak gösterir.
    /// </summary>
    Technical = 1,

    /// <summary>
    /// Öğretici ve sabırlı. Adım adım açıklar, örnekler verir.
    /// </summary>
    Pedagogical = 2,

    /// <summary>
    /// Resmi ve düzesinde. Profesyonel yazışma tonu, mesafeli ama net.
    /// </summary>
    Formal = 3
}

public class AskRequest
{
    public string Question { get; set; } = string.Empty;

    /// <summary>
    /// Frontend'in yönettiği oturum kimliği.
    /// Yeni oturum başlarken frontend GUID üretir ve her istekte gönderir.
    /// Null gelirse SessionContextService yeni oturum başlatır.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Hangi modda çalışılacağı. Frontend buton durumundan gelir.
    /// Default Standard — böylece mevcut istemciler kırılmaz.
    /// </summary>
    public ChatMode Mode { get; set; } = ChatMode.Standard;

    /// <summary>
    /// Bu oturum için seçilen davranış tonu.
    /// Her mesajda gönderilebilir — değişirse SessionContext güncellenir.
    /// Default Casual: en az sürtüşmeli başlangıç deneyimi.
    /// </summary>
    public PersonalityTone Tone { get; set; } = PersonalityTone.Casual;

    /// <summary>
    /// Kullanıcının seçtiği AI sağlayıcısı (Opsiyonel, varsayılan DashScope)
    /// </summary>
    public ProviderType Provider { get; set; } = ProviderType.DashScope;

    /// <summary>
    /// Kullanıcının seçtiği spesifik model kimliği (Örn: qwen-plus, gpt-4o)
    /// </summary>
    public string? ModelId { get; set; }

    public IReadOnlyList<ChatHistoryMessage>? History { get; set; }
}
