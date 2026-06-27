# Notisight — AI Davranış Tonu Seçimi: Uygulama Planı

> Bu plan, `notisight-ai-implementation-plan.md` tamamlandıktan sonra uygulanır.
> `notisight-dual-mode-plan.md` ile paralel veya sonrasında uygulanabilir, bağımlılık yok.
> Her adım bağımsız bir commit'e karşılık gelir, sırayı koru.

---

## Bağlam ve Hedef

Kullanıcı her chat oturumu başında (veya oturum içinde) bir davranış tonu seçer.
Seçilen ton sistem istemini dinamik olarak şekillendirir — aynı soruya farklı ton farklı cevap üretir.

**4 ton:**
- `Casual` — Samimi & kısa. Arkadaş gibi, lafı dolandırma.
- `Technical` — Teknik & hassas. Jargon kullan, detay ver.
- `Pedagogical` — Öğretici & sabırlı. Adım adım açıkla.
- `Formal` — Resmi & düzesinde. Profesyonel yazışma tonu.

**Kapsam:** Her oturum için ayrı seçilebilir. Oturum içinde de değiştirilebilir.
Seçim `SessionContext`'te saklanır, sistem istemini dinamik olarak etkiler.

---

## Etkilenen / Eklenen Dosyalar

```
Features/AI/
├── Contracts/
│   ├── AskRequest.cs               ← PersonalityTone alanı eklenir
│   ├── SessionContext.cs           ← ActiveTone alanı eklenir
│   └── OrchestratorResult.cs       ← ActiveTone yansıtılır (frontend için)
└── Services/
    ├── ToneProfileService.cs       ← YENİ: ton tanımları ve sistem istemi blokları
    ├── IToneProfileService.cs      ← YENİ: interface
    ├── GeminiChatService.cs        ← sistem istemi ton bloğu enjeksiyonu
    ├── SessionContextService.cs    ← ton değişimini kaydet
    └── QueryOrchestratorService.cs ← tonu session'a yaz
```

---

## Uygulama Planı

---

### Adım 1 — PersonalityTone Enum ve AskRequest Güncellemesi

**Dosya:** `Features/AI/Contracts/AskRequest.cs` → güncelle

```csharp
namespace Notisight.Features.AI.Contracts;

public enum PersonalityTone
{
    /// <summary>
    /// Samimi ve kısa. Arkadaş gibi konuşur, gereksiz detaydan kaçınır.
    /// </summary>
    Casual = 0,

    /// <summary>
    /// Teknik ve hassas. Jargon kullanır, detay verir, kaynak gösterir.
    /// </summary>
    Technical = 1,

    /// <summary>
    /// Öğretici ve sabırlı. Adım adım açıklar, örnekler verir.
    /// </summary>
    Pedagogical = 2,

    /// <summary>
    /// Resmi ve düzesinde. Profesyonel yazışma tonu, mesafeli ama net.
    /// </summary>
    Formal = 3
}
```

```csharp
// AskRequest sınıfına ekle:

/// <summary>
/// Bu oturum için seçilen davranış tonu.
/// Her mesajda gönderilebilir — değişirse SessionContext güncellenir.
/// Default Casual: en az sürtüşmeli başlangıç deneyimi.
/// </summary>
public PersonalityTone Tone { get; set; } = PersonalityTone.Casual;
```

---

### Adım 2 — SessionContext Güncellemesi

**Dosya:** `Features/AI/Contracts/SessionContext.cs` → güncelle

```csharp
// SessionContext sınıfına ekle:

/// <summary>
/// Oturumun anlık aktif tonu.
/// Her mesajda gelen Tone ile güncellenir.
/// </summary>
public PersonalityTone ActiveTone { get; set; } = PersonalityTone.Casual;
```

---

### Adım 3 — ToneProfileService (Yeni Servis)

**Dosya:** `Features/AI/Services/IToneProfileService.cs` → oluştur

```csharp
namespace Notisight.Features.AI.Services;

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
```

---

**Dosya:** `Features/AI/Services/ToneProfileService.cs` → oluştur

