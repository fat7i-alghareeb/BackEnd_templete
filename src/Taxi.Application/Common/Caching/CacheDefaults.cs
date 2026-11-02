namespace Taxi.Application.Common.Caching;

/// <summary>
/// Cache lifetimes shared by <c>CachingBehavior</c> and the HybridCache registration.
/// </summary>
public static class CacheDefaults
{
    /// <summary>
    /// How long an entry may live in a node's in-process L1. HybridCache has no backplane, so after
    /// a <c>RemoveByTagAsync</c> on one node, another node can serve its L1 copy for at most this long
    /// before it goes back to the shared L2 (Redis) and sees the invalidation. Keep it short.
    /// </summary>
    public static readonly TimeSpan LocalExpiration = TimeSpan.FromSeconds(30);

    /// <summary>Gets the L1 lifetime for an entry whose overall lifetime is <paramref name="expiration"/>.</summary>
    /// <returns>The shorter of <paramref name="expiration"/> and <see cref="LocalExpiration"/>.</returns>
    public static TimeSpan LocalExpirationFor(TimeSpan expiration) =>
        expiration < LocalExpiration ? expiration : LocalExpiration;
}
