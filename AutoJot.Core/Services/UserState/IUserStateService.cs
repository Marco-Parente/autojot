using Core.Services.Bot;

namespace Core.Services.UserState;

public interface IUserStateService
{
    public Task<UserState> GetUserState(string key);
    public Task SetUserState(string key, UserState content);
    public Task ClearUserState(string key);
}

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

public record TextMessage : IChatMessage
{
    public string Message { get; set; } = null!;
    public TextOrigin TextOrigin { get; set; }

    public override string ToString()
    {
        return Message;
    }
}

public enum TextOrigin
{
    System = 10,
    User = 20,
}

public record MenuMessage : IChatMessage
{
    public string HeaderText { get; set; } = "Multiple file matches found:";
    public string FooterText { get; set; } =
        "Which option do you wanna choose? (select by index number)";

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

    public override string ToString()
    {
        var currentIndex = 0;
        var message = $"{HeaderText}\n\n";

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

        message += $"\n\n{FooterText}";

        return message;
    }
}

public record MenuOption
{
    public required string Name { get; set; }
    public BotAction Action { get; set; }
}
