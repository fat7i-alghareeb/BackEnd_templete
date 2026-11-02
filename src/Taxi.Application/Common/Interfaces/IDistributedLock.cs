namespace Taxi.Application.Common.Interfaces;

/// <summary>
/// A short-lived mutual-exclusion lock that holds across API instances when Redis is configured
/// (and within the process otherwise). Acquisition is atomic; the lock expires on its own after
/// <c>expiry</c> so a crashed holder cannot block others forever.
/// </summary>
public interface IDistributedLock
{
    /// <summary>Tries once, without waiting.</summary>
    /// <returns>A handle that releases the lock when disposed, or <see langword="null"/> if another holder has it.</returns>
    Task<IAsyncDisposable?> TryAcquireAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
}
