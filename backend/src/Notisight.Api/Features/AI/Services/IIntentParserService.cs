using Notisight.Api.Features.AI.Contracts;

namespace Notisight.Api.Features.AI.Services;

public interface IIntentParserService
{
    Task<QueryIntent> ParseAsync(string query, SessionContext? sessionContext);
}
