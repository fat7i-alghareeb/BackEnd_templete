namespace Taxi.Application.Features.Cars.Queries.GetCarDetailsById;

using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Domain.Common.Results;

/// <summary>v2 read of one car. Not culture-aware: the payload holds both languages, so one cache entry serves everyone.</summary>
public record GetCarDetailsByIdQuery(Guid Id) : ICachedQuery<Result<CarDetailsDto>>
{
    public string CacheKey => $"car-details-{this.Id}";

    public string[] Tags => [CacheTags.Cars];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);

    public bool IsCultureAware => false;
}
