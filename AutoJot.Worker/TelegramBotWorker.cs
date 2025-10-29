using Core.Services.Bot;
using Core.Services.Message;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace AutoJot.Worker;

public class TelegramBotWorker : BackgroundService
{
    private readonly ILogger<TelegramBotWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _sp;

    public TelegramBotWorker(
        ILogger<TelegramBotWorker> logger,
        IServiceProvider sp,
        IConfiguration configuration
    )
    {
        _logger = logger;
        _sp = sp;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting Telegram Bot...");

        var bot = new TelegramBotClient(
            _configuration.GetValue<string?>("Telegram:BotToken")
                ?? throw new NullReferenceException("Telegram bot token is not set")
        );

        await bot.DeleteWebhook(true, cancellationToken: stoppingToken);

        bot.StartReceiving(
            updateHandler: HandleUpdate,
            errorHandler: HandleError,
            cancellationToken: stoppingToken
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

            await Task.Delay(1000 * 60, stoppingToken);
        }
    }

    private async Task HandleUpdate(
        ITelegramBotClient botClient,
        Update update,
        CancellationToken cancellationToken
    )
    {
        using var scope = _sp.CreateScope();
        var botService = scope.ServiceProvider.GetRequiredService<IBotService>();

        try
        {
            if (update.Type != UpdateType.Message)
            {
                return;
            }

            var user = update.Message!.From;
            var text = update.Message.Text;

            if (user is null || string.IsNullOrEmpty(text))
            {
                return;
            }

            await botService.ReceiveMessage(TelegramMessageService.ToUserKey(user.Id), text);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error receiving message from Telegram");
        }
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
