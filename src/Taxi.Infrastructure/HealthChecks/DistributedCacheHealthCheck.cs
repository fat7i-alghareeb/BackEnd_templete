using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Taxi.Infrastructure.HealthChecks;

/// <summary>
/// Round-trips one read against the registered <see cref="IDistributedCache"/> (Redis when
/// <c>ConnectionStrings:Redis</c> is set). A miss is healthy — only a connection or protocol
/// failure is not. Registered only when Redis is configured.
/// </summary>
public sealed class DistributedCacheHealthCheck(IDistributedCache cache) : IHealthCheck
{
    private const string ProbeKey = "health:probe";

    private readonly IDistributedCache cache = cache;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await this.cache.GetAsync(ProbeKey, cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Distributed cache is unreachable.", ex);
        }
    }
}