```csharp
namespace Notisight.Features.AI.Services;

/// <summary>
/// Her ton için sistem istemi bloklarını ve UI metadata'sını yönetir.
/// Yeni ton eklemek için sadece bu dosyaya dokunmak yeterlidir.
/// </summary>
public class ToneProfileService : IToneProfileService
{
    private static readonly Dictionary<PersonalityTone, ToneProfile> Profiles = new()
    {
        [PersonalityTone.Casual] = new ToneProfile
        {
            Tone = PersonalityTone.Casual,
            DisplayName = "Samimi",
            Description = "Arkadaş gibi, kısa ve net",
            Icon = "💬"
        },
        [PersonalityTone.Technical] = new ToneProfile
        {
            Tone = PersonalityTone.Technical,
            DisplayName = "Teknik",
            Description = "Detaylı, jargon kullanır",
            Icon = "⚙️"
        },
        [PersonalityTone.Pedagogical] = new ToneProfile
        {
            Tone = PersonalityTone.Pedagogical,
            DisplayName = "Öğretici",
            Description = "Adım adım, örneklerle açıklar",
            Icon = "📚"
        },
        [PersonalityTone.Formal] = new ToneProfile
        {
            Tone = PersonalityTone.Formal,
            DisplayName = "Resmi",
            Description = "Profesyonel, düzesinde",
            Icon = "📋"
        }
    };

    public ToneProfile GetProfile(PersonalityTone tone) => Profiles[tone];

    public string GetSystemPromptBlock(PersonalityTone tone) => tone switch
    {
        PersonalityTone.Casual => """
            [YANIT TONU: SAMİMİ]
            - Kısa ve net konuş. Gereksiz giriş cümlesi kurma.
            - "Merhaba! Tabii ki yardımcı olabilirim." gibi kalıplardan kaçın.
            - Doğrudan konuya gir. Arkadaşça ama saygılı.
            - Cevap 1-3 cümleyi geçmesin, yeterliyse.
            - Teknik terimler için parantez içinde kısa açıklama yeterli.
            """,

        PersonalityTone.Technical => """
            [YANIT TONU: TEKNİK]
            - Teknik terminolojiyi doğru ve tam kullan, açıklamaya gerek yok.
            - Gerektiğinde kod bloğu, komut satırı çıktısı veya yapılandırma örneği ver.
            - Neden/nasıl çalıştığını mekanizma düzeyinde açıkla.
            - Kısaltma, kısayol veya edge case varsa belirt.
            - Uzun ve detaylı cevap kabul edilebilir — doğruluk öncelikli.
            """,

        PersonalityTone.Pedagogical => """
            [YANIT TONU: ÖĞRETİCİ]
            - Konuyu adım adım ve mantıksal sırayla açıkla.
            - Her adımı somut bir örnekle destekle.
            - Karmaşık kavramları önce basit bir benzetmeyle gir, sonra detaylandır.
            - "Neden böyle?" sorusunu her önemli noktada yanıtla.
            - Kullanıcının kafasında net bir mental model oluşturmayı hedefle.
            - Özet veya "Aklında kalsın:" notu ile bitir.
            """,

        PersonalityTone.Formal => """
            [YANIT TONU: RESMİ]
            - Profesyonel ve ölçülü bir dil kullan.
            - Kısaltmalardan ve günlük konuşma ifadelerinden kaçın.
            - Nesnel ve tarafsız bir bakış açısı koru.
            - Bilgiyi düzenli ve yapılandırılmış biçimde sun.
            - Kesin ifadeler kullan; belirsiz veya spekülatif dil kullanma.
            """,

        _ => string.Empty
    };
}
```

**DI Kaydı:**

```csharp
services.AddSingleton<IToneProfileService, ToneProfileService>();
// Singleton: ton profilleri statik, her request'te yeniden oluşturulmasına gerek yok
```

---

### Adım 4 — SessionContextService Güncellemesi

**Dosya:** `Features/AI/Services/ISessionContextService.cs` → güncelle

```csharp
// Mevcut metodlara ek:

/// <summary>
/// Oturumun aktif tonunu günceller.
/// Gelen mesajdaki Tone, mevcut ActiveTone'dan farklıysa çağrılır.
/// </summary>
Task UpdateToneAsync(string sessionId, PersonalityTone tone);
```

**Dosya:** `Features/AI/Services/SessionContextService.cs` → güncelle

```csharp
public Task UpdateToneAsync(string sessionId, PersonalityTone tone)
{
    if (_cache.TryGetValue(CacheKey(sessionId), out SessionContext? context) && context != null)
    {
        context.ActiveTone = tone;
        _cache.Set(CacheKey(sessionId), context, SessionTtl);
    }
    return Task.CompletedTask;
}
```

---

### Adım 5 — GeminiChatService: Ton Bloğu Enjeksiyonu

