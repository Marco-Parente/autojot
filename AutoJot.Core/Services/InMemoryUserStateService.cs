using Core.Services.Bot;

namespace Core.Services;

public record UserState
{
    public string UserKey { get; set; } = null!;
    public IReadOnlyList<IChatMessage> Messages { get; set; } = [];
    public BotAction? CurrentAction { get; set; }
    public string? SelectedFile { get; set; }
    public string? FileContent { get; set; }
    public List<string> FileKeywords { get; set; } = [];

    public UserState AddMessage(IChatMessage message)
    {
        return this with { Messages = new List<IChatMessage>(Messages) { message } };
    }
}

public interface IChatMessage;

public class TextMessage : IChatMessage
{
    public string Message { get; set; } = null!;
    public TextOrigin TextOrigin { get; set; }

    public override string? ToString()
    {
        return Message;
    }
}

public enum TextOrigin
{
    System = 10,
    User = 20,
}

public class MenuMessage : IChatMessage
{
    public IReadOnlyList<MenuOption> FileOptions { get; set; } = [];
    public IReadOnlyList<MenuOption> ExtraOptions { get; set; } = [];

    public MenuOption? GetActionFromOption(string option)
    {
        if (!int.TryParse(option, out var index))
        {
            return null;
        }

        index -= 1;
        var allOptions = FileOptions.Union(ExtraOptions).ToList();
        return allOptions.ElementAtOrDefault(index);
    }

    public override string? ToString()
    {
        var currentIndex = 0;
        var message = "Multiple matches found:\n\n";

        foreach (var option in FileOptions)
        {
            currentIndex++;
            message += $"{currentIndex} - {option.Name}\n";
        }

        if (ExtraOptions.Count > 0)
        {
            message += "\n";
            foreach (var option in ExtraOptions)
            {
                currentIndex++;
                message += $"{currentIndex} - {option.Name}\n";
            }
        }

        message += "\n\nWhich option do you wanna choose? (select by index number)";

        return message;
    }
}

public class MenuOption
{
    public required string Name { get; set; }
    public BotAction Action { get; set; }
}

public interface IUserStateService
{
    public Task<UserState> GetUserState(string key);
    public Task SetUserState(string key, UserState content);
    public Task ClearUserState(string key);
}

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
