using Core.Services.Bot;
using Core.Services.Message;
using Core.Services.Search;
using Core.Shared;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace AutoJot.Worker;

public class TelegramBotWorker : BackgroundService
{
    private readonly ILogger<TelegramBotWorker> _logger;
    private readonly IServiceProvider _sp;
    private readonly ITelegramBotClient _bot;
    private readonly INoteIndex _noteIndex;
    private readonly HashSet<long> _allowedUserIds;

    public TelegramBotWorker(
        ILogger<TelegramBotWorker> logger,
        IServiceProvider sp,
        IOptions<AutoJotOptions> options,
        ITelegramBotClient bot,
        INoteIndex noteIndex
    )
    {
        _logger = logger;
        _sp = sp;
        _bot = bot;
        _noteIndex = noteIndex;
        _allowedUserIds = [.. options.Value.AllowedUserIds];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Telegram Bot...");

        // Builds the index now rather than making the first message pay for it.
        _logger.LogInformation("Indexed {NoteCount} notes", _noteIndex.Count);

        if (_allowedUserIds.Count == 0)
        {
            _logger.LogWarning(
                "AutoJot:AllowedUserIds is empty — every message will be rejected. "
                    + "Add your Telegram user id (it is logged when you message the bot) to enable access."
            );
        }

        await _bot.DeleteWebhook(true, cancellationToken: stoppingToken);

        _bot.StartReceiving(
            updateHandler: HandleUpdate,
            errorHandler: HandleError,
            cancellationToken: stoppingToken
        );

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Telegram Bot stopping...");
        }
    }

    private async Task HandleUpdate(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken cancellationToken
    )
    {
        try
        {
            switch (update)
            {
                case { Type: UpdateType.Message, Message: { From: { } user, Text: { } text } }
                    when !string.IsNullOrEmpty(text):
                    if (!IsAuthorized(user.Id))
                    {
                        return;
                    }

                    await Dispatch(
                        (bot, userKey) => bot.ReceiveMessage(userKey, text, cancellationToken),
                        user.Id
                    );
                    break;

                case { Type: UpdateType.CallbackQuery, CallbackQuery: { } callback }:
                    if (!IsAuthorized(callback.From.Id))
                    {
                        return;
                    }

                    // Answer first: until the callback is answered the button keeps spinning in
                    // the user's client, however long the work behind it takes.
                    await botClient.AnswerCallbackQuery(
                        callback.Id,
                        cancellationToken: cancellationToken
                    );

                    if (!string.IsNullOrEmpty(callback.Data))
                    {
                        await Dispatch(
                            (bot, userKey) =>
                                bot.ReceiveSelection(userKey, callback.Data, cancellationToken),
                            callback.From.Id
                        );
                    }

                    break;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error receiving update from Telegram");
        }
    }

    /// <summary>
    /// The bot reads and overwrites the whole notes vault, so anyone who finds it would otherwise
    /// have full access. Unknown senders are dropped without a reply.
    /// </summary>
    private bool IsAuthorized(long userId)
    {
        if (_allowedUserIds.Contains(userId))
        {
            return true;
        }

        _logger.LogWarning(
            "Rejected update from unauthorized Telegram user id {UserId}. "
                + "Add it to AutoJot:AllowedUserIds if this is you.",
            userId
        );

        return false;
    }

    private async Task Dispatch(Func<IBotService, string, Task> handle, long userId)
    {
        using var scope = _sp.CreateScope();
        var botService = scope.ServiceProvider.GetRequiredService<IBotService>();

        await handle(botService, TelegramMessageService.ToUserKey(userId));
    }

    private Task HandleError(
        ITelegramBotClient _,
        Exception ex,
        CancellationToken cancellationToken
    )
    {
        _logger.LogError(ex, "Error receiving message from Telegram");
        return Task.CompletedTask;
    }
}
