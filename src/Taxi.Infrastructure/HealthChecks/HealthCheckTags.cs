namespace Taxi.Infrastructure.HealthChecks;

/// <summary>Tags that decide which health endpoint runs a check.</summary>
public static class HealthCheckTags
{
    /// <summary>Dependencies that must be up before the instance takes traffic — <c>/health/ready</c>.</summary>
    public const string Ready = "ready";
}
