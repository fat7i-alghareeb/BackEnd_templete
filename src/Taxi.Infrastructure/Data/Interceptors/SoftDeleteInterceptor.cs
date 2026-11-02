namespace Taxi.Infrastructure.Data.Interceptors;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Common;

/// <summary>
/// Converts a delete of any <see cref="ISoftDeletable"/> entity into an update that records when and
/// by whom it was deleted. Registered <b>before</b> <see cref="AuditableEntityInterceptor"/>, so the
/// resulting Modified entry also gets <c>LastModifiedUtc</c> / <c>LastModifiedBy</c>.
/// </summary>
public class SoftDeleteInterceptor(IUser user, TimeProvider dateTime) : SaveChangesInterceptor
{
    private readonly IUser user = user;
    private readonly TimeProvider dateTime = dateTime;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        this.ConvertDeletes(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        this.ConvertDeletes(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ConvertDeletes(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<ISoftDeletable>().Where(e => e.State == EntityState.Deleted))
        {
            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAtUtc = this.dateTime.GetUtcNow();
            entry.Entity.DeletedBy = this.user.Id;

            // Owned values (e.g. a LocalizedText JSON column) are cascaded to Deleted with their
            // owner; left that way EF would null the column. The row stays, so they stay too.
            foreach (var reference in entry.References)
            {
                if (reference.TargetEntry is { State: EntityState.Deleted } owned && owned.Metadata.IsOwned())
                {
                    owned.State = EntityState.Unchanged;
                }
            }
        }
    }
}
