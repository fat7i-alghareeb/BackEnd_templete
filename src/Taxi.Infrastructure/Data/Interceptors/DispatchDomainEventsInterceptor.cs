namespace Taxi.Infrastructure.Data.Interceptors;

using MediatR;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Taxi.Domain.Common;

/// <summary>
/// Publishes domain events <b>after</b> the transaction commits.
/// <para>
/// Events are collected in <see cref="SavingChangesAsync"/> — the ChangeTracker must be read
/// before the save, because entries are detached or reset afterwards — held on this scoped
/// instance, then published in <see cref="SavedChangesAsync"/>.
/// </para>
/// <para>
/// An event asserts that something <i>has happened</i>, so it is not a fact until the data is
/// durable. The trade-off: a handler that throws no longer rolls the transaction back, so the
/// side effect is lost while the write persists. If a side effect must never be lost, write an
/// outbox row inside the same transaction instead — see the Infrastructure blueprint.
/// </para>
/// </summary>
public class DispatchDomainEventsInterceptor(IPublisher publisher) : SaveChangesInterceptor
{
    private readonly IPublisher publisher = publisher;
    private readonly List<DomainEvent> pendingEvents = [];

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        this.CollectEvents(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        this.CollectEvents(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await this.PublishPendingAsync(cancellationToken);

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        this.PublishPendingAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();

        return base.SavedChanges(eventData, result);
    }

    private void CollectEvents(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var entities = context.ChangeTracker.Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count != 0)
            .ToList();

        foreach (var entity in entities)
        {
            this.pendingEvents.AddRange(entity.DomainEvents);

            // Cleared now so a second SaveChanges in the same scope cannot re-publish them.
            entity.ClearDomainEvents();
        }
    }

    private async ValueTask PublishPendingAsync(CancellationToken cancellationToken)
    {
        if (this.pendingEvents.Count == 0)
        {
            return;
        }

        var events = this.pendingEvents.ToArray();
        this.pendingEvents.Clear();

        foreach (var domainEvent in events)
        {
            await this.publisher.Publish(domainEvent, cancellationToken);
        }
    }
}
