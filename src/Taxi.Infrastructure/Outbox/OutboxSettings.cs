using System.ComponentModel.DataAnnotations;

namespace Taxi.Infrastructure.Outbox;

/// <summary>Bound from the <c>Outbox</c> section and validated on start.</summary>
public sealed class OutboxSettings
{
    public const string SectionName = "Outbox";

    /// <summary>Gets or sets a value indicating whether this instance runs the background processor.</summary>
    public bool Enabled { get; set; } = true;

    [Range(1, 3600)]
    public int PollingIntervalSeconds { get; set; } = 5;

    [Range(1, 1000)]
    public int BatchSize { get; set; } = 20;

    /// <summary>Gets or sets the attempts before a message is parked (kept, with its error, but no longer retried).</summary>
    [Range(1, 100)]
    public int MaxAttempts { get; set; } = 5;

    /// <summary>Gets or sets how long processed messages are kept before being purged.</summary>
    [Range(1, 365)]
    public int RetentionDays { get; set; } = 7;
}
