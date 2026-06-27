using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Notisight.Api.Features.Settings.Contracts;
using Notisight.Api.Features.Settings.Enums;
using Notisight.Api.Features.Settings.Models;
using Notisight.Api.Features.Settings.Services;
using Notisight.Api.Infrastructure.Auth;
using Notisight.Api.Infrastructure.Persistence;

namespace Notisight.Api.Features.Settings;

[ApiController]
[Route("api/settings")]
[Authorize]
public class SettingsController(
    ApplicationDbContext dbContext,
    ISecurityService securityService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("ai-providers")]
    public async Task<IActionResult> GetAiProviders(CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();

        var userSettings = await dbContext.AiProviderSettings
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);

        var response = new List<AiProviderDto>();

        foreach (ProviderType providerType in Enum.GetValues(typeof(ProviderType)))
        {
            var setting = userSettings.FirstOrDefault(x => x.ProviderType == providerType);
            
            if (setting == null)
            {
                response.Add(new AiProviderDto
                {
                    ProviderType = providerType,
                    IsConfigured = false
                });
            }
            else
            {
                var decryptedKey = securityService.Decrypt(setting.EncryptedApiKey);
                var isConfigured = !string.IsNullOrWhiteSpace(decryptedKey);
                string? maskedKey = null;

                if (isConfigured && decryptedKey.Length > 4)
                {
                    maskedKey = new string('*', decryptedKey.Length - 4) + decryptedKey.Substring(decryptedKey.Length - 4);
                }
                else if (isConfigured)
                {
                    maskedKey = "****";
                }

                response.Add(new AiProviderDto
                {
                    ProviderType = providerType,
                    IsConfigured = isConfigured,
                    MaskedApiKey = maskedKey,
                    CustomBaseUrl = setting.CustomBaseUrl
                });
            }
        }

        return Ok(response);
    }

    [HttpPost("ai-providers")]
    public async Task<IActionResult> UpdateAiProvider([FromBody] UpdateAiProviderRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();

        var existingSetting = await dbContext.AiProviderSettings
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ProviderType == request.ProviderType, cancellationToken);

        if (existingSetting is null && string.IsNullOrWhiteSpace(request.ApiKey))
        {
            return BadRequest(new { message = "API anahtarı ilk kurulumda zorunludur." });
        }

        var customBaseUrl = string.IsNullOrWhiteSpace(request.CustomBaseUrl) ? null : request.CustomBaseUrl.Trim();

        if (existingSetting == null)
        {
            var newSetting = new AiProviderSettings
            {
                UserId = userId,
                ProviderType = request.ProviderType,
                EncryptedApiKey = securityService.Encrypt(request.ApiKey),
                CustomBaseUrl = customBaseUrl
            };
            dbContext.AiProviderSettings.Add(newSetting);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.ApiKey))
            {
                existingSetting.EncryptedApiKey = securityService.Encrypt(request.ApiKey);
            }

            existingSetting.CustomBaseUrl = customBaseUrl;
            existingSetting.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Ayarlar başarıyla kaydedildi." });
    }
}
