using Microsoft.Extensions.Caching.Memory;
using Notisight.Api.Features.AI.Contracts;

namespace Notisight.Api.Features.AI.Services;

public class SessionContextService : ISessionContextService
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(2);
    private const int MaxTurns = 10;

    public SessionContextService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<SessionContext> GetOrCreateAsync(string? sessionId)
    {
        var id = string.IsNullOrWhiteSpace(sessionId)
            ? Guid.NewGuid().ToString()
            : sessionId;

        var context = _cache.GetOrCreate(CacheKey(id), entry =>
        {
            entry.SlidingExpiration = SessionTtl;
            return new SessionContext { SessionId = id };
        })!;

        return Task.FromResult(context);
    }

    public Task UpdateAsync(SessionContext context, string userMessage, string assistantMessage, ChatMode mode = ChatMode.Standard)
    {
        context.RecentTurns.Add(new ConversationTurn
        {
            UserMessage = userMessage,
            AssistantMessage = assistantMessage,
            Timestamp = DateTime.UtcNow,
            Mode = mode
        });

        if (context.RecentTurns.Count > MaxTurns)
            context.RecentTurns.RemoveAt(0);

        _cache.Set(CacheKey(context.SessionId), context, SessionTtl);

        return Task.CompletedTask;
    }

    public Task AddAccessedSourceAsync(string sessionId, string sourceId)
    {
        if (_cache.TryGetValue(CacheKey(sessionId), out SessionContext? context) && context != null)
        {
            if (!context.RecentlyAccessedSourceIds.Contains(sourceId))
            {
                context.RecentlyAccessedSourceIds.Add(sourceId);
                if (context.RecentlyAccessedSourceIds.Count > 20)
                    context.RecentlyAccessedSourceIds.RemoveAt(0);
            }
            _cache.Set(CacheKey(sessionId), context, SessionTtl);
        }
        return Task.CompletedTask;
    }

    public Task RecordModeSwitchAsync(string sessionId, ChatMode fromMode, ChatMode toMode)
    {
        if (_cache.TryGetValue(CacheKey(sessionId), out SessionContext? context) && context != null)
        {
            context.ActiveMode = toMode;
            context.ModeSwitchHistory.Add(new ModeSwitchEvent
            {
                FromMode = fromMode,
                ToMode = toMode,
                SwitchedAt = DateTime.UtcNow,
                AtTurnIndex = context.RecentTurns.Count
            });
            _cache.Set(CacheKey(sessionId), context, SessionTtl);
        }
        return Task.CompletedTask;
    }

    public Task UpdateToneAsync(string sessionId, PersonalityTone tone)
    {
        if (_cache.TryGetValue(CacheKey(sessionId), out SessionContext? context) && context != null)
        {
            context.ActiveTone = tone;
            _cache.Set(CacheKey(sessionId), context, SessionTtl);
        }
        return Task.CompletedTask;
    }

    private static string CacheKey(string sessionId) => $"session:{sessionId}";
}
