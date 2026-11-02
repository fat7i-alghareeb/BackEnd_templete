namespace Taxi.Application.Features.Drivers.Queries.GetDriverById;

using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Drivers.Dtos;
using Taxi.Domain.Common.Results;

public record GetDriverByIdQuery(Guid Id) : ICachedQuery<Result<DriverDto>>
{
    public string CacheKey => $"drivers-{this.Id}";

    public string[] Tags => [CacheTags.Drivers];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}
