namespace Taxi.Infrastructure.Data.Interceptors;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Common;

public class AuditableEntityInterceptor(IUser user, TimeProvider dateTime) : SaveChangesInterceptor
{
    private readonly IUser user = user;
    private readonly TimeProvider dateTime = dateTime;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        this.UpdateEntities(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        this.UpdateEntities(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public void UpdateEntities(DbContext? context)
    {
        if (context == null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            // HasChangedOwnedEntities matters: editing only a LocalizedText leaves the
            // owner Unchanged, and without it LastModifiedUtc would never move.
            if (entry.State is EntityState.Added or EntityState.Modified || entry.HasChangedOwnedEntities())
            {
                var utcNow = this.dateTime.GetUtcNow();

                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedBy = this.user.Id;
                    entry.Entity.CreatedAtUtc = utcNow;
                }

                entry.Entity.LastModifiedBy = this.user.Id;
                entry.Entity.LastModifiedUtc = utcNow;

                foreach (var ownedEntry in entry.References)
                {
                    if (ownedEntry.TargetEntry is { Entity: AuditableEntity ownedEntity } && ownedEntry.TargetEntry.State is EntityState.Added or EntityState.Modified)
                    {
                        if (ownedEntry.TargetEntry.State == EntityState.Added)
                        {
                            ownedEntity.CreatedBy = this.user.Id;
                            ownedEntity.CreatedAtUtc = utcNow;
                        }

                        ownedEntity.LastModifiedBy = this.user.Id;
                        ownedEntity.LastModifiedUtc = utcNow;
                    }
                }
            }
        }
    }
}
