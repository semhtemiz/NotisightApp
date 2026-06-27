using Notisight.Api.Features.AI.Contracts;

namespace Notisight.Api.Features.AI.Contracts;

public interface ILlmChatService
{
    Task<string?> GenerateGroundedAnswerAsync(
        string question,
        IReadOnlyList<ChatHistoryMessage>? history,
        IReadOnlyList<string> contextChunks,
        CancellationToken cancellationToken,
        SessionContext? sessionContext = null,
        PersonalityTone tone = PersonalityTone.Casual);

    Task<string?> GenerateTitleAsync(
        string question,
        CancellationToken cancellationToken);

    /// <summary>
    /// Standard mod için sistem istemi olmadan serbest sohbet.
    /// RAG bağlamı enjekte edilmez. Oturum geçmişi aktarılır.
    /// </summary>
    Task<string?> FreeChatAsync(
        string userMessage,
        IReadOnlyList<ChatHistoryMessage>? history,
        SessionContext? sessionContext,
        CancellationToken cancellationToken,
        PersonalityTone tone = PersonalityTone.Casual);

    /// <summary>
    /// Kullanıcı sorgusunun niyetini ve anahtar kelimelerini LLM ile çıkarır.
    /// </summary>
    Task<QueryIntent> ExtractIntentAsync(string query, SessionContext? sessionContext, CancellationToken cancellationToken);

    /// <summary>
    /// Standard mod için sistem istemi olmadan serbest sohbet.
    /// RAG bağlamı enjekte edilmez. Oturum geçmişi aktarılır.
    /// </summary>
    IAsyncEnumerable<string> FreeChatStreamAsync(
        string userMessage,
        IReadOnlyList<ChatHistoryMessage>? history,
        SessionContext? sessionContext,
        CancellationToken cancellationToken,
        PersonalityTone tone = PersonalityTone.Casual);

    IAsyncEnumerable<string> GenerateGroundedAnswerStreamAsync(
        string query,
        IReadOnlyList<ChatHistoryMessage>? history,
        List<string> contextChunks,
        CancellationToken cancellationToken,
        SessionContext? sessionContext = null,
        PersonalityTone tone = PersonalityTone.Casual);
}
