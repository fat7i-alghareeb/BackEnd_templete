using System.ComponentModel.DataAnnotations;

namespace Taxi.Api.Infrastructure.RateLimiting;

/// <summary>
/// Bound from the <c>RateLimiting</c> configuration section. Every value has a safe default, so the
/// section is optional; integration tests override it to exercise the limiter quickly.
/// </summary>
public sealed class RateLimitingSettings
{
    public const string SectionName = "RateLimiting";

    [Required]
    public SlidingWindowSettings Default { get; set; } = new();

    [Required]
    public FixedWindowSettings Auth { get; set; } = new();

    public sealed class SlidingWindowSettings
    {
        [Range(1, int.MaxValue)]
        public int PermitLimit { get; set; } = 100;

        [Range(1, 3600)]
        public int WindowSeconds { get; set; } = 60;

        [Range(1, 60)]
        public int SegmentsPerWindow { get; set; } = 6;

        [Range(0, int.MaxValue)]
        public int QueueLimit { get; set; } = 10;
    }

    public sealed class FixedWindowSettings
    {
        [Range(1, int.MaxValue)]
        public int PermitLimit { get; set; } = 10;

        [Range(1, 3600)]
        public int WindowSeconds { get; set; } = 60;
    }
}
