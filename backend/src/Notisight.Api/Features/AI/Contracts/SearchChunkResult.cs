namespace Notisight.Api.Features.AI.Contracts;

public sealed record SearchChunkResult(
    ChunkedNote Chunk,
    double Score);
