namespace Core.Services.UserState;

public class InMemoryUserStateService : IUserStateService
{
    private static readonly Dictionary<string, UserState> Store = new();
    private static readonly Lock Lock = new();

    private UserState CreateNewUserState(string key)
    {
        var state = new UserState { UserKey = key };

        lock (Lock)
        {
            Store[key] = state;
        }

        return state;
    }

    public Task<UserState> GetUserState(string key)
    {
        lock (Lock)
        {
            var state = Store.GetValueOrDefault(key);
            if (state != null)
            {
                return Task.FromResult(state);
            }
        }

        return Task.FromResult(CreateNewUserState(key));
    }

    public Task SetUserState(string key, UserState content)
    {
        lock (Lock)
        {
            Store[key] = content;
        }

        return Task.CompletedTask;
    }

    public Task ClearUserState(string key)
    {
        lock (Lock)
        {
            Store.Remove(key);
        }
        return Task.CompletedTask;
    }
}
