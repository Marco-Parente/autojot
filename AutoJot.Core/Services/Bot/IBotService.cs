namespace Core.Services.Bot;

public interface IBotService
{
    Task ReceiveMessage(string userKey, string message);
}

// should use the message service and userstate service and ai service to handle bot logic and decide what to do next
