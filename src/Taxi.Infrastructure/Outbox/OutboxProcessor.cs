namespace Taxi.Infrastructure.Outbox;

using System.Collections.Frozen;
using System.Text.Json;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Common;
using Taxi.Infrastructure.Data;

/// <summary>
/// Publishes one batch of pending <see cref="OutboxMessage"/>s. Called on a timer by
/// <see cref="OutboxBackgroundService"/>, and directly by tests.
/// <list type="bullet">
/// <item>Only one instance processes at a time: the batch runs under an <see cref="IDistributedLock"/>.</item>
/// <item>Each message is published in its own DI scope, so handlers get fresh scoped services.</item>
/// <item>A failure increments <see cref="OutboxMessage.Attempts"/> and schedules a retry with
/// exponential backoff (2, 4, 8 … seconds, capped at 5 minutes). After <c>MaxAttempts</c> the message is
/// parked: kept with its <see cref="OutboxMessage.Error"/>, never retried.</item>
/// <item>Delivery is at-least-once: if the process dies after a handler ran but before the message
/// was marked, it runs again. Handlers must be idempotent.</item>
/// </list>
/// </summary>
public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IDistributedLock distributedLock,
    IOptions<OutboxSettings> settings,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger)
{
    private const string LockKey = "outbox-processor";

    /// <summary>
    /// The only types an outbox row may be deserialized into: concrete <see cref="DomainEvent"/>s from
    /// the Domain assembly. A tampered <c>Type</c> column can never instantiate anything else.
    /// </summary>
    private static readonly FrozenDictionary<string, Type> EventTypes = typeof(DomainEvent).Assembly
        .GetTypes()
        .Where(t => t is { IsAbstract: false, IsClass: true } && t.IsSubclassOf(typeof(DomainEvent)))
        .ToFrozenDictionary(t => t.FullName!, t => t);

    private readonly IServiceScopeFactory scopeFactory = scopeFactory;
    private readonly IDistributedLock distributedLock = distributedLock;
    private readonly OutboxSettings settings = settings.Value;
    private readonly TimeProvider timeProvider = timeProvider;
    private readonly ILogger<OutboxProcessor> logger = logger;

    /// <returns>The number of messages published successfully in this batch.</returns>
    public async Task<int> ProcessAsync(CancellationToken cancellationToken)
    {
        await using var handle = await this.distributedLock.TryAcquireAsync(LockKey, TimeSpan.FromMinutes(5), cancellationToken);
        if (handle is null)
        {
            return 0;
        }

        await using var scope = this.scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = this.timeProvider.GetUtcNow();

        var messages = await context.Set<OutboxMessage>()
            .Where(m => m.ProcessedOnUtc == null
                        && m.Attempts < this.settings.MaxAttempts
                        && (m.NextAttemptOnUtc == null || m.NextAttemptOnUtc <= now))
            .OrderBy(m => m.OccurredOnUtc)
            .Take(this.settings.BatchSize)
            .ToListAsync(cancellationToken);

        var published = 0;

        foreach (var message in messages)
        {
            try
            {
                await this.PublishAsync(message, cancellationToken);

                message.ProcessedOnUtc = this.timeProvider.GetUtcNow();
                message.Error = null;
                published++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                message.Attempts++;
                message.Error = ex.ToString();
                message.NextAttemptOnUtc = this.timeProvider.GetUtcNow() + Backoff(message.Attempts);

                if (message.Attempts >= this.settings.MaxAttempts)
                {
                    this.logger.LogError(ex, "Outbox message {MessageId} ({Type}) parked after {Attempts} attempts.", message.Id, message.Type, message.Attempts);
                }
                else
                {
                    this.logger.LogWarning(ex, "Outbox message {MessageId} ({Type}) failed, attempt {Attempts}; retrying at {NextAttempt}.", message.Id, message.Type, message.Attempts, message.NextAttemptOnUtc);
                }
            }

            // Saved per message, so a crash mid-batch re-delivers as little as possible.
            await context.SaveChangesAsync(cancellationToken);
        }

        await context.Set<OutboxMessage>()
            .Where(m => m.ProcessedOnUtc != null && m.ProcessedOnUtc < now.AddDays(-this.settings.RetentionDays))
            .ExecuteDeleteAsync(cancellationToken);

        return published;
    }

    private static TimeSpan Backoff(int attempts) =>
        TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempts), 300));

    private async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (!EventTypes.TryGetValue(message.Type, out var type))
        {
            throw new InvalidOperationException($"Unknown domain event type '{message.Type}'.");
        }

        var domainEvent = (DomainEvent)(JsonSerializer.Deserialize(message.Content, type)
            ?? throw new InvalidOperationException($"Outbox message {message.Id} deserialized to null."));

        await using var scope = this.scopeFactory.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        await publisher.Publish(domainEvent, cancellationToken);
    }
}
