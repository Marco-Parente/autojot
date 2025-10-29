namespace Core.Services.Message;

public interface IMessageService
{
    public Task SendMessage(string userKey, string message);
}