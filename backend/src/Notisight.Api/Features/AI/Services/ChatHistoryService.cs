using Microsoft.EntityFrameworkCore;
using Notisight.Api.Domain.Entities;
using Notisight.Api.Infrastructure.Persistence;

namespace Notisight.Api.Features.AI.Services;

public sealed class ChatHistoryService(ApplicationDbContext dbContext) : IChatHistoryService
{
    public async Task<ChatSession> GetOrCreateSessionAsync(Guid userId, string? sessionId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(sessionId) && Guid.TryParse(sessionId, out var parsedId))
        {
            var existing = await dbContext.ChatSessions
                .FirstOrDefaultAsync(s => s.Id == parsedId && s.UserId == userId, cancellationToken);

            if (existing != null)
                return existing;
        }

        var sessionCount = await dbContext.ChatSessions.CountAsync(s => s.UserId == userId, cancellationToken);
        if (sessionCount >= 30)
        {
            var oldestSessions = await dbContext.ChatSessions
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.CreatedAtUtc)
                .Take(sessionCount - 29)
                .ToListAsync(cancellationToken);
            
            dbContext.ChatSessions.RemoveRange(oldestSessions);
        }

        var session = new ChatSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Yeni Sohbet"
        };

        dbContext.ChatSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task SaveUserMessageAsync(Guid sessionId, string content, string mode, CancellationToken cancellationToken)
    {
        dbContext.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatSessionId = sessionId,
            Role = "user",
            Content = content,
            Mode = mode
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAiMessageAsync(Guid sessionId, string content, string mode, string? metadataJson, CancellationToken cancellationToken)
    {
        dbContext.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            ChatSessionId = sessionId,
            Role = "ai",
            Content = content,
            Mode = mode,
            MetadataJson = metadataJson
        });

        // Session'ın UpdatedAtUtc'sini de güncelle
        var session = await dbContext.ChatSessions.FindAsync([sessionId], cancellationToken);
        if (session != null)
        {
            session.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateSessionTitleAsync(Guid sessionId, string title, CancellationToken cancellationToken)
    {
        var session = await dbContext.ChatSessions.FindAsync([sessionId], cancellationToken);
        if (session != null)
        {
            session.Title = title;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<List<ChatSession>> GetUserSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.ChatSessions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.UpdatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ChatMessage>> GetSessionMessagesAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken)
    {
        // Güvenlik: Session'ın kullanıcıya ait olduğunu doğrula
        var session = await dbContext.ChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);

        if (session == null)
            return [];

        return await dbContext.ChatMessages
            .Where(m => m.ChatSessionId == sessionId)
            .OrderBy(m => m.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteSessionAsync(Guid sessionId, Guid userId, CancellationToken cancellationToken)
    {
        var session = await dbContext.ChatSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);

        if (session != null)
        {
            dbContext.ChatSessions.Remove(session); // Cascade delete ile mesajlar da silinir
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
