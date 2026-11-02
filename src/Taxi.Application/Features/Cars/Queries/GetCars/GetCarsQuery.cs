namespace Taxi.Application.Features.Cars.Queries.GetCars;

using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Models;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Domain.Common.Results;

/// <summary>
/// One page of cars. Every input is part of <see cref="CacheKey"/>, so each page is its own cache
/// entry; <see cref="CacheTags.Cars"/> evicts them all at once on any car write.
/// </summary>
public record GetCarsQuery(
    int Page = Paging.DefaultPage,
    int PageSize = Paging.DefaultPageSize) : ICachedQuery<Result<PaginatedList<CarDto>>>
{
    public string CacheKey => $"cars-p{this.Page}-s{Paging.ClampPageSize(this.PageSize)}";

    public string[] Tags => [CacheTags.Cars];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}
