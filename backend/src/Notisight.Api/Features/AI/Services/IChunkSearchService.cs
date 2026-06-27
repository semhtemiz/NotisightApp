using Notisight.Api.Features.AI.Contracts;

namespace Notisight.Api.Features.AI.Services;

public interface IChunkSearchService
{
    Task<IReadOnlyList<SearchChunkResult>> SearchAsync(
        Guid userId,
        string question,
        QueryIntent intent,
        CancellationToken cancellationToken);
}
