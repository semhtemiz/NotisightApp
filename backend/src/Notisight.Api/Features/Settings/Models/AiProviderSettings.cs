using Notisight.Api.Domain.Entities;
using Notisight.Api.Features.Settings.Enums;

namespace Notisight.Api.Features.Settings.Models;

public class AiProviderSettings
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public ProviderType ProviderType { get; set; }

    /// <summary>
    /// Şifrelenmiş API Anahtarı (AES ile şifrelenip tutulur)
    /// </summary>
    public string EncryptedApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Kendi proxy sunucusunu falan kullanmak isteyenler için (isteğe bağlı)
    /// </summary>
    public string? CustomBaseUrl { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
