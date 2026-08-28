using Core.Services.UserState;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace Core.Services.Message;

public class TelegramMessageService : IMessageService
{
    /// <summary>
    /// Telegram rejects messages longer than 4096 characters. Long notes are split rather than
    /// allowed to throw, which would otherwise surface to the user as silence.
    /// </summary>
    private const int MaxMessageLength = 4096;

    /// <summary>Button labels are truncated so long note paths stay tappable on a phone.</summary>
    private const int MaxButtonLabelLength = 60;

    private readonly ITelegramBotClient _client;

    public TelegramMessageService(ITelegramBotClient client)
    {
        _client = client;
    }

    public static string ToUserKey(long userId) => $"telegram-{userId}";

    public static long GetUserIdFromUserKey(string userId) =>
        long.Parse(userId.Replace("telegram-", string.Empty));

    public async Task SendMessage(
        string userKey,
        string message,
        CancellationToken cancellationToken = default
    )
    {
        var chatId = GetUserIdFromUserKey(userKey);

        foreach (var chunk in Chunk(message))
        {
            await _client.SendMessage(chatId, chunk, cancellationToken: cancellationToken);
        }
    }

    public async Task SendMenu(
        string userKey,
        MenuMessage menu,
        CancellationToken cancellationToken = default
    )
    {
        var chatId = GetUserIdFromUserKey(userKey);
        var keyboard = BuildKeyboard(menu);

        // A menu header can be a whole note (the update preview), so it may still need splitting.
        // The keyboard goes on the last chunk, next to the prompt it belongs to.
        var chunks = Chunk(menu.ToString()).ToList();

        for (var i = 0; i < chunks.Count; i++)
        {
            var isLast = i == chunks.Count - 1;

            await _client.SendMessage(
                chatId,
                chunks[i],
                replyMarkup: isLast ? keyboard : null,
                cancellationToken: cancellationToken
            );
        }
    }

    public static InlineKeyboardMarkup BuildKeyboard(MenuMessage menu)
    {
        // One button per row: note paths are far too long to sit side by side.
        return new InlineKeyboardMarkup(
            menu.AllOptions.Select(
                (option, index) =>
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData(
                            TruncateLabel(option.Name),
                            menu.CallbackDataFor(index)
                        ),
                    }
            )
        );
    }

    private static string TruncateLabel(string label) =>
        label.Length <= MaxButtonLabelLength
            ? label
            : string.Concat("…", label.AsSpan(label.Length - MaxButtonLabelLength + 1));

    /// <summary>
    /// Splits text into Telegram-sized chunks, preferring line boundaries so Markdown notes stay
    /// readable across the split.
    /// </summary>
    public static IEnumerable<string> Chunk(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            yield break;
        }

        var remaining = message.AsMemory();

        while (remaining.Length > MaxMessageLength)
        {
            var window = remaining[..MaxMessageLength];
            var breakAt = window.Span.LastIndexOf('\n');

            // No line break to split on: fall back to a hard cut at the limit.
            if (breakAt <= 0)
            {
                breakAt = MaxMessageLength;
            }

            yield return remaining[..breakAt].ToString();
            remaining = remaining[breakAt..].TrimStart('\n');
        }

        if (remaining.Length > 0)
        {
            yield return remaining.ToString();
        }
    }
}
