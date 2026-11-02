namespace Taxi.Application.Features.Cars.Queries.GetCarDetails;

using System.Globalization;
using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Models;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Domain.Common.Results;

/// <summary>v2 car list: same inputs, filters and sorting as v1's <c>GetCarsQuery</c>, v2 item shape.</summary>
public record GetCarDetailsQuery(
    int Page = Paging.DefaultPage,
    int PageSize = Paging.DefaultPageSize,
    string? Search = null,
    string? Make = null,
    int? Year = null,
    string? SortBy = null,
    string? SortDirection = null) : ICachedQuery<Result<PaginatedList<CarDetailsDto>>>
{
    public string CacheKey => string.Join(
        '-',
        "car-details",
        $"p{this.Page}",
        $"s{Paging.ClampPageSize(this.PageSize)}",
        $"q{Normalize(this.Search)}",
        $"m{Normalize(this.Make)}",
        $"y{this.Year?.ToString(CultureInfo.InvariantCulture)}",
        $"o{Normalize(this.SortBy)}",
        $"d{Normalize(this.SortDirection)}");

    public string[] Tags => [CacheTags.Cars];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);

    public bool IsCultureAware => false;

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
