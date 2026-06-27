using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Notisight.Api.Infrastructure.Http;
using Microsoft.Extensions.Options;
using Notisight.Api.Options;
using Notisight.Api.Features.AI.Contracts;

namespace Notisight.Api.Features.AI.Services;

public sealed class GeminiChatService(
    HttpClient httpClient,
    IOptions<GeminiOptions> geminiOptions,
    ILogger<GeminiChatService> logger,
    IToneProfileService toneProfileService) : ILlmChatService
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);
    private readonly GeminiOptions _options = geminiOptions.Value;

    public async Task<string?> GenerateGroundedAnswerAsync(
        string question,
        IReadOnlyList<ChatHistoryMessage>? history,
        IReadOnlyList<string> contextChunks,
        CancellationToken cancellationToken,
        SessionContext? sessionContext = null,
        PersonalityTone tone = PersonalityTone.Casual)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || contextChunks.Count == 0)
        {
            return null;
        }

        var userProfile = sessionContext?.Profile != null
            ? $"Kullanıcı alanı: {sessionContext.Profile.Domain}"
            : "";

        var modBilgisi = sessionContext?.ModeSwitchHistory.Count > 0
            ? string.Join(Environment.NewLine, [
                "[MOD BİLGİSİ]",
                "Bu konuşmada kullanıcı iki farklı mod arasında geçiş yapabilir:",
                "- [Serbest sohbet] etiketli mesajlar: Genel AI sohbeti, not bağlamı yok.",
                "- [Not asistanı] etiketli mesajlar: Notlardan cevap verilmiş, kaynak gösterilmiş.",
                "",
                "Şu an NOT ASİSTANI modundasın. Yalnızca sağlanan not bağlamına göre cevap ver.",
                "[Serbest sohbet] modunda konuşulanlar genel bağlam için kullanılabilir",
                "ama kaynak olarak gösterilmez ve not içeriği gibi davranılmaz.",
                ""
            ])
            : "";

        var toneBlock = toneProfileService.GetSystemPromptBlock(tone);

        var systemPrompt = string.Join(
            Environment.NewLine,
            [
                toneBlock,
                "",
                "# GÖREV: KİŞİSEL BİLGİ SENTEZLEYİCİ (AI ARAŞTIRMACI)",
                "Sen kullanıcının kişisel veri havuzundaki (PDF, Ses, Markdown) notlar üzerinde çalışan, belgeler arasında anlamsal bağlantılar kuran gelişmiş bir araştırmacısın.",
                "Görevin sadece kelime araması yapmak değil; bağlamı okumak, dolaylı referansları anlamak ve kullanıcının asıl niyetine yönelik tutarlı bir yanıt üretmektir.",
                "",
                modBilgisi,
                "# TEMEL KURALLAR",
                "",
                "1. BAĞLAM VE YORUMLAMA",
                "- Sana verilen BAĞLAM parçalarını dikkatlice incele. Her parça [ID: xxx] etiketine sahiptir.",
                "- Kullanıcının sorusundaki tarihler, kişi adları veya üstü kapalı ifadeleri bağlamdaki bilgilerle eşleştirmeye çalış (örneğin 'bugünkü' kelimesi notun tarihiyle veya içeriğiyle örtüşebilir).",
                "- Cevabını doğrudan ve doğal bir dille yaz.",
                "- Bilgileri birbirine bağla: Örneğin 'Şu notta bahsedilen hedef, diğer notta tamamlanmış görünüyor' gibi.",
                "",
                "2. KAYNAK GÖSTERİMİ (ATIF)",
                "- Söylediğin her bilginin veya çıkardığın sonucun sonuna ilgili bağlam ID'sini [ID: xyz] şeklinde ZORUNLU olarak ekle.",
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
            ]);

        var contents = new List<GeminiContent>();

        if (history != null && history.Count > 0)
        {
            foreach (var msg in history)
            {
                var role = msg.Role.Equals("ai", StringComparison.OrdinalIgnoreCase) ? "model" : "user";
                contents.Add(new GeminiContent([new GeminiPart(msg.Text)], role));
            }
        }

        var currentMessage = string.Join(
            Environment.NewLine,
            [
                systemPrompt,
                "",
                "BAĞLAM (Kullanıcının Notları):",
                "---------------------",
                string.Join(Environment.NewLine + Environment.NewLine, contextChunks),
                "---------------------",
                "",
                $"KULLANICININ SORUSU: {question}"
            ]);

        contents.Add(new GeminiContent([new GeminiPart(currentMessage)], "user"));

        return await SendToGeminiAsync(contents, temperature: 0.1f, responseMimeType: null, cancellationToken: cancellationToken);
    }

    public async Task<string?> FreeChatAsync(
        string userMessage,
        IReadOnlyList<ChatHistoryMessage>? history,
        SessionContext? sessionContext,
        CancellationToken cancellationToken,
        PersonalityTone tone = PersonalityTone.Casual)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return null;
        }

        var toneBlock = toneProfileService.GetSystemPromptBlock(tone);
        var prompt = $"{toneBlock}\n\nKullanıcı: {userMessage}";

        var contents = new List<GeminiContent>();

        if (history != null && history.Count > 0)
        {
            foreach (var msg in history)
            {
                var role = msg.Role.Equals("ai", StringComparison.OrdinalIgnoreCase) ? "model" : "user";
                contents.Add(new GeminiContent([new GeminiPart(msg.Text)], role));
            }
        }

        contents.Add(new GeminiContent([new GeminiPart(prompt)], "user"));

        // Standard modda: sistem istemi yok, JSON zorunluluğu yok, daha yaratıcı temperature
        return await SendToGeminiAsync(contents, temperature: 0.7f, responseMimeType: null, cancellationToken: cancellationToken);
    }

    public async Task<string?> GenerateTitleAsync(string question, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return "Yeni Sohbet";
        }

        var prompt = $"Şu soru için 2-4 kelimelik kısa ve öz bir sohbet başlığı oluştur. Lütfen tırnak işareti, nokta veya başka noktalama işaretleri kullanma. Sadece başlığı yaz. Soru: {question}";

        var contents = new List<GeminiContent>
        {
            new([new GeminiPart(prompt)], "user")
        };

        var title = await SendToGeminiAsync(contents, temperature: 0.7f, responseMimeType: null, cancellationToken: cancellationToken);
        return title?.Trim('"', ' ', '\n', '\r') ?? "Yeni Sohbet";
    }

    /// <summary>
    /// Ortak Gemini HTTP çağrısı. Tüm public metodlar bunu kullanır.
    /// </summary>
    private async Task<string?> SendToGeminiAsync(
        List<GeminiContent> contents,
        float temperature,
        string? responseMimeType,
        CancellationToken cancellationToken)
    {
        using var response = await RetryableHttp.SendAsync(
            () =>
            {
                var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"https://generativelanguage.googleapis.com/v1beta/models/{_options.ChatModel}:generateContent");
                request.Headers.Add("x-goog-api-key", _options.ApiKey);
                request.Content = JsonContent.Create(
                    new GeminiGenerateRequest(
                        contents.ToArray(),
                        new GeminiGenerationConfig(temperature, responseMimeType)),
                    options: JsonOptions);
                return request;
            },
            httpClient.SendAsync,
            logger,
            "Gemini chat generation",
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Gemini API returned {StatusCode}", response.StatusCode);
            return null;
        }

        var payload = await response.Content.ReadFromJsonAsync<GeminiGenerateResponse>(
            cancellationToken: cancellationToken);

        return payload?.Candidates?
            .SelectMany(x => x.Content?.Parts ?? [])
            .Select(x => x.Text)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }

    private async IAsyncEnumerable<string> SendToGeminiStreamAsync(
        List<GeminiContent> contents,
        float temperature,
        string? responseMimeType,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var requestUrl = $"https://generativelanguage.googleapis.com/v1beta/models/{_options.ChatModel}:streamGenerateContent?alt=sse";
        
        var requestMsg = new HttpRequestMessage(HttpMethod.Post, requestUrl);
        requestMsg.Headers.Add("x-goog-api-key", _options.ApiKey);
        requestMsg.Content = JsonContent.Create(
            new GeminiGenerateRequest(
                contents.ToArray(),
                new GeminiGenerationConfig(temperature, responseMimeType)),
            options: JsonOptions);

        using var response = await httpClient.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Gemini API returned {StatusCode}", response.StatusCode);
            yield return "Üzgünüm, şu an yanıt üretemiyorum.";
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

                GeminiGenerateResponse? payload = null;
                try
                {
                    payload = System.Text.Json.JsonSerializer.Deserialize<GeminiGenerateResponse>(dataStr, JsonOptions);
                }
                catch { continue; }

                var text = payload?.Candidates?
                    .SelectMany(x => x.Content?.Parts ?? [])
                    .Select(x => x.Text)
                    .FirstOrDefault(x => !string.IsNullOrEmpty(x));

                if (!string.IsNullOrEmpty(text))
                {
                    yield return text;
                }
            }
        }
    }

    private static string BuildSessionHistory(SessionContext? ctx)
    {
        if (ctx?.RecentTurns.Count is null or 0)
            return "Henüz geçmiş yok.";

        return string.Join("\n", ctx.RecentTurns.TakeLast(5)
            .Select(t =>
            {
                var modeLabel = t.Mode == ChatMode.Notisight ? "[Not asistanı]" : "[Serbest sohbet]";
                return $"Kullanıcı {modeLabel}: {t.UserMessage}\nAsistan: {t.AssistantMessage}";
            }));
    }

    private static string BuildConversationHistory(SessionContext? ctx)
    {
        if (ctx?.RecentTurns.Count is null or 0)
            return string.Empty;

        var lines = ctx.RecentTurns.TakeLast(5).Select(t =>
        {
            var modeLabel = t.Mode == ChatMode.Notisight ? "[Not asistanı]" : "[Serbest sohbet]";
            return $"Kullanıcı {modeLabel}: {t.UserMessage}\nAsistan: {t.AssistantMessage}";
        });

        return string.Join("\n\n", lines);
    }

    public async Task<QueryIntent> ExtractIntentAsync(string query, SessionContext? sessionContext, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return new QueryIntent();
        }

        var systemPrompt = string.Join(
            Environment.NewLine,
            [
                "Sen akıllı bir 'Niyet Okuyucu' (Intent Parser) sistemisin.",
                "Görecin, kullanıcının arama sorgusunu analiz etmek, gizli niyetini bulmak ve RAG arama motoru için optimize etmektir.",
                "",
                "# ÇIKTI FORMATI ZORUNLULUĞU",
                "JSON döndürmelisin. Şema:",
                "{",
                "  \"ambiguity_score\": 0.0 - 1.0, // Kullanıcı ne aradığını ne kadar net belirtmiş? (0.0: Çok net, 1.0: Çok belirsiz)",
                "  \"source_type_hint\": \"audio\" | \"pdf\" | \"md\" | null, // Kullanıcı ses, pdf veya not diyorsa belirt.",
                "  \"time_hint\": \"recent\" | \"specific\" | null, // Zamanla ilgili vurgu varsa.",
                "  \"key_entities\": [\"sample\", \"fatura\", \"proje_adi\"], // Aramada yüksek ağırlık verilecek KESİN dosya adları, kelimeler, özel isimler.",
                "  \"optimized_search_query\": \"...\", // Sadece anlamsal (vektörel) arama motoruna gönderilecek düzeltilmiş, İngilizce/Türkçe eşanlamlıları eklenmiş net arama cümlesi. (Eğer soru doğrudan arama cümlesiyse sadece düzeltilmiş halini yaz).",
                "  \"clarification_question\": \"...\" // Eğer ambiguity_score >= 0.6 ise sorulacak soru, değilse null.",
                "}",
                "",
                "ÖNEMLİ KURALLAR:",
                "- Kullanıcı 'sample adında bir ses dosyası' diyorsa, key_entities: [\"sample\"] olmalı, source_type_hint: \"audio\" olmalı.",
                "- 'optimized_search_query', kullanıcının bağlamıyla uyumlu en iyi RAG vektör arama cümlesi olmalı.",
                "- 'key_entities', kelime bazlı aramada (Keyword Search) başlıkları öne çıkarmak için kullanılır. Sorudaki *en özel, en spesifik* isimleri buraya al."
            ]);

        var currentMessage = $"KULLANICI SORGUSU: {query}";

        var contents = new List<GeminiContent>
        {
            new([new GeminiPart(systemPrompt)], "user"),
            new([new GeminiPart("Anladım, JSON formatında analiz edeceğim.")], "model"),
            new([new GeminiPart(currentMessage)], "user")
        };

        var jsonResponse = await SendToGeminiAsync(contents, temperature: 0.1f, responseMimeType: "application/json", cancellationToken: cancellationToken);

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
            else if (cleanJson.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                cleanJson = cleanJson.Substring(3);
            }

            if (cleanJson.EndsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                cleanJson = cleanJson.Substring(0, cleanJson.Length - 3);
            }
            cleanJson = cleanJson.Trim();

            using var doc = System.Text.Json.JsonDocument.Parse(cleanJson);
            var root = doc.RootElement;
            
            var intent = new QueryIntent();
            
            if (root.TryGetProperty("ambiguity_score", out var ambScore) && ambScore.ValueKind == System.Text.Json.JsonValueKind.Number)
                intent.AmbiguityScore = (float)ambScore.GetDouble();
                
            if (root.TryGetProperty("source_type_hint", out var srcHint) && srcHint.ValueKind == System.Text.Json.JsonValueKind.String)
                intent.SourceTypeHint = srcHint.GetString();
                
            if (root.TryGetProperty("time_hint", out var timeHint) && timeHint.ValueKind == System.Text.Json.JsonValueKind.String)
                intent.TimeHint = timeHint.GetString();
                
            if (root.TryGetProperty("key_entities", out var keyEnts) && keyEnts.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                intent.KeyEntities = keyEnts.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList();
            }
            
            if (root.TryGetProperty("optimized_search_query", out var optQuery) && optQuery.ValueKind == System.Text.Json.JsonValueKind.String)
                intent.OptimizedSearchQuery = optQuery.GetString();
                
            if (root.TryGetProperty("clarification_question", out var clQuestion) && clQuestion.ValueKind == System.Text.Json.JsonValueKind.String)
                intent.ClarificationQuestion = clQuestion.GetString();
                
            return intent;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse intent JSON from Gemini.");
            return new QueryIntent();
        }
    }

    private async IAsyncEnumerable<string> EmptyStreamAsync()
    {
        yield break;
    }

    public IAsyncEnumerable<string> FreeChatStreamAsync(
        string userMessage,
        IReadOnlyList<ChatHistoryMessage>? history,
        SessionContext? sessionContext,
        CancellationToken cancellationToken,
        PersonalityTone tone = PersonalityTone.Casual)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return EmptyStreamAsync();
        }

        var toneBlock = toneProfileService.GetSystemPromptBlock(tone);
        var prompt = $"{toneBlock}\n\nKullanıcı: {userMessage}";

        var contents = new List<GeminiContent>();

        if (history != null && history.Count > 0)
        {
            foreach (var msg in history)
            {
                var role = msg.Role.Equals("ai", StringComparison.OrdinalIgnoreCase) ? "model" : "user";
                contents.Add(new GeminiContent([new GeminiPart(msg.Text)], role));
            }
        }

        contents.Add(new GeminiContent([new GeminiPart(prompt)], "user"));

        return SendToGeminiStreamAsync(contents, temperature: 0.7f, responseMimeType: null, cancellationToken);
    }

    public IAsyncEnumerable<string> GenerateGroundedAnswerStreamAsync(
        string query,
        IReadOnlyList<ChatHistoryMessage>? history,
        List<string> contextChunks,
        CancellationToken cancellationToken,
        SessionContext? sessionContext = null,
        PersonalityTone tone = PersonalityTone.Casual)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return EmptyStreamAsync();
        }

        var toneBlock = toneProfileService.GetSystemPromptBlock(tone);

        var modBilgisi = sessionContext?.ActiveMode == ChatMode.Notisight 
            ? "Mevcut Mod: Not Asistanı (Sadece sağlanan notlar üzerinden konuş)." 
            : "Mevcut Mod: Serbest Sohbet.";

        var userProfile = "[Bilinmiyor]";
        if (sessionContext != null)
        {
            userProfile = $"- Mevcut Oturum: {sessionContext.SessionId}\n" +
                          $"- Son 5 Konuşma Geçmişi:\n{BuildSessionHistory(sessionContext)}";
        }

        var systemPrompt = string.Join(
            Environment.NewLine,
            [
                toneBlock,
                "",
                "# GÖREV: KİŞİSEL BİLGİ SENTEZLEYİCİ (AI ARAŞTIRMACI)",
                "Sen kullanıcının kişisel veri havuzundaki (PDF, Ses, Markdown) notlar üzerinde çalışan, belgeler arasında anlamsal bağlantılar kuran gelişmiş bir araştırmacısın.",
                "Görevin sadece kelime araması yapmak değil; bağlamı okumak, dolaylı referansları anlamak ve kullanıcının asıl niyetine yönelik tutarlı bir yanıt üretmektir.",
                "",
                modBilgisi,
                "# TEMEL KURALLAR",
                "",
                "1. BAĞLAM VE YORUMLAMA",
                "- Sana verilen BAĞLAM parçalarını dikkatlice incele. Her parça [ID: xxx] etiketine sahiptir.",
                "- Kullanıcının sorusundaki tarihler, kişi adları veya üstü kapalı ifadeleri bağlamdaki bilgilerle eşleştirmeye çalış (örneğin 'bugünkü' kelimesi notun tarihiyle veya içeriğiyle örtüşebilir).",
                "- Cevabını doğrudan ve doğal bir dille yaz.",
                "- Bilgileri birbirine bağla: Örneğin 'Şu notta bahsedilen hedef, diğer notta tamamlanmış görünüyor' gibi.",
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
            ]);

        var contents = new List<GeminiContent>();

        if (history != null && history.Count > 0)
        {
            foreach (var msg in history)
            {
                var role = msg.Role.Equals("ai", StringComparison.OrdinalIgnoreCase) ? "model" : "user";
                contents.Add(new GeminiContent([new GeminiPart(msg.Text)], role));
            }
        }

        var currentMessage = string.Join(
            Environment.NewLine,
            [
                systemPrompt,
                "",
                "BAĞLAM (Kullanıcının Notları):",
                "---------------------",
                string.Join(Environment.NewLine + Environment.NewLine, contextChunks),
                "---------------------",
                "",
                $"KULLANICININ SORUSU: {query}"
            ]);

        contents.Add(new GeminiContent([new GeminiPart(currentMessage)], "user"));

        return SendToGeminiStreamAsync(contents, temperature: 0.1f, responseMimeType: null, cancellationToken);
    }

    private sealed record GeminiGenerateRequest(
        [property: JsonPropertyName("contents")] GeminiContent[] Contents,
        [property: JsonPropertyName("generationConfig")] GeminiGenerationConfig? GenerationConfig = null);

    private sealed record GeminiGenerationConfig(
        [property: JsonPropertyName("temperature")] float Temperature,
        [property: JsonPropertyName("responseMimeType")] string? ResponseMimeType = null);

    private sealed record GeminiContent(
        [property: JsonPropertyName("parts")] GeminiPart[] Parts,
        [property: JsonPropertyName("role")] string Role);

    private sealed record GeminiPart(string Text);

    private sealed record GeminiGenerateResponse(
        [property: JsonPropertyName("candidates")] IReadOnlyList<GeminiCandidate>? Candidates);

    private sealed record GeminiCandidate(
        [property: JsonPropertyName("content")] GeminiResponseContent? Content);

    private sealed record GeminiResponseContent(
        [property: JsonPropertyName("parts")] IReadOnlyList<GeminiResponsePart>? Parts);

    private sealed record GeminiResponsePart(
        [property: JsonPropertyName("text")] string? Text);
}
