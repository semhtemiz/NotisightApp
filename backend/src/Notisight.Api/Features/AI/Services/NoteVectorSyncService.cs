using Notisight.Api.Domain.Entities;
using Notisight.Api.Features.AI.Contracts;
using Notisight.Api.Features.Ingestion.Contracts;
using Notisight.Api.Features.Ingestion.Services;
using Notisight.Api.Infrastructure.Persistence;
using Notisight.Api.Options;
using Microsoft.Extensions.Options;

namespace Notisight.Api.Features.AI.Services;

public sealed class NoteVectorSyncService(
    ITextChunkingService textChunkingService,
    IEmbeddingService embeddingService,
    IQdrantVectorService qdrantVectorService,
    IPdfIngestionService pdfIngestionService,
    IFileStorageService fileStorageService,
    ApplicationDbContext dbContext,
    IOptions<QdrantOptions> qdrantOptions,
    ILogger<NoteVectorSyncService> logger) : INoteVectorSyncService
{
    private readonly QdrantOptions _qdrantOptions = qdrantOptions.Value;

    public async Task UpsertNoteAsync(Note note, CancellationToken cancellationToken)
    {
        try
        {
            note.VectorSyncStatus = VectorSyncStatus.Indexing;
            note.VectorSyncError = null;
            note.VectorSyncedAtUtc = null;
            await dbContext.SaveChangesAsync(cancellationToken);

            await qdrantVectorService.DeleteByNoteIdAsync(note.Id, cancellationToken);

            var chunks = await CreateChunksAsync(note, cancellationToken);
            chunks = chunks
                .Select(chunk => chunk with { UserId = note.UserId })
                .ToList();

            if (chunks.Count > 0)
            {
                var vectors = new List<IReadOnlyList<float>>(chunks.Count);
                foreach (var chunk in chunks)
                {
                    var vector = await embeddingService.EmbedDocumentAsync(
                        $"{chunk.Title}\n{chunk.Content}",
                        cancellationToken);

                    if (vector.Count != _qdrantOptions.VectorSize)
                    {
                        throw new InvalidOperationException(
                            $"Embedding vector size mismatch. Expected {_qdrantOptions.VectorSize}, received {vector.Count}.");
                    }

                    vectors.Add(vector);
                }

                await qdrantVectorService.UpsertChunksAsync(chunks, vectors, cancellationToken);
            }

            note.VectorSyncStatus = VectorSyncStatus.Synced;
            note.VectorSyncError = null;
            note.VectorSyncedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Vector sync failed for note {NoteId}", note.Id);

            note.VectorSyncStatus = VectorSyncStatus.Failed;
            note.VectorSyncError = Trim(exception.Message, 1000);
            note.VectorSyncedAtUtc = null;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<List<ChunkedNote>> CreateChunksAsync(Note note, CancellationToken cancellationToken)
    {
        if (note.FileType == "pdf" && !string.IsNullOrWhiteSpace(note.FileUrl))
        {
            try
            {
                using var pdfStream = await fileStorageService.DownloadFileAsync(note.FileUrl, cancellationToken);
                var pages = await pdfIngestionService.ExtractPageTextsAsync(pdfStream, cancellationToken);
                if (pages.Count > 0)
                {
                    return textChunkingService.ChunkPages(note.Id, note.Title, pages).ToList();
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "PDF page extraction failed for note {NoteId}, falling back to plain text chunking", note.Id);
            }
        }

        if (note.FileType == "audio")
        {
            return textChunkingService.ChunkAudio(note.Id, note.Title, note.Content).ToList();
        }

        var cleanContent = CleanHtml(note.Content);
        return textChunkingService.Chunk(note.Id, note.Title, cleanContent).ToList();
    }

    private static string CleanHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        // Blok elemanların sonunu yeni satırla değiştirerek kelimelerin bitişmesini önle
        var text = System.Text.RegularExpressions.Regex.Replace(html, @"<(br|/p|/h[1-6]|/div|/li)>", "\n", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        
        // Geri kalan tüm HTML etiketlerini boşlukla değiştir
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", " ");
        
        // HTML entity'lerini (örn: &nbsp;, &amp;) normal karaktere çevir
        text = System.Net.WebUtility.HtmlDecode(text);
        
        // Fazla boşlukları ve satır atlamalarını temizle
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[ \t]+", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\n\s*\n+", "\n\n");
        
        return text.Trim();
    }

    public Task DeleteNoteAsync(Guid noteId, CancellationToken cancellationToken) =>
        qdrantVectorService.DeleteByNoteIdAsync(noteId, cancellationToken);

    private static string Trim(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
