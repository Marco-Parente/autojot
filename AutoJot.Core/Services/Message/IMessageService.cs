using Core.Services.UserState;

namespace Core.Services.Message;

public interface IMessageService
{
    public Task SendMessage(
        string userKey,
        string message,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Sends a menu whose options the user picks by tapping, rather than by typing an index.
    /// </summary>
    public Task SendMenu(
        string userKey,
        MenuMessage menu,
        CancellationToken cancellationToken = default
    );
}