**Dosya:** `Features/AI/Services/GeminiChatService.cs` → güncelle

#### 5a. Constructor'a `IToneProfileService` inject et:

```csharp
private readonly IToneProfileService _toneProfileService;

public GeminiChatService(
    IHttpClientFactory httpClientFactory,
    IToneProfileService toneProfileService,
    // ... mevcut parametreler
    )
{
    _toneProfileService = toneProfileService;
    // ...
}
```

#### 5b. `BuildSystemPrompt` metodunu güncelle:

```csharp
// ÖNCE:
private string BuildSystemPrompt(SessionContext? sessionContext = null)

// SONRA:
private string BuildSystemPrompt(
    SessionContext? sessionContext = null,
    PersonalityTone tone = PersonalityTone.Casual)
```

Metod içinde, mevcut sistem isteminin hemen başına ton bloğunu ekle:

```csharp
private string BuildSystemPrompt(
    SessionContext? sessionContext = null,
    PersonalityTone tone = PersonalityTone.Casual)
{
    var toneBlock = _toneProfileService.GetSystemPromptBlock(tone);

    // Ton bloğu en başa gelir — LLM ilk okuduğunu daha güçlü uygular
    var systemPrompt = $"""
        {toneBlock}

        [KİŞİSEL NOT ASİSTANI]
        ... (mevcut sistem istemi içeriği aynen korunur) ...
        """;

    // Mevcut oturum geçmişi ve profil enjeksiyonu da korunur
    // ...

    return systemPrompt;
}
```

#### 5c. `AskAsync` ve `FreeChatAsync` metodlarına ton parametresi ekle:

```csharp
// AskAsync:
public async Task<GeminiResponse> AskAsync(
    string prompt,
    List<SearchChunkResult> chunks,
    SessionContext? sessionContext = null,
    PersonalityTone tone = PersonalityTone.Casual)  // YENİ
{
    var systemPrompt = BuildSystemPrompt(sessionContext, tone);
    // ...
}

// FreeChatAsync:
public async Task<GeminiResponse> FreeChatAsync(
    string userMessage,
    SessionContext? sessionContext = null,
    PersonalityTone tone = PersonalityTone.Casual)  // YENİ
{
    // Standard modda da ton geçerli —
    // kullanıcı Samimi modda serbest sohbet edebilir
    var toneBlock = _toneProfileService.GetSystemPromptBlock(tone);
    // toneBlock'u prompt'a ekle
    // ...
}
```

---

### Adım 6 — QueryOrchestratorService Güncellemesi

**Dosya:** `Features/AI/Services/QueryOrchestratorService.cs` → güncelle

`ProcessAsync` içinde, oturum yüklendikten hemen sonra ton kontrolü ekle:

```csharp
public async Task<OrchestratorResult> ProcessAsync(AskRequest request)
{
    var session = await _sessionContext.GetOrCreateAsync(request.SessionId);

    // === TON DEĞİŞİM KONTROLÜ ===
    // Gelen ton oturumdakinden farklıysa güncelle
    if (session.ActiveTone != request.Tone)
    {
        await _sessionContext.UpdateToneAsync(session.SessionId, request.Tone);
        session.ActiveTone = request.Tone; // local referansı da güncelle
    }

    // Mod geçiş kontrolü (dual-mode plan'dan) ...

    // Standard mod:
    if (request.Mode == ChatMode.Standard)
    {
        return await HandleStandardModeAsync(request, session);
    }

    // Notisight modu:
    return await HandleNotisightModeAsync(request, session);
}
```

Her iki handler'da da tonu Gemini çağrısına geçir:

```csharp
// HandleStandardModeAsync içinde:
var geminiResponse = await _geminiChatService.FreeChatAsync(
    request.Query,
    session,
    session.ActiveTone);   // YENİ

// HandleNotisightModeAsync içinde (RagAnswerService üzerinden):
var ragResult = await _ragAnswer.AnswerAsync(
    request.Query,
    chunks,
    session,
    session.ActiveTone);   // YENİ
```

---

### Adım 7 — RagAnswerService Güncellemesi

**Dosya:** `Features/AI/Services/RagAnswerService.cs` → güncelle

`AnswerAsync` imzasına ton parametresi ekle:

