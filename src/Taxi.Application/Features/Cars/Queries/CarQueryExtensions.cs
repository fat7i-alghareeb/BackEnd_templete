namespace Taxi.Application.Features.Cars.Queries;

using Taxi.Contracts.Common;
using Taxi.Contracts.Requests.Cars;
using Taxi.Domain.Cars;

/// <summary>
/// Filtering and sorting shared by every car list (v1 <c>GetCarsQuery</c> and v2
/// <c>GetCarDetailsQuery</c>), so two API versions can never filter differently.
/// </summary>
public static class CarQueryExtensions
{
    /// <summary>
    /// <paramref name="search"/>: make or model contains it; <paramref name="make"/>: exact make;
    /// <paramref name="year"/>: exact year. All string matches are case-insensitive — <c>ToLower()</c>
    /// on both sides translates to SQL <c>lower()</c> on any provider.
    /// </summary>
    /// <returns>The filtered query.</returns>
    public static IQueryable<Car> ApplyFilters(this IQueryable<Car> query, string? search, string? make, int? year)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c => c.Make.ToLower().Contains(term) || c.Model.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(make))
        {
            var exact = make.Trim().ToLower();
            query = query.Where(c => c.Make.ToLower() == exact);
        }

        if (year is int y)
        {
            query = query.Where(c => c.Year == y);
        }

        return query;
    }

    /// <summary>
    /// Maps the whitelisted <see cref="CarSortFields"/> value (already validated) to a key selector;
    /// default <c>createdAt desc</c>. Id is always the final tie-breaker so paging is deterministic.
    /// </summary>
    /// <returns>The ordered query.</returns>
    public static IOrderedQueryable<Car> ApplySort(this IQueryable<Car> query, string? sortBy, string? sortDirection)
    {
        var descending = string.IsNullOrWhiteSpace(sortDirection)
            || string.Equals(sortDirection.Trim(), SortDirections.Desc, StringComparison.OrdinalIgnoreCase);

        var field = string.IsNullOrWhiteSpace(sortBy) ? CarSortFields.Default : sortBy.Trim();

        IOrderedQueryable<Car> ordered = field switch
        {
            _ when Is(field, CarSortFields.Make) => descending ? query.OrderByDescending(c => c.Make) : query.OrderBy(c => c.Make),
            _ when Is(field, CarSortFields.Model) => descending ? query.OrderByDescending(c => c.Model) : query.OrderBy(c => c.Model),
            _ when Is(field, CarSortFields.Year) => descending ? query.OrderByDescending(c => c.Year) : query.OrderBy(c => c.Year),
            _ => descending ? query.OrderByDescending(c => c.CreatedAtUtc) : query.OrderBy(c => c.CreatedAtUtc),
        };

        return ordered.ThenBy(c => c.Id);

        static bool Is(string value, string field) => string.Equals(value, field, StringComparison.OrdinalIgnoreCase);
    }
}
