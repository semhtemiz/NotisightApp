using System.Text.RegularExpressions;
using Notisight.Api.Features.AI.Contracts;

namespace Notisight.Api.Features.AI.Services;

public sealed partial class RagAnswerService : IRagAnswerService
{
    private readonly ILlmChatService _llmChatService;

    public RagAnswerService(ILlmChatService geminiChatService)
    {
        _llmChatService = geminiChatService;
    }

    public async Task<RagAnswerStreamResult> AnswerStreamAsync(
        Guid userId,
        string query,
        IReadOnlyList<ChatHistoryMessage>? history,
        List<SearchChunkResult> chunks,
        SessionContext? sessionContext,
        CancellationToken cancellationToken,
        PersonalityTone tone = PersonalityTone.Casual)
    {
        var sources = chunks
            .GroupBy(x => new { x.Chunk.NoteId, x.Chunk.Title })
            .Select(group => new AskSourceReference(group.Key.NoteId, group.Key.Title, group.Max(x => x.Score)))
            .OrderByDescending(x => x.Score)
            .ToList();

        var chunkMap = new Dictionary<string, SearchChunkResult>();
        var contextChunks = new List<string>();
        for (int i = 0; i < chunks.Count; i++)
        {
            var refId = $"c{i + 1}";
            var match = chunks[i];
            chunkMap[refId] = match;

            var locationInfo = !string.IsNullOrEmpty(match.Chunk.SourceLabel)
                ? $" ({match.Chunk.SourceLabel})"
                : "";

            contextChunks.Add(
                $"[ID: {refId}] Kaynak: {match.Chunk.Title}{locationInfo}\n{match.Chunk.Content}");
        }

        var fullPrompt = $"Soru: {query}";

        var stream = _llmChatService.GenerateGroundedAnswerStreamAsync(
            fullPrompt,
            history,
            contextChunks,
            cancellationToken,
            sessionContext,
            tone);

        var result = new RagAnswerStreamResult 
        { 
            Sources = sources,
            ChunkMap = chunkMap,
            AnswerStream = stream,
            GuvenSeviyesi = chunks.Count > 0 ? "yuksek" : "dusuk"
        };

        return result;
    }

    public static IReadOnlyList<CitationReference> ParseCitations(
        string answer,
        Dictionary<string, SearchChunkResult> chunkMap)
    {
        var citations = new List<CitationReference>();
        var seenIds = new HashSet<string>();

        foreach (Match match in RefPattern().Matches(answer))
        {
            var refId = match.Groups[1].Value.Trim();
            if (!seenIds.Add(refId) || !chunkMap.TryGetValue(refId, out var chunk))
                continue;

            citations.Add(new CitationReference(
                refId,
                chunk.Chunk.NoteId,
                chunk.Chunk.Title,
                chunk.Chunk.SourceType,
                chunk.Chunk.SourceLabel,
                Trim(chunk.Chunk.Content, 120)));
        }

        return citations;
    }

    [GeneratedRegex(@"\[ID:\s*(\w+)\]", RegexOptions.IgnoreCase)]
    private static partial Regex RefPattern();

    private static string Trim(string text, int length)
    {
        return text.Length <= length ? text : text[..length] + "...";
    }
}
