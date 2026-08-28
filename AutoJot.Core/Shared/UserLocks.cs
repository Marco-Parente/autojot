using System.Collections.Concurrent;

namespace Core.Shared;

/// <summary>
/// Serializes message handling per user. Telegram delivers updates concurrently, so without this
/// two quick messages from the same user interleave their read-modify-write of the conversation
/// state and one of them is lost.
/// </summary>
public sealed class UserLocks
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> AcquireAsync(
        string userKey,
        CancellationToken cancellationToken = default
    )
    {
        var semaphore = _locks.GetOrAdd(userKey, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }
}
