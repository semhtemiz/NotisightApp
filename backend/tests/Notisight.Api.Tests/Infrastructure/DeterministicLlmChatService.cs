using System.Runtime.CompilerServices;
using Notisight.Api.Features.AI.Contracts;

namespace Notisight.Api.Tests.Infrastructure;

public sealed class DeterministicLlmChatService : ILlmChatService
{
    public Task<string?> GenerateGroundedAnswerAsync(
        string question,
        IReadOnlyList<ChatHistoryMessage>? history,
        IReadOnlyList<string> contextChunks,
        CancellationToken cancellationToken,
        SessionContext? sessionContext = null,
        PersonalityTone tone = PersonalityTone.Casual) =>
        Task.FromResult<string?>("Deterministic grounded answer [ID: c1]");

    public Task<string?> GenerateTitleAsync(string question, CancellationToken cancellationToken) =>
        Task.FromResult<string?>("Deterministic title");

    public Task<string?> FreeChatAsync(
        string userMessage,
        IReadOnlyList<ChatHistoryMessage>? history,
        SessionContext? sessionContext,
        CancellationToken cancellationToken,
        PersonalityTone tone = PersonalityTone.Casual) =>
        Task.FromResult<string?>("Deterministic free chat response");

    public Task<QueryIntent> ExtractIntentAsync(
        string query,
        SessionContext? sessionContext,
        CancellationToken cancellationToken) =>
        Task.FromResult(new QueryIntent
        {
            OptimizedSearchQuery = query,
            KeyEntities = query
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(token => token.Length > 3)
                .Take(5)
                .ToList()
        });

    public async IAsyncEnumerable<string> FreeChatStreamAsync(
        string userMessage,
        IReadOnlyList<ChatHistoryMessage>? history,
        SessionContext? sessionContext,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        PersonalityTone tone = PersonalityTone.Casual)
    {
        await Task.Yield();
        yield return "Deterministic free chat response";
    }

    public async IAsyncEnumerable<string> GenerateGroundedAnswerStreamAsync(
        string query,
        IReadOnlyList<ChatHistoryMessage>? history,
        List<string> contextChunks,
        [EnumeratorCancellation] CancellationToken cancellationToken,
        SessionContext? sessionContext = null,
        PersonalityTone tone = PersonalityTone.Casual)
    {
        await Task.Yield();
        yield return "Deterministic grounded answer [ID: c1]";
    }
}
