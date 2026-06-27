using Microsoft.Extensions.Options;
using Notisight.Api.Features.AI.Contracts;
using Notisight.Api.Options;

namespace Notisight.Api.Features.AI.Services;

public sealed class TextChunkingService(IOptions<RagOptions> ragOptions) : ITextChunkingService
{
    private readonly RagOptions _options = ragOptions.Value;

    public IReadOnlyList<ChunkedNote> Chunk(Guid noteId, string title, string content)
    {
        return ChunkSource(noteId, title, content, "note");
    }

    public IReadOnlyList<ChunkedNote> ChunkSource(
        Guid noteId,
        string title,
        string content,
        string sourceType,
        string sourceLabel = "")
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var sentences = SplitSentences(content);
        if (sentences.Count == 0) return [];

        return ChunkSentences(noteId, title, sentences, sourceType, sourceLabel);
    }

    public IReadOnlyList<ChunkedNote> ChunkPages(
        Guid noteId, string title, IReadOnlyList<(int PageNumber, string Text)> pages)
    {
        if (pages.Count == 0) return [];

        var chunks = new List<ChunkedNote>();
        var chunkSize = Math.Max(32, _options.ChunkTokenTarget);
        var overlapSize = Math.Max(0, (int)Math.Round(chunkSize * (_options.ChunkOverlapPercent / 100d)));

        foreach (var (pageNumber, text) in pages)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;

            var sentences = SplitSentences(text);
            if (sentences.Count == 0) continue;

            var pageLabel = $"Sayfa {pageNumber}";
            var pageChunks = ChunkSentences(noteId, title, sentences, "pdf", pageLabel, chunks.Count);

            // Apply overlap from previous page's last chunk
            chunks.AddRange(pageChunks);
        }

        return chunks;
    }

    public IReadOnlyList<ChunkedNote> ChunkAudio(Guid noteId, string title, string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            return [];
        }

        var sentences = SplitSentences(transcript);
        if (sentences.Count == 0) return [];

        var totalWords = sentences.Sum(s => GetWordCount(s));
        var chunkSize = Math.Max(32, _options.ChunkTokenTarget);
        var overlapSize = Math.Max(0, (int)Math.Round(chunkSize * (_options.ChunkOverlapPercent / 100d)));

        var chunks = new List<ChunkedNote>();
        var currentChunkSentences = new List<string>();
        var currentWordCount = 0;
        var wordsProcessed = 0;

        for (int i = 0; i < sentences.Count; i++)
        {
            var sentence = sentences[i];
            var wordCount = GetWordCount(sentence);

            if (currentWordCount + wordCount > chunkSize && currentChunkSentences.Count > 0)
            {
                // Estimate timestamp based on word position ratio
                var ratio = totalWords > 0 ? (double)wordsProcessed / totalWords : 0;
                var label = FormatTimestamp(ratio);

                chunks.Add(new ChunkedNote(
                    Guid.NewGuid(),
                    noteId,
                    title,
                    string.Join(" ", currentChunkSentences),
                    chunks.Count,
                    "audio",
                    label));

                var overlapSentences = new List<string>();
                var currentOverlapWords = 0;
                for (int j = currentChunkSentences.Count - 1; j >= 0; j--)
                {
                    var sCount = GetWordCount(currentChunkSentences[j]);
                    if (currentOverlapWords + sCount > overlapSize && overlapSentences.Count > 0)
                        break;
                    overlapSentences.Insert(0, currentChunkSentences[j]);
                    currentOverlapWords += sCount;
                }

                currentChunkSentences.Clear();
                currentChunkSentences.AddRange(overlapSentences);
                currentWordCount = currentOverlapWords;
            }

            currentChunkSentences.Add(sentence);
            currentWordCount += wordCount;
            wordsProcessed += wordCount;
        }

        if (currentChunkSentences.Count > 0)
        {
            var ratio = totalWords > 0 ? (double)wordsProcessed / totalWords : 1;
            var label = FormatTimestamp(Math.Max(0, ratio - (double)currentWordCount / totalWords));

            chunks.Add(new ChunkedNote(
                Guid.NewGuid(),
                noteId,
                title,
                string.Join(" ", currentChunkSentences),
                chunks.Count,
                "audio",
                label));
        }

        return chunks;
    }

    private IReadOnlyList<ChunkedNote> ChunkSentences(
        Guid noteId, string title, List<string> sentences,
        string sourceType, string sourceLabel, int startIndex = 0)
    {
        var chunkSize = Math.Max(32, _options.ChunkTokenTarget);
        var overlapSize = Math.Max(0, (int)Math.Round(chunkSize * (_options.ChunkOverlapPercent / 100d)));

        var chunks = new List<ChunkedNote>();
        var currentChunkSentences = new List<string>();
        var currentWordCount = 0;

        for (int i = 0; i < sentences.Count; i++)
        {
            var sentence = sentences[i];
            var wordCount = GetWordCount(sentence);

            if (currentWordCount + wordCount > chunkSize && currentChunkSentences.Count > 0)
            {
                chunks.Add(new ChunkedNote(
                    Guid.NewGuid(),
                    noteId,
                    title,
                    string.Join(" ", currentChunkSentences),
                    startIndex + chunks.Count,
                    sourceType,
                    sourceLabel));

                var overlapSentences = new List<string>();
                var currentOverlapWords = 0;
                for (int j = currentChunkSentences.Count - 1; j >= 0; j--)
                {
                    var sCount = GetWordCount(currentChunkSentences[j]);
                    if (currentOverlapWords + sCount > overlapSize && overlapSentences.Count > 0)
                        break;
                    overlapSentences.Insert(0, currentChunkSentences[j]);
                    currentOverlapWords += sCount;
                }

                currentChunkSentences.Clear();
                currentChunkSentences.AddRange(overlapSentences);
                currentWordCount = currentOverlapWords;
            }

            currentChunkSentences.Add(sentence);
            currentWordCount += wordCount;
        }

        if (currentChunkSentences.Count > 0)
        {
            chunks.Add(new ChunkedNote(
                Guid.NewGuid(),
                noteId,
                title,
                string.Join(" ", currentChunkSentences),
                startIndex + chunks.Count,
                sourceType,
                sourceLabel));
        }

        return chunks;
    }

    private static List<string> SplitSentences(string content)
    {
        return System.Text.RegularExpressions.Regex.Split(content, @"(?<=[\.\!\?\n])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToList();
    }

    private static int GetWordCount(string text) =>
        text.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries).Length;

    private static string FormatTimestamp(double ratio)
    {
        // Estimate assuming ~150 words per minute for typical speech
        // This is a rough approximation; ratio is position within transcript
        var estimatedMinutes = ratio * 60; // assume max ~60 min recording
        var mins = (int)estimatedMinutes;
        var secs = (int)((estimatedMinutes - mins) * 60);
        return $"~{mins:D2}:{secs:D2}";
    }
}
