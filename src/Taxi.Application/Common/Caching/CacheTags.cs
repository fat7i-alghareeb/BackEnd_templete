namespace Taxi.Application.Common.Caching;

/// <summary>
/// Canonical cache tags used by <c>ICachedQuery.Tags</c> and by the
/// <c>HybridCache.RemoveByTagAsync</c> calls in the command handlers that
/// invalidate them. A query and its invalidating commands MUST reference the
/// same constant — never a string literal.
/// </summary>
public static class CacheTags
{
    /// <summary>Everything derived from the <c>Cars</c> table.</summary>
    public const string Cars = "car";

    /// <summary>Per-user identity projections returned by <c>GetUserByIdQuery</c>.</summary>
    public const string UserInfo = "user-info";
}
