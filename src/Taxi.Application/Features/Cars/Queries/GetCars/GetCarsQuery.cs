namespace Taxi.Application.Features.Cars.Queries.GetCars;

using MediatR;
using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Domain.Common.Results;

public record GetCarsQuery() : ICachedQuery<Result<List<CarDto>>>
{
    public string CacheKey => "cars";

    public string[] Tags => [CacheTags.Cars];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}
