using Notisight.Api.Features.AI.Contracts;

namespace Notisight.Api.Features.AI.Services;

public enum ConfidenceLevel { High, Medium, Low }

public interface IConfidenceEngineService
{
    ConfidenceLevel Evaluate(List<SearchChunkResult> chunks, QueryIntent intent);
}
