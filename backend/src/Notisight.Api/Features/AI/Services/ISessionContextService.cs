using Notisight.Api.Features.AI.Contracts;

namespace Notisight.Api.Features.AI.Services;

public interface ISessionContextService
{
    Task<SessionContext> GetOrCreateAsync(Guid userId, string? sessionId);
    Task UpdateAsync(SessionContext context, string userMessage, string assistantMessage, ChatMode mode = ChatMode.Standard);
    Task AddAccessedSourceAsync(SessionContext context, string sourceId);

    /// <summary>
    /// Mod değiştiğinde çağrılır. ActiveMode'u günceller ve ModeSwitchEvent kaydeder.
    /// </summary>
    Task RecordModeSwitchAsync(SessionContext context, ChatMode fromMode, ChatMode toMode);

    /// <summary>
    /// Oturumun aktif tonunu günceller.
    /// Gelen mesajdaki Tone, mevcut ActiveTone'dan farklıysa çağrılır.
    /// </summary>
    Task UpdateToneAsync(SessionContext context, PersonalityTone tone);
}
