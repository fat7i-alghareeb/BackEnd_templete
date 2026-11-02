using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Taxi.Infrastructure.Data.Configurations;

public static class ConcurrencyTokenExtensions
{
    /// <summary>
    /// Uses PostgreSQL's <c>xmin</c> system column — the id of the transaction that last wrote the
    /// row — as an optimistic concurrency token. No column is added: Npgsql maps a <c>uint</c>
    /// row-version property to <c>xmin</c>. It is a shadow property, so the domain never sees it.
    /// <para>
    /// Every UPDATE/DELETE of the entity then carries <c>WHERE "xmin" = @original</c>; if another
    /// transaction changed the row since it was loaded, zero rows match and EF throws
    /// <see cref="DbUpdateConcurrencyException"/>, which <c>GlobalExceptionHandler</c> maps to 409.
    /// </para>
    /// </summary>
    /// <returns>The builder, for chaining.</returns>
    public static EntityTypeBuilder<TEntity> UseXminConcurrencyToken<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        builder.Property<uint>("xmin")
               .HasColumnName("xmin")
               .HasColumnType("xid")
               .IsRowVersion();

        return builder;
    }
}
