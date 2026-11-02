using Microsoft.EntityFrameworkCore;

using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Cars;
using Taxi.Domain.Drivers;
using Taxi.Domain.Identity;

namespace Taxi.Application.UnitTests.TestDoubles;

/// <summary>
/// An EF Core InMemory <see cref="IAppDbContext"/> for handler tests. It models only what the handlers
/// read and write; provider-specific behaviour (JSONB, xmin, soft-delete interceptor, ExecuteDelete)
/// is covered by the integration tests against real PostgreSQL.
/// </summary>
public sealed class TestAppDbContext(DbContextOptions<TestAppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Car> Cars => this.Set<Car>();

    public DbSet<Driver> Drivers => this.Set<Driver>();

    public DbSet<RefreshToken> RefreshTokens => this.Set<RefreshToken>();

    public static TestAppDbContext Create() =>
        new(new DbContextOptionsBuilder<TestAppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Car>(car =>
        {
            car.HasKey(c => c.Id);
            car.OwnsOne(c => c.Description);
            car.HasQueryFilter(c => !c.IsDeleted);
        });

        modelBuilder.Entity<Driver>(driver =>
        {
            driver.HasKey(d => d.Id);
            driver.OwnsMany(d => d.Certifications, c =>
            {
                c.HasKey(x => x.Id);
                c.OwnsOne(x => x.Name);
            });
            driver.Navigation(d => d.Certifications).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<RefreshToken>().HasKey(r => r.Id);
    }
}
