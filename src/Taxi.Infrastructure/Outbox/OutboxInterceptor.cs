namespace Taxi.Infrastructure.Outbox;

using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Taxi.Domain.Common;

/// <summary>
/// Moves every pending domain event into an <see cref="OutboxMessage"/> row of the <b>same</b>
/// <c>SaveChanges</c> — one transaction, so the change and its events are durable together or not at
/// all. Nothing is published here; <see cref="OutboxProcessor"/> does that afterwards.
/// </summary>
public sealed class OutboxInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    private readonly TimeProvider timeProvider = timeProvider;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        this.AddOutboxMessages(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        this.AddOutboxMessages(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddOutboxMessages(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var entities = context.ChangeTracker.Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count != 0)
            .ToList();

        var now = this.timeProvider.GetUtcNow();

        foreach (var entity in entities)
        {
            foreach (var domainEvent in entity.DomainEvents)
            {
                context.Set<OutboxMessage>().Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = domainEvent.GetType().FullName!,
                    Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                    OccurredOnUtc = now,
                });
            }

            // Cleared now so a second SaveChanges in the same scope cannot store them twice.
            entity.ClearDomainEvents();
        }
    }
}
