namespace Taxi.Application.Features.Drivers.Queries.GetDrivers;

using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Models;
using Taxi.Application.Features.Drivers.Dtos;
using Taxi.Domain.Common.Results;

/// <summary>One page of drivers, newest first. <paramref name="Search"/> matches name or licence number.</summary>
public record GetDriversQuery(
    int Page = Paging.DefaultPage,
    int PageSize = Paging.DefaultPageSize,
    string? Search = null) : ICachedQuery<Result<PaginatedList<DriverDto>>>
{
    public string CacheKey =>
        $"drivers-p{this.Page}-s{Paging.ClampPageSize(this.PageSize)}-q{this.Search?.Trim().ToLowerInvariant()}";

    public string[] Tags => [CacheTags.Drivers];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}
