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

    public MenuMessage? CurrentMenu => Messages.OfType<MenuMessage>().LastOrDefault();
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
    /// <summary>
    /// Identifies this menu in the callback data of its buttons, so a tap on a menu that has since
    /// been superseded can be recognised and rejected instead of acted on.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];

    public string HeaderText { get; set; } = "Multiple file matches found:";
    public string FooterText { get; set; } = "Pick an option below, or send /cancel to start over.";

    public IReadOnlyList<MenuOption> FileOptions { get; set; } = [];
    public IReadOnlyList<MenuOption> ExtraOptions { get; set; } = [];

    public IReadOnlyList<MenuOption> AllOptions => [.. FileOptions, .. ExtraOptions];

    /// <summary>
    /// Telegram caps callback data at 64 bytes, so buttons carry a menu id and an index rather
    /// than a note path.
    /// </summary>
    public string CallbackDataFor(int index) => $"{Id}:{index}";

    public MenuSelection? Resolve(string callbackData)
    {
        var separator = callbackData.IndexOf(':');

        if (separator <= 0 || !callbackData.AsSpan(..separator).SequenceEqual(Id))
        {
            return null;
        }

        if (!int.TryParse(callbackData.AsSpan((separator + 1)..), out var index))
        {
            return null;
        }

        var options = AllOptions;

        return index < 0 || index >= options.Count
            ? null
            : new MenuSelection(options[index], index < FileOptions.Count);
    }

    public override string ToString()
    {
        return $"{HeaderText}\n\n{FooterText}";
    }
}

/// <param name="IsFileOption">
/// True when the option names a note, as opposed to an action like "Create new note".
/// </param>
public record MenuSelection(MenuOption Option, bool IsFileOption);

public record MenuOption
{
    public required string Name { get; set; }
    public BotAction Action { get; set; }
}
