using Notisight.Api.Features.AI.Contracts;

namespace Notisight.Api.Features.AI.Services;

public interface ITextChunkingService
{
    IReadOnlyList<ChunkedNote> Chunk(Guid noteId, string title, string content);
    IReadOnlyList<ChunkedNote> ChunkSource(Guid noteId, string title, string content, string sourceType, string sourceLabel = "");
    IReadOnlyList<ChunkedNote> ChunkPages(Guid noteId, string title, IReadOnlyList<(int PageNumber, string Text)> pages);
    IReadOnlyList<ChunkedNote> ChunkAudio(Guid noteId, string title, string transcript);
}
