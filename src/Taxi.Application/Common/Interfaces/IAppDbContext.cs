using Microsoft.EntityFrameworkCore;
using Taxi.Domain.Cars;
using Taxi.Domain.Identity;

namespace Taxi.Application.Common.Interfaces;

public interface IAppDbContext
{
    public DbSet<Car> Cars { get; }

    public DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}