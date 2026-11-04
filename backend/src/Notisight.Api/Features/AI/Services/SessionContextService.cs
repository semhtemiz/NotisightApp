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

    public Task<SessionContext> GetOrCreateAsync(Guid userId, string? sessionId)
    {
        var id = string.IsNullOrWhiteSpace(sessionId)
            ? Guid.NewGuid().ToString()
            : sessionId;

        var context = _cache.GetOrCreate(CacheKey(userId, id), entry =>
        {
            entry.SlidingExpiration = SessionTtl;
            return new SessionContext { UserId = userId, SessionId = id };
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

        _cache.Set(CacheKey(context), context, SessionTtl);

        return Task.CompletedTask;
    }

    public Task AddAccessedSourceAsync(SessionContext context, string sourceId)
    {
        if (_cache.TryGetValue(CacheKey(context), out SessionContext? cached) && cached != null)
        {
            if (!cached.RecentlyAccessedSourceIds.Contains(sourceId))
            {
                cached.RecentlyAccessedSourceIds.Add(sourceId);
                if (cached.RecentlyAccessedSourceIds.Count > 20)
                    cached.RecentlyAccessedSourceIds.RemoveAt(0);
            }
            _cache.Set(CacheKey(context), cached, SessionTtl);
        }
        return Task.CompletedTask;
    }

    public Task RecordModeSwitchAsync(SessionContext context, ChatMode fromMode, ChatMode toMode)
    {
        if (_cache.TryGetValue(CacheKey(context), out SessionContext? cached) && cached != null)
        {
            cached.ActiveMode = toMode;
            cached.ModeSwitchHistory.Add(new ModeSwitchEvent
            {
                FromMode = fromMode,
                ToMode = toMode,
                SwitchedAt = DateTime.UtcNow,
                AtTurnIndex = cached.RecentTurns.Count
            });
            _cache.Set(CacheKey(context), cached, SessionTtl);
        }
        return Task.CompletedTask;
    }

    public Task UpdateToneAsync(SessionContext context, PersonalityTone tone)
    {
        if (_cache.TryGetValue(CacheKey(context), out SessionContext? cached) && cached != null)
        {
            cached.ActiveTone = tone;
            _cache.Set(CacheKey(context), cached, SessionTtl);
        }
        return Task.CompletedTask;
    }

    // The owner is part of the key: two users who send the same session id must never share
    // one cached conversation context.
    private static string CacheKey(Guid userId, string sessionId) => $"user:{userId}:session:{sessionId}";

    private static string CacheKey(SessionContext context) => CacheKey(context.UserId, context.SessionId);
}
