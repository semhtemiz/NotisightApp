namespace Notisight.Api.Features.AI.Contracts;

/// <summary>
/// Bir chat oturumuna ait hafıza ve kullanıcı profil verisi.
/// SessionContextService tarafından cache'de tutulur.
/// </summary>
public class SessionContext
{
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Son 10 mesaj çifti (kullanıcı + asistan).
    /// GeminiChatService'e sistem istemi olarak enjekte edilir.
    /// </summary>
    public List<ConversationTurn> RecentTurns { get; set; } = new();

    /// <summary>
    /// Bu oturumda erişilen kaynak/not ID'leri.
    /// "geçen" gibi belirsiz referansları çözmekte kullanılır.
    /// </summary>
    public List<string> RecentlyAccessedSourceIds { get; set; } = new();

    /// <summary>
    /// Kullanıcı hakkında çıkarılan profil bilgisi.
    /// İlk versiyonda statik/manuel, ilerleyen sürümde otomatik çıkarılır.
    /// </summary>
    public UserProfile Profile { get; set; } = new();

    /// <summary>
    /// Anlık aktif mod. Her mesajda güncellenir.
    /// </summary>
    public ChatMode ActiveMode { get; set; } = ChatMode.Standard;

    /// <summary>
    /// Bu oturumda gerçekleşen mod geçişlerinin kaydı.
    /// </summary>
    public List<ModeSwitchEvent> ModeSwitchHistory { get; set; } = new();

    /// <summary>
    /// Oturumun anlık aktif tonu.
    /// Her mesajda gelen Tone ile güncellenir.
    /// </summary>
    public PersonalityTone ActiveTone { get; set; } = PersonalityTone.Casual;
}

public class ConversationTurn
{
    public string UserMessage { get; set; } = string.Empty;
    public string AssistantMessage { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Bu mesajın hangi modda yazıldığını saklar.
    /// Hafıza aktarımında "bu Standard modda yazıldı" notu düşülür.
    /// </summary>
    public ChatMode Mode { get; set; } = ChatMode.Standard;
}

/// <summary>
/// Oturum içinde mod geçişi olduğunda kaydedilen event.
/// "Geçen konuşmada" gibi belirsiz referanslarda
/// hangi modda konuşulduğunu anlamak için kullanılır.
/// </summary>
public class ModeSwitchEvent
{
    public ChatMode FromMode { get; set; }
    public ChatMode ToMode { get; set; }
    public DateTime SwitchedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Geçişin kaçıncı mesajda gerçekleştiği (RecentTurns index'i).
    /// </summary>
    public int AtTurnIndex { get; set; }
}

public class UserProfile
{
    /// <summary>
    /// Kullanıcının alan uzmanlığı. Sistem isteminde bağlam için kullanılır.
    /// Örn: "software_engineer", "researcher", "student"
    /// </summary>
    public string Domain { get; set; } = "professional";

    /// <summary>
    /// Kullanıcının en çok kullandığı kaynak tipleri, sıralı.
    /// Örn: ["md", "pdf", "audio"]
    /// </summary>
    public List<string> PreferredSourceTypes { get; set; } = new();
}
