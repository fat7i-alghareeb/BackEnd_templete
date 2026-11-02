namespace Taxi.Infrastructure.Outbox;

/// <summary>
/// One domain event waiting to be published. Written by <see cref="OutboxInterceptor"/> in the same
/// transaction as the change that raised it; read and marked by <see cref="OutboxProcessor"/>.
/// An Infrastructure persistence concern, not a domain entity.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    /// <summary>Gets the event's CLR type name, resolved only against <c>DomainEvent</c> subtypes on read.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Gets the event serialized as JSON.</summary>
    public string Content { get; init; } = string.Empty;

    public DateTimeOffset OccurredOnUtc { get; init; }

    public DateTimeOffset? ProcessedOnUtc { get; set; }

    public int Attempts { get; set; }

    public DateTimeOffset? NextAttemptOnUtc { get; set; }

    public string? Error { get; set; }
}
