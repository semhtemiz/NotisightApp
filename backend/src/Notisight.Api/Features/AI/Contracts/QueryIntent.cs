namespace Notisight.Api.Features.AI.Contracts;

/// <summary>
/// IntentParserService'in sorguda tespit ettiği niyet ve meta bilgileri.
/// </summary>
public class QueryIntent
{
    /// <summary>
    /// 0.0 = tamamen net, 1.0 = tamamen belirsiz.
    /// 0.6 ve üzeri -> kullanıcıya netleştirici soru sor.
    /// </summary>
    public float AmbiguityScore { get; set; }

    /// <summary>
    /// Sorguda kaynak tipi ipucu varsa dolu gelir: "audio", "pdf", "md", "text".
    /// Yoksa null — tüm kaynak tipleri aranır.
    /// </summary>
    public string? SourceTypeHint { get; set; }

    /// <summary>
    /// Zaman ipucu: "recent" (geçen, dün, bu hafta), "specific" (2023, Ocak), null.
    /// </summary>
    public string? TimeHint { get; set; }

    /// <summary>
    /// Sorguda tespit edilen anahtar entity isimleri (proje adı, hata kodu, vb.)
    /// Hem vektör hem keyword aramasını güçlendirir.
    /// </summary>
    public List<string> KeyEntities { get; set; } = new();

    /// <summary>
    /// LLM tarafından üretilen, vektör araması için optimize edilmiş sorgu.
    /// Opsiyoneldir. Doluysa, aramada kullanıcı sorgusu yerine bu kullanılır.
    /// </summary>
    public string? OptimizedSearchQuery { get; set; }

    /// <summary>
    /// Eğer belirsizlik yüksekse ve kullanıcıya soru sorulacaksa bu alan dolu gelir.
    /// Örn: "Kastettiğiniz 2023'teki proje mi yoksa bu yılki mi?"
    /// </summary>
    public string? ClarificationQuestion { get; set; }

    /// <summary>
    /// Bu sorgu doğrudan cevaplamaya geçilebilir mi?
    /// false ise ClarificationQuestion kullanıcıya döndürülür, RAG çalıştırılmaz.
    /// </summary>
    public bool ShouldProceedToRetrieval => true;
}
