using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Notisight.Api.Features.AI.Contracts;
using Notisight.Api.Features.AI.Services;
using Notisight.Api.Features.Settings.Enums;
using Notisight.Api.Infrastructure.Auth;
using Notisight.Api.Infrastructure.Errors;

namespace Notisight.Api.Features.AI;

[ApiController]
[Route("ai")]
[Authorize]
[EnableRateLimiting("ai")]
public sealed class AiController(
    IQueryOrchestratorService orchestrator,
    IChatHistoryService chatHistory,
    ICurrentUser currentUser,
    IToneProfileService toneProfileService,
    Notisight.Api.Infrastructure.Persistence.ApplicationDbContext dbContext,
    Notisight.Api.Features.Settings.Services.ISecurityService securityService,
    IChatConfigurationProvider chatConfigProvider,
    ILogger<AiController> logger) : ControllerBase
{
    [HttpPost("ask")]
    public async Task Ask([FromBody] AskRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        try
        {
            var userId = currentUser.GetRequiredUserId();

            // Oturumu bul veya oluştur
            var session = await chatHistory.GetOrCreateSessionAsync(userId, request.SessionId, cancellationToken);
            request.SessionId = session.Id.ToString();

            // Kullanıcı mesajını kaydet
            var modeName = request.Mode == ChatMode.Notisight ? "Notisight" : "Standard";
            await chatHistory.SaveUserMessageAsync(session.Id, request.Question, modeName, cancellationToken);

            var result = await orchestrator.ProcessAsync(request, userId, async (msg) => 
            {
                await WriteEventAsync("progress", new { step = msg }, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }, cancellationToken);

            var answerBuilder = new System.Text.StringBuilder();

            await foreach (var chunkText in result.AnswerStream.WithCancellation(cancellationToken))
            {
                answerBuilder.Append(chunkText);
                await WriteEventAsync("chunk", new { content = chunkText }, cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

            var fullAnswer = answerBuilder.ToString();

            // Atıfları (citations) sonda parse et (Eğer ChunkMap varsa ve RAG modundaysak)
            if (result.ChunkMap != null && result.ChunkMap.Count > 0)
            {
                result.Citations = Notisight.Api.Features.AI.Services.RagAnswerService.ParseCitations(fullAnswer, result.ChunkMap).ToList();
            }

            // AI mesajını kaydet
            var metadataJson = (result.Sources?.Count > 0 || result.Citations?.Count > 0)
                ? JsonSerializer.Serialize(new { sources = result.Sources ?? new(), citations = result.Citations ?? new() })
                : null;
                
            await chatHistory.SaveAiMessageAsync(session.Id, fullAnswer, result.ProducedByMode.ToString(), metadataJson, cancellationToken);

            // SessionContext Update
            var sessionContextService = HttpContext.RequestServices.GetRequiredService<Notisight.Api.Features.AI.Services.ISessionContextService>();
            var sessionCtx = await sessionContextService.GetOrCreateAsync(session.Id.ToString());
            await sessionContextService.UpdateAsync(sessionCtx, request.Question, fullAnswer, result.ProducedByMode);

            await WriteEventAsync("complete", new 
            { 
                sources = result.Sources ?? new(), 
                citations = result.Citations ?? new(),
                sessionId = session.Id.ToString(),
                guvenSeviyesi = result.GuvenSeviyesi,
                netlestiriciSoru = result.NetlestiriciSoru,
                isClarificationRequest = result.IsClarificationRequest,
                producedByMode = result.ProducedByMode.ToString(),
                suggestModeSwitch = result.SuggestModeSwitch,
                activeTone = result.ActiveTone.ToString()
            }, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI ask stream failed.");
            await WriteEventAsync("error", new { message = BuildStreamErrorMessage(ex) }, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    private async Task WriteEventAsync(string eventName, object payload, CancellationToken cancellationToken)
    {
        await Response.WriteAsync($"event: {eventName}\n", cancellationToken);
        await Response.WriteAsync($"data: {JsonSerializer.Serialize(payload)}\n\n", cancellationToken);
    }

    private static string BuildStreamErrorMessage(Exception exception)
    {
        return exception switch
        {
            ApiHttpException apiHttpException => apiHttpException.Message,
            HttpRequestException => "AI veya arama servisine ulasilamadi. Lutfen biraz sonra tekrar deneyin.",
            OperationCanceledException => "Istek iptal edildi.",
            _ => "Istek islenirken beklenmeyen bir hata olustu. Lutfen tekrar deneyin."
        };
    }

    [HttpPost("generate-title")]
    public async Task<IActionResult> GenerateTitle([FromBody] AskRequest request, [FromServices] ILlmChatService llmChatService, CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();
        await ConfigureChatAsync(userId, request.Provider, request.ModelId, cancellationToken);

        var title = await llmChatService.GenerateTitleAsync(request.Question, cancellationToken);

        // Session başlığını güncelle
        if (!string.IsNullOrWhiteSpace(request.SessionId) && Guid.TryParse(request.SessionId, out var sessionId))
        {
            await chatHistory.UpdateSessionTitleAsync(sessionId, title ?? "Yeni Sohbet", CancellationToken.None);
        }

        return Ok(new { title });
    }

    [HttpPost("inline-edit")]
    [ProducesResponseType(typeof(InlineEditResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InlineEditResponse>> InlineEdit(
        [FromBody] InlineEditRequest request,
        [FromServices] ILlmChatService llmChatService,
        CancellationToken cancellationToken)
    {
        var selectedText = request.SelectedText.Trim();
        if (string.IsNullOrWhiteSpace(selectedText))
        {
            return BadRequest(new { message = "Selected text is required." });
        }

        var normalizedAction = request.Action.Trim().ToLowerInvariant();
        if (normalizedAction is not ("rewrite" or "explain"))
        {
            return BadRequest(new { message = "Unsupported inline edit action." });
        }

        var userId = currentUser.GetRequiredUserId();
        await ConfigureChatAsync(userId, request.Provider, request.ModelId, cancellationToken);

        var prompt = BuildInlineEditPrompt(normalizedAction, selectedText, request.SurroundingText, request.Target);
        var result = await llmChatService.FreeChatAsync(prompt, null, null, cancellationToken, request.Tone);
        if (result is null)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "AI sağlayıcısından yanıt alınamadı. Model ve API ayarlarını kontrol edin." });
        }

        var cleanedResult = CleanInlineEditResult(result);

        if (string.IsNullOrWhiteSpace(cleanedResult))
        {
            if (normalizedAction == "rewrite")
            {
                return Ok(new InlineEditResponse(selectedText));
            }

            return StatusCode(StatusCodes.Status502BadGateway, new { message = "AI seçili metin için açıklama üretemedi. Lütfen tekrar deneyin." });
        }

        return Ok(new InlineEditResponse(cleanedResult));
    }

    private async Task ConfigureChatAsync(
        Guid userId,
        ProviderType provider,
        string? modelId,
        CancellationToken cancellationToken)
    {
        var apiSetting = await dbContext.AiProviderSettings
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ProviderType == provider, cancellationToken);

        if (apiSetting is null)
        {
            return;
        }

        var key = securityService.Decrypt(apiSetting.EncryptedApiKey);
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        chatConfigProvider.SetConfiguration(new ChatConfiguration
        {
            ProviderType = provider,
            ApiKey = key,
            CustomBaseUrl = string.IsNullOrWhiteSpace(apiSetting.CustomBaseUrl) ? null : apiSetting.CustomBaseUrl.Trim(),
            ModelId = IsUsableModelId(modelId) ? modelId!.Trim() : GetDefaultModelId(provider)
        });
    }

    private static string BuildInlineEditPrompt(string action, string selectedText, string? surroundingText, string target)
    {
        var contextBlock = string.IsNullOrWhiteSpace(surroundingText)
            ? "Bağlam yok."
            : surroundingText.Trim();

        if (action == "explain")
        {
            return $"""
            Seçili metnin ne olduğunu Türkçe olarak 1-2 kısa cümlede açıkla.
            Not içine yazılacak metin üretme; sadece kullanıcıya gösterilecek kısa açıklamayı döndür.
            Başlık, madde listesi, markdown, tırnak, giriş cümlesi ve kapanış cümlesi kullanma.
            Boş yanıt verme.

            Hedef alan: {target}
            Bağlam:
            {contextBlock}

            Seçili metin:
            {selectedText}
            """;
        }

        return $"""
        Seçili metni Türkçe yazım, dil bilgisi, devriklik, noktalama, akıcılık ve netlik açısından hafifçe düzelt.
        Anlamı, tonu, kişi/özne bilgisini, teknik terimleri ve yaklaşık uzunluğu koru.
        Yeni bilgi ekleme, açıklama katma, aşırı profesyonelleştirme, süsleme veya genişletme yapma.
        Sadece düzeltilmiş metni döndür; tırnak, markdown, başlık, madde listesi veya açıklama yazma.
        Düzeltilecek bir şey yoksa seçili metni aynen döndür. Boş yanıt verme.

        Hedef alan: {target}
        Bağlam:
        {contextBlock}

        Seçili metin:
        {selectedText}
        """;
    }

    private static string CleanInlineEditResult(string? result)
    {
        var cleaned = (result ?? string.Empty).Trim();
        if (cleaned.StartsWith("```", StringComparison.Ordinal) && cleaned.EndsWith("```", StringComparison.Ordinal))
        {
            cleaned = cleaned.Trim('`').Trim();
        }

        if (cleaned.Length >= 2 &&
            ((cleaned[0] == '"' && cleaned[^1] == '"') ||
             (cleaned[0] == '“' && cleaned[^1] == '”')))
        {
            cleaned = cleaned[1..^1].Trim();
        }

        return cleaned;
    }

    private static bool IsUsableModelId(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId))
        {
            return false;
        }

        var normalized = modelId.Trim();
        return !normalized.Equals("undefined", StringComparison.OrdinalIgnoreCase) &&
               !normalized.Equals("null", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDefaultModelId(ProviderType providerType)
    {
        return providerType switch
        {
            ProviderType.OpenAI => "gpt-5-mini",
            ProviderType.DashScope => "qwen-plus",
            ProviderType.Anthropic => "claude-sonnet-4-5-20250929",
            ProviderType.Gemini => "gemini-2.5-flash",
            ProviderType.DeepSeek => "deepseek-chat",
            ProviderType.OpenRouter => "openai/gpt-5-mini",
            ProviderType.Grok => "grok-4",
            _ => "gpt-5-mini"
        };
    }

    /// <summary>
    /// Kullanıcının tüm sohbet oturumlarını listele.
    /// </summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();
        var sessions = await chatHistory.GetUserSessionsAsync(userId, cancellationToken);
        
        return Ok(sessions.Select(s => new
        {
            id = s.Id,
            title = s.Title,
            createdAt = s.CreatedAtUtc,
            updatedAt = s.UpdatedAtUtc
        }));
    }

    /// <summary>
    /// Belirtilen oturumun mesajlarını getir.
    /// </summary>
    [HttpGet("sessions/{sessionId}/messages")]
    public async Task<IActionResult> GetSessionMessages(Guid sessionId, CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();
        var messages = await chatHistory.GetSessionMessagesAsync(sessionId, userId, cancellationToken);

        return Ok(messages.Select(m => new
        {
            id = m.Id,
            role = m.Role,
            content = m.Content,
            mode = m.Mode,
            metadataJson = m.MetadataJson,
            createdAt = m.CreatedAtUtc
        }));
    }

    /// <summary>
    /// Belirtilen oturumu ve mesajlarını sil.
    /// </summary>
    [HttpDelete("sessions/{sessionId}")]
    public async Task<IActionResult> DeleteSession(Guid sessionId, CancellationToken cancellationToken)
    {
        var userId = currentUser.GetRequiredUserId();
        await chatHistory.DeleteSessionAsync(sessionId, userId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Kullanılabilir ton profillerini döndürür.
    /// Frontend ton seçici UI'ı bu endpoint'i kullanır.
    /// </summary>
    [HttpGet("tones")]
    [AllowAnonymous]
    public IActionResult GetTones()
    {
        var tones = Enum.GetValues<PersonalityTone>()
            .Select(t => toneProfileService.GetProfile(t))
            .Select(p => new
            {
                value = (int)p.Tone,
                key = p.Tone.ToString(),
                displayName = p.DisplayName,
                description = p.Description,
                icon = p.Icon
            });

        return Ok(tones);
    }
}
