using Notisight.Api.Features.AI.Contracts;

namespace Notisight.Api.Features.AI.Services;

public class ConfidenceEngineService : IConfidenceEngineService
{
    // RRF skorları çok küçüktür: max ~0.016 (1/61).
    // Bu eşikler RRF skalasına göre ayarlanmıştır.
    private const float HighScoreThreshold = 0.012f;
    private const float MediumScoreThreshold = 0.005f;

    public ConfidenceLevel Evaluate(List<SearchChunkResult> chunks, QueryIntent intent)
    {
        if (chunks.Count == 0)
            return ConfidenceLevel.Low;

        var topScore = chunks.Max(c => c.Score);

        // Chunk bulunduysa en az Medium döndür — Gemini'ye her zaman ulaşsın
        if (topScore >= HighScoreThreshold)
            return ConfidenceLevel.High;

        // Herhangi bir chunk varsa Gemini karar versin, biz bloke etmeyelim
        return ConfidenceLevel.Medium;
    }
}
