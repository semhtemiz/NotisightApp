using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Notisight.Api.Features.AI.Contracts;
using Notisight.Api.Features.AI.Options;
using Notisight.Api.Infrastructure.Http;

namespace Notisight.Api.Features.AI.Services;

public class OpenAiChatService(
    HttpClient httpClient,
    IChatConfigurationProvider configProvider,
    ILogger<OpenAiChatService> logger,
    IToneProfileService toneProfileService) : ILlmChatService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private async Task<string?> SendToOpenAiAsync(
        object requestBody,
        CancellationToken cancellationToken)
    {
        var config = configProvider.Current;
        if (config == null || string.IsNullOrWhiteSpace(config.ApiKey))
            return "Sistem hatası: API yapılandırması bulunamadı. Lütfen ayarlardan API anahtarınızı girin.";

        using var response = await RetryableHttp.SendAsync(
            () =>
            {
                var baseUrl = config.CustomBaseUrl ?? GetDefaultBaseUrl(config.ProviderType);
                var request = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
                request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
                request.Content = JsonContent.Create(requestBody, options: JsonOptions);
                return request;
            },
            httpClient.SendAsync,
            logger,
            "OpenAI chat generation",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            await LogProviderFailureAsync(response, "OpenAI-compatible chat generation", cancellationToken);
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<OpenAiResponse>(cancellationToken: cancellationToken);
        var message = payload?.Choices?.FirstOrDefault()?.Message;
        return message is null ? null : ExtractTextContent(message.Content);
    }

    private async IAsyncEnumerable<string> SendToOpenAiStreamAsync(
        object requestBody,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var config = configProvider.Current;
        if (config == null || string.IsNullOrWhiteSpace(config.ApiKey))
        {
            yield return "Sistem hatası: API yapılandırması bulunamadı. Lütfen ayarlardan API anahtarınızı girin.";
            yield break;
        }

        var baseUrl = config.CustomBaseUrl ?? GetDefaultBaseUrl(config.ProviderType);
        var requestUrl = $"{baseUrl}/chat/completions";
        var requestMsg = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        requestMsg.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
        requestMsg.Content = JsonContent.Create(requestBody, options: JsonOptions);

        using var response = await httpClient.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            yield return await BuildProviderFailureMessageAsync(response, cancellationToken);
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            if (line.StartsWith("data: "))
            {
                var dataStr = line.Substring(6).Trim();
                if (dataStr == "[DONE]") break;

                OpenAiStreamResponse? payload = null;
                try
                {
                    payload = JsonSerializer.Deserialize<OpenAiStreamResponse>(dataStr, JsonOptions);
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Failed to parse OpenAI stream chunk: {Data}", dataStr);
                    continue;
                }

                var delta = payload?.Choices?.FirstOrDefault()?.Delta;
                var chunkText = delta is null ? null : ExtractTextContent(delta.Content);
                if (!string.IsNullOrEmpty(chunkText))
                {
                    yield return chunkText;
                }
            }
        }
    }

    private async IAsyncEnumerable<string> EmptyStreamAsync()
    {
        yield break;
    }

    private async Task LogProviderFailureAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        var body = await ReadSafeBodyAsync(response, cancellationToken);
        logger.LogWarning(
            "{Operation} failed. StatusCode: {StatusCode}. Body: {Body}",
            operation,
            (int)response.StatusCode,
            body);
    }

    private async Task<string> BuildProviderFailureMessageAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        await LogProviderFailureAsync(response, "OpenAI-compatible stream generation", cancellationToken);

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                "AI sağlayıcısı API anahtarını kabul etmedi. API anahtarını ve sağlayıcı seçimini kontrol edin.",
            System.Net.HttpStatusCode.NotFound =>
                "AI sağlayıcısı seçilen modeli veya endpoint adresini bulamadı. Model seçimini ya da özel Base URL ayarını kontrol edin.",
            System.Net.HttpStatusCode.TooManyRequests =>
                "AI sağlayıcısı kullanım limitine takıldı. Biraz bekleyip tekrar deneyin veya sağlayıcı kotasını kontrol edin.",
            System.Net.HttpStatusCode.BadRequest =>
                "AI sağlayıcısı isteği geçersiz buldu. Model seçimini ve API yapılandırmasını kontrol edin.",
            _ =>
                $"AI sağlayıcısı şu an yanıt veremiyor. HTTP {(int)response.StatusCode} ({response.ReasonPhrase})."
        };
    }

    private static async Task<string> ReadSafeBodyAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(body))
            {
                return "<empty>";
            }

            return body.Length <= 1000 ? body : body[..1000];
        }
        catch
        {
            return "<unreadable>";
        }
    }

    public async Task<QueryIntent> ExtractIntentAsync(string query, SessionContext? sessionContext, CancellationToken cancellationToken)
    {
        var config = configProvider.Current;
        if (config == null || string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return new QueryIntent();
        }

        var systemPrompt = string.Join(
            Environment.NewLine,
            new string[]
            {
                "Sen kullanıcının Notisight'taki bağlamını (sorusunun niyetini) analiz eden bir yardımcı zekasın.",
                "Lütfen DÜZ JSON çıktısı üret. Başında/sonunda markdown vb. olmasın.",
                "Çıktı formatı:",
                "{",
                "  \"should_proceed_to_retrieval\": true/false,",
                "  \"optimized_search_query\": \"(eğer true ise vektör araması için optimize edilmiş sorgu, değilse null)\",",
                "  \"clarification_question\": \"(eğer false ise kullanıcıya sorulacak netleştirici soru, değilse null)\",",
                "  \"source_type_hint\": \"(eğer kullanıcı sadece ses, video, pdf veya docx gibi bir belge türünde arama yapmanı istiyorsa: audio, video, document, all. Varsayılan all)\",",
                "  \"key_entities\": [\"(sorgudaki anahtar kelimeler/özel isimler/kavramlar. yoksa boş dizi)\"]",
                "}",
                "",
                "ÖNEMLİ KURALLAR:",
                "- Kullanıcı 'sample adında bir ses dosyası' diyorsa, key_entities: [\"sample\"] olmalı, source_type_hint: \"audio\" olmalı.",
                "- 'optimized_search_query', kullanıcının bağlamıyla uyumlu en iyi RAG vektör arama cümlesi olmalı.",
                "- 'key_entities', kelime bazlı aramada (Keyword Search) başlıkları öne çıkarmak için kullanılır. Sorudaki *en özel, en spesifik* isimleri buraya al."
            });

        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = $"KULLANICI SORGUSU: {query}" }
        };

        var requestBody = CreateChatRequest(
            config.ModelId,
            messages,
            temperature: 0.1,
            responseFormat: new { type = "json_object" });

        var jsonResponse = await SendToOpenAiAsync(requestBody, cancellationToken);

        if (string.IsNullOrWhiteSpace(jsonResponse))
        {
            return new QueryIntent();
        }

        try
        {
            var cleanJson = jsonResponse.Trim();
            if (cleanJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            {
                cleanJson = cleanJson.Substring(7);
            }
            if (cleanJson.EndsWith("```"))
            {
                cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
            }
            cleanJson = cleanJson.Trim();

            using var doc = JsonDocument.Parse(cleanJson);
            var root = doc.RootElement;

            var intent = new QueryIntent
            {
                SourceTypeHint = root.TryGetProperty("source_type_hint", out var sth) && sth.ValueKind == JsonValueKind.String 
                                 ? sth.GetString() ?? "all" : "all"
            };

            if (root.TryGetProperty("key_entities", out var ke) && ke.ValueKind == JsonValueKind.Array)
            {
                var entities = ke.EnumerateArray()
                    .Where(x => x.ValueKind == JsonValueKind.String)
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Cast<string>()
                    .ToList();
                intent.KeyEntities = entities;
            }

            if (root.TryGetProperty("optimized_search_query", out var optQuery) && optQuery.ValueKind == JsonValueKind.String)
                intent.OptimizedSearchQuery = optQuery.GetString();

            if (root.TryGetProperty("clarification_question", out var clQuestion) && clQuestion.ValueKind == JsonValueKind.String)
                intent.ClarificationQuestion = clQuestion.GetString();

            return intent;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse intent JSON from OpenAI.");
            return new QueryIntent();
        }
    }

    public async Task<string?> FreeChatAsync(
        string userMessage,
        IReadOnlyList<ChatHistoryMessage>? history,
        SessionContext? sessionContext,
        CancellationToken cancellationToken,
        PersonalityTone tone = PersonalityTone.Casual)
    {
        var config = configProvider.Current;
        if (config == null || string.IsNullOrWhiteSpace(config.ApiKey)) 
            return "Lütfen ayarlar sayfasından API yapılandırmanızı tamamlayın.";

        var toneBlock = toneProfileService.GetSystemPromptBlock(tone);
        var messages = new List<object> { new { role = "system", content = toneBlock } };

        if (history != null && history.Count > 0)
        {
            foreach (var msg in history)
            {
                var role = msg.Role.Equals("ai", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
                messages.Add(new { role, content = msg.Text });
            }
        }

        messages.Add(new { role = "user", content = userMessage });

        var requestBody = CreateChatRequest(config.ModelId, messages, temperature: 0.7);

        return await SendToOpenAiAsync(requestBody, cancellationToken);
    }

    public IAsyncEnumerable<string> FreeChatStreamAsync(
        string userMessage,
        IReadOnlyList<ChatHistoryMessage>? history,
        SessionContext? sessionContext,
        CancellationToken cancellationToken,
        PersonalityTone tone = PersonalityTone.Casual)
    {
        var config = configProvider.Current;
        if (config == null || string.IsNullOrWhiteSpace(config.ApiKey)) 
            return EmptyStreamWithMessageAsync("Lütfen ayarlar sayfasından API yapılandırmanızı tamamlayın.");

        var toneBlock = toneProfileService.GetSystemPromptBlock(tone);
        var messages = new List<object> { new { role = "system", content = toneBlock } };

        if (history != null && history.Count > 0)
        {
            foreach (var msg in history)
            {
                var role = msg.Role.Equals("ai", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
                messages.Add(new { role, content = msg.Text });
            }
        }

        messages.Add(new { role = "user", content = userMessage });

        var requestBody = CreateChatRequest(config.ModelId, messages, temperature: 0.7, stream: true);

        return SendToOpenAiStreamAsync(requestBody, cancellationToken);
    }

    public async Task<string?> GenerateGroundedAnswerAsync(
        string query,
        IReadOnlyList<ChatHistoryMessage>? history,
        IReadOnlyList<string> contextChunks,
        CancellationToken cancellationToken,
        SessionContext? sessionContext = null,
        PersonalityTone tone = PersonalityTone.Casual)
    {
        var config = configProvider.Current;
        if (config == null || string.IsNullOrWhiteSpace(config.ApiKey)) 
            return "Lütfen ayarlar sayfasından API yapılandırmanızı tamamlayın.";

        var toneBlock = toneProfileService.GetSystemPromptBlock(tone);

        var modBilgisi = sessionContext?.ActiveMode == ChatMode.Notisight 
            ? "Mevcut Mod: Not Asistanı (Sadece sağlanan notlar üzerinden konuş)." 
            : "Mevcut Mod: Serbest Sohbet.";

        var userProfile = sessionContext?.Profile?.Domain == "professional"
            ? "Kullanıcı profesyoneldir."
            : "Kullanıcı hakkında henüz bir bilgi yok.";

        var systemPrompt = string.Join(
            Environment.NewLine,
            new string[]
            {
                "Sen kullanıcının özel notlarını analiz eden akıllı bir asistan 'Notisight'sın.",
                modBilgisi,
                toneBlock,
                "",
                "Aşağıda 'BAĞLAM' başlığı altında kullanıcının sorusuyla alakalı olabilecek not parçaları verilmiştir. Her parçanın başında [ID: c1] gibi bir kimlik vardır.",
                "",
                "1. CEVAP ÜRETİMİ",
                "- Gelen soruyu YALNIZCA BAĞLAM'daki bilgilere dayanarak yanıtla.",
                "- Cümleleri kendi kelimelerinle akıcı bir şekilde kur.",
                "",
                "2. KAYNAK GÖSTERİMİ (ATIF)",
                "- Söylediğin her bilginin veya çıkardığın sonucun sonuna ilgili bağlam ID'sini ZORUNLU olarak ekle. Format: '[ID: c1]'",
                "- Robotik bir dil kullanma, akıcı bir paragraf oluştur.",
                "",
                "3. EKSİK BİLGİ DURUMU (ÖNEMLİ)",
                "- Eğer sorulan bilgi sağlanan bağlamda KESİNLİKLE yoksa ve dolaylı yoldan da çıkarılamıyorsa, halüsinasyon yapma. O zaman açıkça 'Bunu notlarınızda bulamadım.' de.",
                "- Ancak ufak tefek ipuçları veya kısmi bilgiler varsa, 'Notlarınızda doğrudan geçmiyor ancak şu notta şöyle bir bilgi var: ...' şeklinde yönlendirme yap.",
                "",
                "[KULLANICI PROFİLİ]",
                userProfile,
                "",
                "# ÇIKTI FORMATI",
                "Lütfen yanıtını DOĞRUDAN DÜZ METİN VE MARKDOWN olarak ver. JSON formatı KULLANMA."
            });

        var messages = new List<object>();

        if (history != null && history.Count > 0)
        {
            foreach (var msg in history)
            {
                var role = msg.Role.Equals("ai", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
                messages.Add(new { role, content = msg.Text });
            }
        }

        var currentMessage = string.Join(
            Environment.NewLine,
            new string[]
            {
                systemPrompt,
                "",
                "BAĞLAM (Kullanıcının Notları):",
                "---------------------",
                string.Join(Environment.NewLine + Environment.NewLine, contextChunks),
                "---------------------",
                "",
                $"KULLANICININ SORUSU: {query}"
            });

        messages.Add(new { role = "user", content = currentMessage });

        var requestBody = CreateChatRequest(config.ModelId, messages, temperature: 0.1);

        return await SendToOpenAiAsync(requestBody, cancellationToken);
    }

    public IAsyncEnumerable<string> GenerateGroundedAnswerStreamAsync(
        string query,
        IReadOnlyList<ChatHistoryMessage>? history,
        List<string> contextChunks,
        CancellationToken cancellationToken,
        SessionContext? sessionContext = null,
        PersonalityTone tone = PersonalityTone.Casual)
    {
        var config = configProvider.Current;
        if (config == null || string.IsNullOrWhiteSpace(config.ApiKey)) 
            return EmptyStreamWithMessageAsync("Lütfen ayarlar sayfasından API yapılandırmanızı tamamlayın.");

        var toneBlock = toneProfileService.GetSystemPromptBlock(tone);

        var modBilgisi = sessionContext?.ActiveMode == ChatMode.Notisight 
            ? "Mevcut Mod: Not Asistanı (Sadece sağlanan notlar üzerinden konuş)." 
            : "Mevcut Mod: Serbest Sohbet.";

        var userProfile = sessionContext?.Profile?.Domain == "professional"
            ? "Kullanıcı profesyoneldir."
            : "Kullanıcı hakkında henüz bir bilgi yok.";

        var systemPrompt = string.Join(
            Environment.NewLine,
            new string[]
            {
                "Sen kullanıcının özel notlarını analiz eden akıllı bir asistan 'Notisight'sın.",
                modBilgisi,
                toneBlock,
                "",
                "Aşağıda 'BAĞLAM' başlığı altında kullanıcının sorusuyla alakalı olabilecek not parçaları verilmiştir. Her parçanın başında [ID: c1] gibi bir kimlik vardır.",
                "",
                "1. CEVAP ÜRETİMİ",
                "- Gelen soruyu YALNIZCA BAĞLAM'daki bilgilere dayanarak yanıtla.",
                "- Cümleleri kendi kelimelerinle akıcı bir şekilde kur.",
                "",
                "2. KAYNAK GÖSTERİMİ (ATIF)",
                "- Söylediğin her bilginin veya çıkardığın sonucun sonuna ilgili bağlam ID'sini ZORUNLU olarak ekle. Format: '[ID: c1]'",
                "- Robotik bir dil kullanma, akıcı bir paragraf oluştur.",
                "",
                "3. EKSİK BİLGİ DURUMU (ÖNEMLİ)",
                "- Eğer sorulan bilgi sağlanan bağlamda KESİNLİKLE yoksa ve dolaylı yoldan da çıkarılamıyorsa, halüsinasyon yapma. O zaman açıkça 'Bunu notlarınızda bulamadım.' de.",
                "- Ancak ufak tefek ipuçları veya kısmi bilgiler varsa, 'Notlarınızda doğrudan geçmiyor ancak şu notta şöyle bir bilgi var: ...' şeklinde yönlendirme yap.",
                "",
                "[KULLANICI PROFİLİ]",
                userProfile,
                "",
                "# ÇIKTI FORMATI",
                "Lütfen yanıtını DOĞRUDAN DÜZ METİN VE MARKDOWN olarak ver. JSON formatı KULLANMA."
            });

        var messages = new List<object>();

        if (history != null && history.Count > 0)
        {
            foreach (var msg in history)
            {
                var role = msg.Role.Equals("ai", StringComparison.OrdinalIgnoreCase) ? "assistant" : "user";
                messages.Add(new { role, content = msg.Text });
            }
        }

        var currentMessage = string.Join(
            Environment.NewLine,
            new string[]
            {
                systemPrompt,
                "",
                "BAĞLAM (Kullanıcının Notları):",
                "---------------------",
                string.Join(Environment.NewLine + Environment.NewLine, contextChunks),
                "---------------------",
                "",
                $"KULLANICININ SORUSU: {query}"
            });

        messages.Add(new { role = "user", content = currentMessage });

        var requestBody = CreateChatRequest(config.ModelId, messages, temperature: 0.1, stream: true);

        return SendToOpenAiStreamAsync(requestBody, cancellationToken);
    }

    public async Task<string?> GenerateTitleAsync(string question, CancellationToken cancellationToken)
    {
        var config = configProvider.Current;
        if (config == null || string.IsNullOrWhiteSpace(config.ApiKey)) 
            return "Yeni Sohbet";

        var messages = new List<object>
        {
            new { role = "system", content = "Sen sohbet başlıkları oluşturan bir asistansın. Kullanıcının ilk sorusuna bakarak en fazla 4 kelimelik kısa ve öz bir başlık üret. Noktalama işareti veya tırnak kullanma." },
            new { role = "user", content = question }
        };

        var requestBody = CreateChatRequest(config.ModelId, messages, temperature: 0.5);

        var response = await SendToOpenAiAsync(requestBody, cancellationToken);
        return response?.Trim('\"', ' ', '\n', '\r');
    }

    // Utilities
    private string GetDefaultBaseUrl(Notisight.Api.Features.Settings.Enums.ProviderType providerType)
    {
        return providerType switch
        {
            Notisight.Api.Features.Settings.Enums.ProviderType.DashScope => "https://dashscope-intl.aliyuncs.com/compatible-mode/v1",
            Notisight.Api.Features.Settings.Enums.ProviderType.Anthropic => "https://api.anthropic.com/v1", // Note: requires different JSON structure usually, but we assume OpenAI compat proxy
            Notisight.Api.Features.Settings.Enums.ProviderType.Gemini => "https://generativelanguage.googleapis.com/v1beta/openai",
            Notisight.Api.Features.Settings.Enums.ProviderType.DeepSeek => "https://api.deepseek.com",
            Notisight.Api.Features.Settings.Enums.ProviderType.OpenRouter => "https://openrouter.ai/api/v1",
            Notisight.Api.Features.Settings.Enums.ProviderType.Grok => "https://api.x.ai/v1",
            _ => "https://api.openai.com/v1"
        };
    }

    private static Dictionary<string, object?> CreateChatRequest(
        string modelId,
        List<object> messages,
        double? temperature = null,
        object? responseFormat = null,
        bool stream = false)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = modelId,
            ["messages"] = messages
        };

        if (temperature.HasValue && SupportsTemperature(modelId))
        {
            body["temperature"] = temperature.Value;
        }

        if (responseFormat is not null && SupportsResponseFormat(modelId))
        {
            body["response_format"] = responseFormat;
        }

        if (stream)
        {
            body["stream"] = true;
        }

        return body;
    }

    private static bool SupportsTemperature(string modelId)
    {
        var normalized = modelId.Trim().ToLowerInvariant();
        return !normalized.Contains("gpt-5") &&
               !normalized.Contains("claude") &&
               !normalized.Contains("/o1") &&
               !normalized.Contains("/o3") &&
               !normalized.Contains("/o4") &&
               !normalized.StartsWith("o1", StringComparison.Ordinal) &&
               !normalized.StartsWith("o3", StringComparison.Ordinal) &&
               !normalized.StartsWith("o4", StringComparison.Ordinal);
    }

    private static bool SupportsResponseFormat(string modelId)
    {
        var normalized = modelId.Trim().ToLowerInvariant();
        return !normalized.Contains("gpt-5") &&
               !normalized.Contains("claude");
    }

    private async IAsyncEnumerable<string> EmptyStreamWithMessageAsync(string message)
    {
        yield return message;
        await Task.CompletedTask;
    }

    private static string? ExtractTextContent(JsonElement content)
    {
        if (content.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString();
        }

        if (content.ValueKind == JsonValueKind.Array)
        {
            var builder = new StringBuilder();
            foreach (var part in content.EnumerateArray())
            {
                var partText = ExtractTextContent(part);
                if (!string.IsNullOrEmpty(partText))
                {
                    builder.Append(partText);
                }
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        if (content.ValueKind == JsonValueKind.Object)
        {
            if (content.TryGetProperty("text", out var text))
            {
                return ExtractTextContent(text);
            }

            if (content.TryGetProperty("content", out var nestedContent))
            {
                return ExtractTextContent(nestedContent);
            }

            if (content.TryGetProperty("output_text", out var outputText))
            {
                return ExtractTextContent(outputText);
            }
        }

        return null;
    }

    // JSON Helper Classes
    private sealed class OpenAiResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAiChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChoice
    {
        [JsonPropertyName("message")]
        public OpenAiMessage? Message { get; set; }
    }

    private sealed class OpenAiMessage
    {
        [JsonPropertyName("content")]
        public JsonElement Content { get; set; }
    }

    private sealed class OpenAiStreamResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAiStreamChoice>? Choices { get; set; }
    }

    private sealed class OpenAiStreamChoice
    {
        [JsonPropertyName("delta")]
        public OpenAiDelta? Delta { get; set; }
    }

    private sealed class OpenAiDelta
    {
        [JsonPropertyName("content")]
        public JsonElement Content { get; set; }
    }
}
