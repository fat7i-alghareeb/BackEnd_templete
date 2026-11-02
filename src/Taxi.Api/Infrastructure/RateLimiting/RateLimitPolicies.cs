namespace Taxi.Api.Infrastructure.RateLimiting;

/// <summary>
/// Named rate-limiter policies. Reference these constants from <c>[EnableRateLimiting]</c>; never
/// write the policy name inline.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// Applied to every controller through <c>ApiController</c>. Sliding window, partitioned by
    /// user id when authenticated and by client IP otherwise.
    /// </summary>
    public const string Default = "default";

    /// <summary>
    /// Applied to the anonymous credential endpoints (token generation, refresh, registration,
    /// password reset). Fixed window, partitioned by client IP — the brute-force guard.
    /// </summary>
    public const string Auth = "auth";
}
