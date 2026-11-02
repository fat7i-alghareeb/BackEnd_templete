using System.Linq.Expressions;

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Cars;
using Taxi.Domain.Common;
using Taxi.Domain.Identity;
using Taxi.Infrastructure.Identity;

namespace Taxi.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options), IAppDbContext
{
    public DbSet<Car> Cars => this.Set<Car>();

    public DbSet<RefreshToken> RefreshTokens => this.Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ApplySoftDeleteQueryFilters(builder);
    }

    /// <summary>
    /// Adds <c>WHERE NOT "IsDeleted"</c> to every query over an <see cref="ISoftDeletable"/> entity,
    /// so no handler has to remember it. Opt out per query with <c>IgnoreQueryFilters()</c>
    /// (e.g. an admin "restore" or "show deleted" use case).
    /// </summary>
    private static void ApplySoftDeleteQueryFilters(ModelBuilder builder)
    {
        var softDeletable = builder.Model.GetEntityTypes()
            .Where(t => typeof(ISoftDeletable).IsAssignableFrom(t.ClrType) && t.BaseType is null && !t.IsOwned());

        foreach (var entityType in softDeletable)
        {
            var entity = Expression.Parameter(entityType.ClrType, "e");
            var notDeleted = Expression.Not(Expression.Property(entity, nameof(ISoftDeletable.IsDeleted)));

            entityType.SetQueryFilter(Expression.Lambda(notDeleted, entity));
        }
    }
}
