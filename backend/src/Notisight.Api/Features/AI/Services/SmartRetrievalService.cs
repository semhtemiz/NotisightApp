using Notisight.Api.Features.AI.Contracts;
using Notisight.Api.Options;
using Microsoft.Extensions.Options;

namespace Notisight.Api.Features.AI.Services;

public class SmartRetrievalService : ISmartRetrievalService
{
    private readonly IChunkSearchService _chunkSearchService;
    private readonly RagOptions _ragOptions;

    public SmartRetrievalService(IChunkSearchService chunkSearchService, IOptions<RagOptions> ragOptions)
    {
        _chunkSearchService = chunkSearchService;
        _ragOptions = ragOptions.Value;
    }

    public async Task<List<SearchChunkResult>> RetrieveAsync(
        Guid userId,
        string query,
        QueryIntent intent,
        CancellationToken cancellationToken)
    {
        // Aşama 1: Hibrit Arama (RRF ile birleştirilmiş Vektör + Kelime)
        var rawResults = await _chunkSearchService.SearchAsync(userId, query, intent, cancellationToken);

        // Aşama 2: Reranking (Soft Boosting)
        // Katı filtre yerine mantıksal puanlama
        var reranked = rawResults
            .Select(r => new
            {
                Result = r,
                BoostedScore = ComputeBoostedScore(r, query, intent)
            })
            .OrderByDescending(x => x.BoostedScore)
            .Take(Math.Max(1, _ragOptions.TopK))
            .Select(x => x.Result with { Score = x.BoostedScore })
            .ToList();

        return reranked;
    }

    private static float ComputeBoostedScore(
        SearchChunkResult chunk,
        string query,
        QueryIntent intent)
    {
        float score = (float)chunk.Score; 

        // Kaynak tipi uyuşuyorsa Soft Boost (RRF skalasıyla orantılı)
        if (!string.IsNullOrEmpty(intent.SourceTypeHint))
        {
            if (string.Equals(chunk.Chunk.SourceType, intent.SourceTypeHint, StringComparison.OrdinalIgnoreCase))
            {
                score += 0.005f;
            }
        }

        var chunkTextLower = chunk.Chunk.Content?.ToLowerInvariant() ?? "";
        var titleLower = chunk.Chunk.Title?.ToLowerInvariant() ?? "";

        // KeyEntities eşleşmeleri — başlık eşleşmesi en güçlü sinyal
        if (intent.KeyEntities?.Count > 0)
        {
            foreach (var entity in intent.KeyEntities)
            {
                var lowerEntity = entity.ToLowerInvariant();
                if (titleLower.Contains(lowerEntity))
                {
                    score += 0.01f; // Başlıkta eşleşme — güçlü sinyal
                }
                if (chunkTextLower.Contains(lowerEntity))
                {
                    score += 0.003f; // İçerikte eşleşme
                }
            }
        }

        return score;
    }
}
