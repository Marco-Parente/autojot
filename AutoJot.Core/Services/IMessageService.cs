using Microsoft.Extensions.Configuration;
using Telegram.Bot;

namespace Core.Services;

public interface IMessageService
{
    public Task SendMessage(string userKey, string message);
}

public class TelegramMessageService : IMessageService
{
    private readonly IConfiguration _configuration;

    public TelegramMessageService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public static string ToUserKey(long userId) => $"telegram-{userId}";

    public static long GetUserIdFromUserKey(string userId) =>
        long.Parse(userId.Replace("telegram-", string.Empty));

    public async Task SendMessage(string userKey, string message)
    {
        var client = new TelegramBotClient(
            _configuration.GetValue<string?>("Telegram:BotToken")
                ?? throw new NullReferenceException("Telegram bot token not set")
        );

        await client.SendMessage(GetUserIdFromUserKey(userKey), message);
    }
}
