using System.Reflection;

using Microsoft.EntityFrameworkCore;

using Taxi.Domain.Common;
using Taxi.Infrastructure.Data;

using Xunit;

namespace Taxi.Api.IntegrationTests.Features;

/// <summary>
/// EF Core silently skips get-only properties unless a configuration names them. That is how
/// RefreshToken.ExpiresOnUtc went unstored and every refresh failed. This test needs no database:
/// it compares each domain entity's public properties with the built model.
/// </summary>
public class ModelMappingTests
{
    [Fact]
    public void EveryPublicPropertyOfEveryDomainEntity_IsMapped()
    {
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=unused")
            .Options);

        var unmapped = context.Model.GetEntityTypes()
            .Where(t => !t.IsOwned() && t.ClrType.IsSubclassOf(typeof(Entity)))
            .SelectMany(t => t.ClrType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != nameof(Entity.DomainEvents))
                .Where(p => t.FindProperty(p.Name) is null && t.FindNavigation(p.Name) is null)
                .Select(p => $"{t.ClrType.Name}.{p.Name}"))
            .ToList();

        Assert.True(unmapped.Count == 0, $"Unmapped entity properties: {string.Join(", ", unmapped)}");
    }
}
