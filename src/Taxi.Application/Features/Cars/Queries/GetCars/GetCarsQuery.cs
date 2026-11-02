namespace Taxi.Application.Features.Cars.Queries.GetCars;

using System.Globalization;
using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Models;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Contracts.Common;
using Taxi.Contracts.Requests.Cars;
using Taxi.Domain.Common.Results;

/// <summary>
/// One filtered, sorted page of cars. Every input is part of <see cref="CacheKey"/> (normalised, so
/// <c>?make=Tesla</c> and <c>?make=tesla</c> share an entry); <see cref="CacheTags.Cars"/> evicts
/// every combination at once on any car write.
/// </summary>
public record GetCarsQuery(
    int Page = Paging.DefaultPage,
    int PageSize = Paging.DefaultPageSize,
    string? Search = null,
    string? Make = null,
    int? Year = null,
    string? SortBy = null,
    string? SortDirection = null) : ICachedQuery<Result<PaginatedList<CarDto>>>
{
    public string CacheKey => string.Join(
        '-',
        "cars",
        $"p{this.Page}",
        $"s{Paging.ClampPageSize(this.PageSize)}",
        $"q{Normalize(this.Search)}",
        $"m{Normalize(this.Make)}",
        $"y{this.Year?.ToString(CultureInfo.InvariantCulture)}",
        $"o{Normalize(this.SortBy) ?? CarSortFields.Default.ToLowerInvariant()}",
        $"d{Normalize(this.SortDirection) ?? SortDirections.Desc}");

    public string[] Tags => [CacheTags.Cars];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
