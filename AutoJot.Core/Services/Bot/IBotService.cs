namespace Core.Services.Bot;

public interface IBotService
{
    Task ReceiveMessage(
        string userKey,
        string message,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Handles a menu button tap. Kept separate from <see cref="ReceiveMessage"/> so callback data
    /// can never be mistaken for something the user typed.
    /// </summary>
    Task ReceiveSelection(
        string userKey,
        string callbackData,
        CancellationToken cancellationToken = default
    );
}
