using Notisight.Api.Features.AI.Contracts;

namespace Notisight.Api.Features.AI.Services;

public interface ISmartRetrievalService
{
    Task<List<SearchChunkResult>> RetrieveAsync(
        Guid userId,
        string query,
        QueryIntent intent,
        CancellationToken cancellationToken);
}
