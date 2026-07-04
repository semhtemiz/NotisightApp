using Microsoft.EntityFrameworkCore;
using Notisight.Api.Features.AI.Contracts;
using Notisight.Api.Features.Settings.Enums;
using Notisight.Api.Features.Settings.Services;
using Notisight.Api.Infrastructure.Persistence;

namespace Notisight.Api.Features.AI.Services;

public class QueryOrchestratorService : IQueryOrchestratorService
{
    private readonly IIntentParserService _intentParser;
    private readonly ISessionContextService _sessionContext;
    private readonly ISmartRetrievalService _smartRetrieval;
    private readonly IConfidenceEngineService _confidenceEngine;
    private readonly IRagAnswerService _ragAnswer;
    private readonly ILlmChatService _llmChatService;
    private readonly ApplicationDbContext _dbContext;
    private readonly ISecurityService _securityService;
    private readonly IChatConfigurationProvider _chatConfigProvider;

    public QueryOrchestratorService(
        IIntentParserService intentParser,
        ISessionContextService sessionContext,
        ISmartRetrievalService smartRetrieval,
        IConfidenceEngineService confidenceEngine,
        IRagAnswerService ragAnswer,
        ILlmChatService llmChatService,
        ApplicationDbContext dbContext,
        ISecurityService securityService,
        IChatConfigurationProvider chatConfigProvider)
    {
        _intentParser = intentParser;
        _sessionContext = sessionContext;
        _smartRetrieval = smartRetrieval;
        _confidenceEngine = confidenceEngine;
        _ragAnswer = ragAnswer;
        _llmChatService = llmChatService;
        _dbContext = dbContext;
        _securityService = securityService;
        _chatConfigProvider = chatConfigProvider;
    }

    public async Task<OrchestratorStreamResult> ProcessAsync(AskRequest request, Guid userId, Func<string, Task>? onProgress, CancellationToken cancellationToken)
    {
        // 0. API Config Yükle
        var apiSetting = await _dbContext.AiProviderSettings
            .FirstOrDefaultAsync(x => x.UserId == userId && x.ProviderType == request.Provider, cancellationToken);

        if (apiSetting != null)
        {
            var key = _securityService.Decrypt(apiSetting.EncryptedApiKey);
            if (!string.IsNullOrWhiteSpace(key))
            {
                string? customBaseUrl = string.IsNullOrWhiteSpace(apiSetting.CustomBaseUrl) ? null : apiSetting.CustomBaseUrl.Trim();
                string modelId = string.IsNullOrWhiteSpace(request.ModelId)
                    ? GetDefaultModelId(request.Provider)
                    : request.ModelId.Trim();

                if (!string.IsNullOrWhiteSpace(customBaseUrl))
                {
                    if (!customBaseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        customBaseUrl = null;
                    }
                }

                _chatConfigProvider.SetConfiguration(new ChatConfiguration
                {
                    ProviderType = request.Provider,
                    ApiKey = key,
                    CustomBaseUrl = customBaseUrl,
                    ModelId = modelId
                });
            }
        }

        if (onProgress != null) await onProgress("Oturum bilgileri yükleniyor...");
        // === KATMAN 1: Oturum bağlamını yükle ===
        var session = await _sessionContext.GetOrCreateAsync(request.SessionId);

        // === MOD GEÇİŞİ KONTROLÜ ===
        if (session.ActiveMode != request.Mode)
        {
            await _sessionContext.RecordModeSwitchAsync(
                session.SessionId,
                fromMode: session.ActiveMode,
                toMode: request.Mode);
        }

        // === TON DEĞİŞİM KONTROLÜ ===
        if (session.ActiveTone != request.Tone)
        {
            await _sessionContext.UpdateToneAsync(session.SessionId, request.Tone);
            session.ActiveTone = request.Tone;
        }

        if (request.Mode == ChatMode.Standard)
        {
            if (onProgress != null) await onProgress("Soru AI modeline gönderiliyor...");
            return await HandleStandardModeAsync(request, session, cancellationToken);
        }

        // Notisight modu → mevcut 4 katmanlı pipeline
        return await HandleNotisightModeAsync(request, session, userId, onProgress, cancellationToken);
    }

