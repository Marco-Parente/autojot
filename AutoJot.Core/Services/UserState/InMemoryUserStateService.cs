using Microsoft.Extensions.Caching.Memory;

namespace Core.Services.UserState;

public class InMemoryUserStateService : IUserStateService
{
    /// <summary>
    /// A conversation abandoned mid-menu would otherwise keep the user pinned in
    /// <see cref="Bot.BotAction.WaitForInput"/> forever, and keep their state alive for the life of
    /// the process. Sliding, so an active conversation is never cut off mid-flow.
    /// </summary>
    public static readonly TimeSpan Expiry = TimeSpan.FromMinutes(30);

    private readonly IMemoryCache _cache;

    public InMemoryUserStateService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<UserState> GetUserState(string key)
    {
        var state =
            _cache.GetOrCreate(
                key,
                entry =>
                {
                    entry.SlidingExpiration = Expiry;
                    return new UserState { UserKey = key };
                }
            ) ?? new UserState { UserKey = key };

        return Task.FromResult(state);
    }

    public Task SetUserState(string key, UserState content)
    {
        _cache.Set(key, content, new MemoryCacheEntryOptions { SlidingExpiration = Expiry });
        return Task.CompletedTask;
    }

    public Task ClearUserState(string key)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
}
