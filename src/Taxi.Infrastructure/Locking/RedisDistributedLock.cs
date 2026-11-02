using StackExchange.Redis;

using Taxi.Application.Common.Interfaces;

namespace Taxi.Infrastructure.Locking;

/// <summary>
/// <see cref="IDistributedLock"/> on Redis: <c>LockTakeAsync</c> is an atomic <c>SET key token NX PX</c>
/// and <c>LockReleaseAsync</c> deletes only when the stored token is still ours, so an expired lock
/// that someone else re-acquired is never released by mistake.
/// </summary>
public sealed class RedisDistributedLock(IConnectionMultiplexer redis) : IDistributedLock
{
    private const string Prefix = "taxi:lock:";

    private readonly IConnectionMultiplexer redis = redis;

    public async Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var database = this.redis.GetDatabase();
        var redisKey = new RedisKey(Prefix + key);
        var token = Guid.NewGuid().ToString("N");

        return await database.LockTakeAsync(redisKey, token, expiry)
            ? new Releaser(database, redisKey, token)
            : null;
    }

    private sealed class Releaser(IDatabase database, RedisKey key, string token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() => await database.LockReleaseAsync(key, token);
    }
}
