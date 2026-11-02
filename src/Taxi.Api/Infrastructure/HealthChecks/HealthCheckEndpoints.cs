using System.Text.Json;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Taxi.Infrastructure.HealthChecks;

namespace Taxi.Api.Infrastructure.HealthChecks;

public static class HealthCheckEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Maps the two probes. Both are anonymous (they sit outside the fallback policy) and outside
    /// rate limiting, and both answer 200 when healthy and 503 otherwise.
    /// <list type="bullet">
    /// <item><c>/health/live</c> — runs no checks: the process is up and serving requests. Use it for
    /// a liveness probe or a container <c>HEALTHCHECK</c>; it must not fail because a dependency is down.</item>
    /// <item><c>/health/ready</c> — runs every check tagged <see cref="HealthCheckTags.Ready"/>
    /// (PostgreSQL, Redis). Use it to gate traffic.</item>
    /// </list>
    /// </summary>
    /// <returns>The route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapAppHealthChecks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponseAsync,
        }).AllowAnonymous();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(HealthCheckTags.Ready),
            ResponseWriter = WriteResponseAsync,
        }).AllowAnonymous();

        return endpoints;
    }

    /// <summary>
    /// <c>{ status, totalDurationMs, checks: [{ name, status, durationMs, description }] }</c>.
    /// Exception details are deliberately omitted — the probe is anonymous.
    /// </summary>
    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = entry.Value.Duration.TotalMilliseconds,
                description = entry.Value.Description,
            }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
