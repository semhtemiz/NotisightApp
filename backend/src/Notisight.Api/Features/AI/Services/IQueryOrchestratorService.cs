using Notisight.Api.Features.AI.Contracts;

namespace Notisight.Api.Features.AI.Services;

public interface IQueryOrchestratorService
{
    Task<OrchestratorStreamResult> ProcessAsync(AskRequest request, Guid userId, Func<string, Task>? onProgress, CancellationToken cancellationToken);
}

public class OrchestratorStreamResult
{
    public IAsyncEnumerable<string> AnswerStream { get; set; } = default!;
    public string GuvenSeviyesi { get; set; } = "yuksek";
    public string? NetlestiriciSoru { get; set; }
    public List<AskSourceReference> Sources { get; set; } = new();
    public List<CitationReference> Citations { get; set; } = new();
    public Dictionary<string, SearchChunkResult>? ChunkMap { get; set; }
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// true ise bu yanıt kullanıcıya doğrudan netleştirici soru olarak döndürülür,
    /// RAG cevabı değildir. Frontend bunu sıradan bir AI mesajı gibi gösterebilir.
    /// </summary>
    public bool IsClarificationRequest { get; set; }

    /// <summary>
    /// true gelirse Notisight modunda konu notlarda bulunamadı demektir.
    /// Frontend bunu görünce "Standard moda geçeyim mi?" chip'i gösterir.
    /// </summary>
    public bool SuggestModeSwitch { get; set; }

    /// <summary>
    /// Yanıtın hangi modda üretildiği. Frontend rozet gösterimi için kullanır.
    /// </summary>
    public ChatMode ProducedByMode { get; set; }

    /// <summary>
    /// Bu yanıtı üreten aktif ton.
    /// Frontend ton seçici UI'ının senkron kalması için kullanılır.
    /// </summary>
    public PersonalityTone ActiveTone { get; set; } = PersonalityTone.Casual;
}
