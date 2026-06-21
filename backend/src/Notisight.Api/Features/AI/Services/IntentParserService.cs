using Notisight.Api.Features.AI.Contracts;

namespace Notisight.Api.Features.AI.Services;

public class IntentParserService(ILlmChatService llmChatService, ILogger<IntentParserService> logger) : IIntentParserService
{
    private static readonly HashSet<string> StopWords = new()
    {
        "bir", "bu", "şu", "ve", "ile", "için", "olan", "gibi", "kadar",
        "nasıl", "neden", "nerede", "nedir", "hangi", "adında", "adındaki",
        "dosya", "dosyası", "dosyasında", "bana", "söyle", "anlat", "açıkla",
        "hakkında", "var", "olan", "içinde", "içindeki", "mı", "mi", "mu", "mü",
        "da", "de", "ki", "ne", "den", "dan", "the", "is", "what", "how"
    };

    public async Task<QueryIntent> ParseAsync(string query, SessionContext? sessionContext)
    {
        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var intent = await llmChatService.ExtractIntentAsync(query, sessionContext, cts.Token);
            
            // Eğer LLM tamamen başarısız olduysa fallback'e düş
            if (string.IsNullOrWhiteSpace(intent.OptimizedSearchQuery))
            {
                return GetFallbackIntent(query);
            }
            
            return intent;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "LLM tabanlı niyet okuma başarısız oldu, kurallı okumaya geçiliyor.");
            return GetFallbackIntent(query);
        }
    }

    private QueryIntent GetFallbackIntent(string query)
    {
        var lowerQuery = query.ToLowerInvariant();
        var intent = new QueryIntent
        {
            OptimizedSearchQuery = query
        };

        if (lowerQuery.Contains("ses") || lowerQuery.Contains("kayıt") || lowerQuery.Contains("audio"))
            intent.SourceTypeHint = "audio";
        else if (lowerQuery.Contains("pdf") || lowerQuery.Contains("belge"))
            intent.SourceTypeHint = "pdf";

        intent.KeyEntities = query.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3 && !StopWords.Contains(w.ToLowerInvariant()))
            .Take(5)
            .ToList();

        return intent;
    }
}
