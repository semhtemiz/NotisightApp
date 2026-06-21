namespace Notisight.Api.Features.AI.Services;

using Notisight.Api.Features.AI.Contracts;

public interface IToneProfileService
{
    /// <summary>
    /// Verilen ton için sistem istemi bloğunu döndürür.
    /// GeminiChatService bu bloğu ana sistem istemine enjekte eder.
    /// </summary>
    string GetSystemPromptBlock(PersonalityTone tone);

    /// <summary>
    /// Frontend için ton metadata'sı — isim, açıklama, ikon önerisi.
    /// </summary>
    ToneProfile GetProfile(PersonalityTone tone);
}

public class ToneProfile
{
    public PersonalityTone Tone { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Frontend'in gösterebileceği emoji/ikon önerisi.
    /// </summary>
    public string Icon { get; set; } = string.Empty;
}
