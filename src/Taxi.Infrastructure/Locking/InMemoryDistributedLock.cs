using System.Collections.Concurrent;

using Taxi.Application.Common.Interfaces;

namespace Taxi.Infrastructure.Locking;

/// <summary>
/// Process-local <see cref="IDistributedLock"/>, used when Redis is not configured. Correct for a
/// single instance only.
/// </summary>
public sealed class InMemoryDistributedLock(TimeProvider timeProvider) : IDistributedLock
{
    private readonly ConcurrentDictionary<string, (Guid Owner, DateTimeOffset ExpiresAt)> locks = new();
    private readonly TimeProvider timeProvider = timeProvider;

    public Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var owner = Guid.NewGuid();
        var now = this.timeProvider.GetUtcNow();
        var entry = (owner, now + expiry);

        while (true)
        {
            if (this.locks.TryAdd(key, entry))
            {
                return Task.FromResult<IAsyncDisposable?>(new Releaser(this, key, owner));
            }

            // Take over an expired lock — but only if nobody changed it since we looked.
            if (this.locks.TryGetValue(key, out var current) && current.ExpiresAt <= now)
            {
                if (this.locks.TryUpdate(key, entry, current))
                {
                    return Task.FromResult<IAsyncDisposable?>(new Releaser(this, key, owner));
                }

                continue;
            }

            return Task.FromResult<IAsyncDisposable?>(null);
        }
    }

    private sealed class Releaser(InMemoryDistributedLock parent, string key, Guid owner) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            // Remove only our own entry; an expired lock may already belong to someone else.
            if (parent.locks.TryGetValue(key, out var current) && current.Owner == owner)
            {
                parent.locks.TryRemove(new KeyValuePair<string, (Guid, DateTimeOffset)>(key, current));
            }

            return ValueTask.CompletedTask;
        }
    }
}
