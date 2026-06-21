using Notisight.Api.Features.AI.Contracts;

namespace Notisight.Api.Features.AI.Services;

public class RagAnswerStreamResult
{
    public IAsyncEnumerable<string> AnswerStream { get; set; } = default!;
    public string GuvenSeviyesi { get; set; } = "yuksek";
    public string? NetlestiriciSoru { get; set; }
    public List<AskSourceReference> Sources { get; set; } = new();
    public Dictionary<string, SearchChunkResult> ChunkMap { get; set; } = new();
}

public interface IRagAnswerService
{
    Task<RagAnswerStreamResult> AnswerStreamAsync(
        Guid userId,
        string query,
        IReadOnlyList<ChatHistoryMessage>? history,
        List<SearchChunkResult> chunks,
        SessionContext? sessionContext,
        CancellationToken cancellationToken,
        PersonalityTone tone = PersonalityTone.Casual);
}