    private async IAsyncEnumerable<string> YieldSingleAsync(string text)
    {
        yield return text;
        await Task.CompletedTask;
    }

    /// <summary>
    /// Standard mod: Serbest AI sohbeti. RAG pipeline çalışmaz.
    /// </summary>
    private async Task<OrchestratorStreamResult> HandleStandardModeAsync(
        AskRequest request,
        SessionContext session,
        CancellationToken cancellationToken)
    {
        var stream = _llmChatService.FreeChatStreamAsync(request.Question, request.History, session, cancellationToken, session.ActiveTone);

        return new OrchestratorStreamResult
        {
            AnswerStream = stream,
            SessionId = session.SessionId,
            GuvenSeviyesi = "yuksek", // Standard modda güven kavramı yok
            ProducedByMode = ChatMode.Standard,
            ActiveTone = session.ActiveTone,
            Sources = new List<AskSourceReference>()
        };
    }

    /// <summary>
    /// Notisight modu: 4 katmanlı pipeline (Intent → Retrieval → Confidence → RAG).
    /// </summary>
    private async Task<OrchestratorStreamResult> HandleNotisightModeAsync(
        AskRequest request,
        SessionContext session,
        Guid userId,
        Func<string, Task>? onProgress,
        CancellationToken cancellationToken)
    {
        if (onProgress != null) await onProgress("Kullanıcı niyeti analiz ediliyor...");
        // === KATMAN 1: Niyet Analizi ===
        var intent = await _intentParser.ParseAsync(request.Question, session);

        if (!intent.ShouldProceedToRetrieval)
        {
            return new OrchestratorStreamResult
            {
                AnswerStream = YieldSingleAsync(intent.ClarificationQuestion!),
                IsClarificationRequest = true,
                SessionId = session.SessionId,
                GuvenSeviyesi = "orta",
                ProducedByMode = ChatMode.Notisight
            };
        }

        if (onProgress != null) await onProgress("Bilgi havuzu taranıyor...");
        // === KATMAN 2: Akıllı Arama ===
        var chunks = await _smartRetrieval.RetrieveAsync(userId, request.Question, intent, cancellationToken);

        foreach (var chunk in chunks)
        {
            if (chunk.Chunk.NoteId != Guid.Empty)
                await _sessionContext.AddAccessedSourceAsync(session.SessionId, chunk.Chunk.NoteId.ToString());
        }

        if (onProgress != null) await onProgress($"{chunks.Count} kaynak analiz ediliyor...");
        // === KATMAN 3: Güven Değerlendirmesi ===
        var confidence = _confidenceEngine.Evaluate(chunks, intent);

        if (confidence == ConfidenceLevel.Low)
        {
            return new OrchestratorStreamResult
            {
                AnswerStream = YieldSingleAsync("Bunu notlarınızda bulamadım."),
                GuvenSeviyesi = "dusuk",
                SessionId = session.SessionId,
                Sources = new List<AskSourceReference>(),
                ProducedByMode = ChatMode.Notisight,
                ActiveTone = session.ActiveTone,
                SuggestModeSwitch = true // Frontend "Standard moda geçeyim mi?" gösterir
            };
        }

        if (onProgress != null) await onProgress("Kapsamlı cevap üretiliyor...");
        // === KATMAN 5: RAG cevap üretimi ===
        var ragResult = await _ragAnswer.AnswerStreamAsync(userId, request.Question, request.History, chunks, session, cancellationToken, session.ActiveTone);

        return new OrchestratorStreamResult
        {
            AnswerStream = ragResult.AnswerStream,
            GuvenSeviyesi = ragResult.GuvenSeviyesi,
            NetlestiriciSoru = ragResult.NetlestiriciSoru,
            Sources = ragResult.Sources,
            ChunkMap = ragResult.ChunkMap,
            SessionId = session.SessionId,
            ProducedByMode = ChatMode.Notisight,
            ActiveTone = session.ActiveTone,
            SuggestModeSwitch = false
        };
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
}
