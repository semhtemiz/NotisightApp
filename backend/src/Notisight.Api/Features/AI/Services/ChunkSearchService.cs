using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notisight.Api.Features.AI.Contracts;
using Notisight.Api.Infrastructure.Persistence;
using Notisight.Api.Options;

namespace Notisight.Api.Features.AI.Services;

public sealed class ChunkSearchService(
    ApplicationDbContext dbContext,
    ITextChunkingService textChunkingService,
    IEmbeddingService embeddingService,
    IQdrantVectorService qdrantVectorService,
    ILogger<ChunkSearchService> logger,
    IOptions<RagOptions> ragOptions) : IChunkSearchService
{
    private readonly RagOptions _ragOptions = ragOptions.Value;

    public async Task<IReadOnlyList<SearchChunkResult>> SearchAsync(
        Guid userId,
        string question,
        QueryIntent intent,
        CancellationToken cancellationToken)
    {
        // 1. Vektör Araması (Anlamsal)
        var searchQuery = !string.IsNullOrWhiteSpace(intent.OptimizedSearchQuery) 
            ? intent.OptimizedSearchQuery 
            : question;

        IReadOnlyList<SearchChunkResult> vectorResults = [];
        try
        {
            var queryVector = await embeddingService.EmbedQueryAsync(searchQuery, cancellationToken);
            vectorResults = await qdrantVectorService.SearchAsync(
                userId,
                queryVector,
                _ragOptions.TopK * 3, // Daha geniş bir vektör havuzu (eski 2x yerine 3x)
                cancellationToken);
            vectorResults = vectorResults
                .Where(x => x.Score >= _ragOptions.MinVectorScore)
                .ToList();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Vector search failed for user {UserId}; continuing with keyword search fallback.",
                userId);
        }

        // 2. Kelime Araması (Keyword Search)
        var notes = await dbContext.Notes
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .ToListAsync(cancellationToken);
        var folderRows = await dbContext.Folders
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new FolderPathRow(x.Id, x.Name, x.ParentFolderId))
            .ToListAsync(cancellationToken);
        var folderMap = folderRows.ToDictionary(x => x.Id);

        // Arama terimlerini oluştur (Hem orijinal soru hem de LLM'den gelen anahtar kelimeler)
        var terms = question
            .Split((char[])null!, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.ToLowerInvariant())
            .ToList();
            
        if (intent.KeyEntities?.Count > 0)
        {
            terms.AddRange(intent.KeyEntities.Select(x => x.ToLowerInvariant()));
        }
        terms = terms.Distinct().ToList();

        var keywordResults = new List<SearchChunkResult>();
        foreach (var note in notes)
        {
            var folderPath = BuildFolderPath(note.FolderId, folderMap);
            var sourceType = string.IsNullOrWhiteSpace(note.FileType) ? "note" : note.FileType!;
            var searchableContent = CleanText(note.Content);
            var chunks = sourceType == "audio"
                ? textChunkingService.ChunkAudio(note.Id, note.Title, searchableContent, note.DurationSeconds)
                : textChunkingService.ChunkSource(note.Id, note.Title, searchableContent, sourceType);
            
            // İçeriği boş ama başlığı olan notlar için sanal chunk üret (başlık araması çalışsın)
            if (chunks.Count == 0 && !string.IsNullOrWhiteSpace(note.Title))
            {
                chunks = [new ChunkedNote(Guid.NewGuid(), note.Id, note.Title, note.Title, 0, sourceType ?? "note", "")];
            }

            foreach (var chunk in chunks)
            {
                float score = 0;
                var chunkContentLower = chunk.Content?.ToLowerInvariant() ?? "";
                var titleLower = chunk.Title?.ToLowerInvariant() ?? "";
                var folderPathLower = folderPath.ToLowerInvariant();
                var sourceLabelLower = chunk.SourceLabel?.ToLowerInvariant() ?? "";
                var sourceTypeLower = (sourceType ?? "note").ToLowerInvariant();

                foreach (var term in terms)
                {
                    if (term.Length <= 2) continue; // Çok kısa kelimeleri atla
                    
                    bool inTitle = titleLower.Contains(term, StringComparison.Ordinal);
                    bool inContent = chunkContentLower.Contains(term, StringComparison.Ordinal);
                    bool inFolderPath = folderPathLower.Contains(term, StringComparison.Ordinal);
                    bool inSourceLabel = sourceLabelLower.Contains(term, StringComparison.Ordinal);
                    bool inSourceType = sourceTypeLower.Contains(term, StringComparison.Ordinal);

                    if (inTitle) score += 5.0f; // Başlıkta eşleşme (Title Boosting)
                    if (inContent) score += 1.0f; // İçerikte eşleşme
                    if (inFolderPath) score += 4.0f;
                    if (inSourceLabel) score += 2.0f;
                    if (inSourceType) score += 1.5f;
                    
                    // LLM'in özellikle bulduğu KeyEntities kelimeleriyse daha yüksek puan ver
                    if (intent.KeyEntities != null && intent.KeyEntities.Any(e => e.Equals(term, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (inTitle) score += 15.0f; // Vektör puanlarıyla daha uyumlu bir oran
                        if (inContent) score += 3.0f;
                        if (inFolderPath) score += 12.0f;
                        if (inSourceLabel) score += 6.0f;
                    }
                }

                if (score > 0)
                {
                    var enrichedChunk = chunk with
                    {
                        UserId = note.UserId,
                        FolderId = note.FolderId,
                        FolderPath = folderPath,
                        DurationSeconds = note.DurationSeconds
                    };
                    keywordResults.Add(new SearchChunkResult(enrichedChunk, score));
                }
            }
        }

        // 3. Hibrit Birleştirme (Reciprocal Rank Fusion - RRF)
        var combined = new Dictionary<string, (SearchChunkResult Result, double RrfScore)>();

        int k = 60; // RRF sabiti
        
        var rankedVectors = vectorResults.OrderByDescending(x => x.Score).ToList();
        for (int i = 0; i < rankedVectors.Count; i++)
        {
            var res = rankedVectors[i];
            var key = $"{res.Chunk.NoteId}_{res.Chunk.Index}";
            var rrf = 1.0 / (k + i + 1);
            combined[key] = (res, rrf);
        }

        var rankedKeywords = keywordResults.OrderByDescending(x => x.Score).ToList();
        for (int i = 0; i < rankedKeywords.Count; i++)
        {
            var res = rankedKeywords[i];
            var key = $"{res.Chunk.NoteId}_{res.Chunk.Index}";
            var rrf = 1.0 / (k + i + 1);
            
            if (combined.TryGetValue(key, out var existing))
            {
                combined[key] = (existing.Result, existing.RrfScore + rrf);
            }
            else
            {
                combined[key] = (res, rrf);
            }
        }

        // Toplam RRF skoruna göre sırala
        return combined.Values
            .OrderByDescending(x => x.RrfScore)
            .Take(_ragOptions.TopK)
            .Select(x => x.Result with { Score = x.RrfScore })
            .ToList();
    }

    private static string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = System.Text.RegularExpressions.Regex.Replace(
            text,
            @"<(br|/p|/h[1-6]|/div|/li)>",
            "\n",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"<[^>]+>", " ");
        cleaned = System.Net.WebUtility.HtmlDecode(cleaned);
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[ \t]+", " ");
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\n\s*\n+", "\n\n");
        return cleaned.Trim();
    }

    private static string BuildFolderPath(
        Guid? folderId,
        IReadOnlyDictionary<Guid, FolderPathRow> folderMap)
    {
        if (!folderId.HasValue)
        {
            return "Ana dizin";
        }

        var visited = new HashSet<Guid>();
        var path = new List<string>();
        var currentFolderId = folderId;

        while (currentFolderId.HasValue &&
               visited.Add(currentFolderId.Value) &&
               folderMap.TryGetValue(currentFolderId.Value, out var folder))
        {
            path.Insert(0, folder.Name);
            currentFolderId = folder.ParentFolderId;
        }

        return path.Count > 0 ? string.Join(" / ", path) : "Ana dizin";
    }

    private sealed record FolderPathRow(Guid Id, string Name, Guid? ParentFolderId);
}