```csharp
// ÖNCE:
public async Task<RagAnswerResult> AnswerAsync(
    string query,
    List<SearchChunkResult> chunks,
    SessionContext? sessionContext = null)

// SONRA:
public async Task<RagAnswerResult> AnswerAsync(
    string query,
    List<SearchChunkResult> chunks,
    SessionContext? sessionContext = null,
    PersonalityTone tone = PersonalityTone.Casual)  // YENİ
```

`GeminiChatService.AskAsync` çağrısına tonu geçir:

```csharp
var geminiResponse = await _geminiChatService.AskAsync(
    fullPrompt,
    chunks,
    sessionContext,
    tone);   // YENİ
```

---

### Adım 8 — OrchestratorResult ve AiController Güncellemesi

**`OrchestratorResult`'a ekle:**

```csharp
/// <summary>
/// Bu yanıtı üreten aktif ton.
/// Frontend ton seçici UI'ının senkron kalması için kullanılır.
/// </summary>
public PersonalityTone ActiveTone { get; set; } = PersonalityTone.Casual;
```

**`AiController.cs` SSE payload'ına ekle:**

```csharp
await SendSseEventAsync(context, new
{
    // ... mevcut alanlar ...
    activeTone = result.ActiveTone.ToString()   // YENİ: "Casual", "Technical" vb.
});
```

---

### Adım 9 — Ton Listesi Endpoint'i (Opsiyonel ama Önerilen)

Frontend'in ton seçeneklerini hard-code etmemesi için basit bir endpoint ekle.
Bu sayede ileride yeni ton eklendiğinde frontend değişmez.

**`AiController.cs`'e yeni endpoint ekle:**

```csharp
/// <summary>
/// Kullanılabilir ton profillerini döndürür.
/// Frontend ton seçici UI'ı bu endpoint'i kullanır.
/// </summary>
[HttpGet("tones")]
public IActionResult GetTones()
{
    var tones = Enum.GetValues<PersonalityTone>()
        .Select(t => _toneProfileService.GetProfile(t))
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
```

**DI için `AiController` constructor'ına `IToneProfileService` inject et:**

```csharp
private readonly IToneProfileService _toneProfileService;

public AiController(
    IQueryOrchestratorService orchestrator,
    IToneProfileService toneProfileService)
{
    _orchestrator = orchestrator;
    _toneProfileService = toneProfileService;
}
```

---

## Önemli Notlar

### Ton bloğunun sistem istemindeki yeri

Ton bloğu **her zaman en başa** gelir. LLM'ler sistem isteminin ilk kısmına daha güçlü uyar. Ton sonra gelirse diğer kurallar (sıfır halüsinasyon, JSON formatı vb.) tonu ezebilir.

### Standard mod + ton kombinasyonu

Standard modda da ton geçerlidir. Kullanıcı "Teknik" tonu seçip serbest sohbet edebilir. `FreeChatAsync` da ton bloğunu alır.

### Yeni ton ekleme

İleride yeni ton eklemek için sadece:
1. `PersonalityTone` enum'una yeni değer ekle
2. `ToneProfileService`'te `Profiles` dict'ine ve `GetSystemPromptBlock` switch'ine yeni case ekle

Başka hiçbir dosyaya dokunmak gerekmez.

### `IRagAnswerService` imzası

`RagAnswerService.AnswerAsync`'e ton parametresi eklendi. Interface'i de (`IRagAnswerService`) güncellemeyi unutma.

---

## Uygulama Sırası Özeti

| # | Dosya | İşlem | Bağımlılık |
|---|-------|--------|------------|
| 1 | `AskRequest.cs` | `PersonalityTone` enum + `Tone` alanı | — |
| 2 | `SessionContext.cs` | `ActiveTone` alanı | PersonalityTone |
| 3 | `IToneProfileService.cs` | Oluştur | PersonalityTone |
| 3 | `ToneProfileService.cs` | Oluştur | IToneProfileService |
| 4 | `ISessionContextService.cs` + impl | `UpdateToneAsync` ekle | PersonalityTone |
| 5 | `GeminiChatService.cs` | Ton enjeksiyonu, metod imzaları | ToneProfileService |
| 6 | `QueryOrchestratorService.cs` | Ton kontrolü + handler'lara geçir | UpdateToneAsync |
| 7 | `RagAnswerService.cs` | `AnswerAsync` imzası + Gemini çağrısı | — |
| 8 | `OrchestratorResult.cs` + Controller | `ActiveTone` alanı + SSE | — |
| 9 | `AiController.cs` | `/tones` endpoint (opsiyonel) | ToneProfileService |