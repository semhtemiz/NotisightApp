using Notisight.Api.Domain.Entities;

namespace Notisight.Api.Features.AI.Services;

public interface IChatHistoryService
{
    Task<ChatSession> GetOrCreateSessionAsync(Guid userId, string? sessionId, CancellationToken cancellationToken);
    Task SaveUserMessageAsync(Guid sessionId, string content, string mode, CancellationToken cancellationToken);
    Task SaveAiMessageAsync(Guid sessionId, string content, string mode, string? metadataJson, CancellationToken cancellationToken);
    Task UpdateSessionTitleAsync(Guid sessionId, string title, CancellationToken cancellationToken);
    Task<List<ChatSession>> GetUserSessionsAsync(Guid userId, CancellationToken cancellationToken);
    Task<List<ChatMessage>> GetSessionMessagesAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken);
    Task DeleteSessionAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken);
}
