namespace Taxi.Domain.Common;

/// <summary>
/// Marks an entity whose rows are never physically deleted. Handlers keep calling
/// <c>DbSet.Remove(entity)</c>; <c>SoftDeleteInterceptor</c> turns that into an update that sets
/// these three properties, and a global query filter hides deleted rows from every query.
/// <para>
/// Like the audit fields on <see cref="AuditableEntity"/>, the setters are public only so the
/// interceptor can stamp them — domain code never assigns them.
/// </para>
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTimeOffset? DeletedAtUtc { get; set; }

    string? DeletedBy { get; set; }
}
