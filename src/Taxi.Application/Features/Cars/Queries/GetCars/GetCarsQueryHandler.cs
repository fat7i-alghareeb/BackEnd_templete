namespace Taxi.Application.Features.Cars.Queries.GetCars;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Models;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Application.Features.Cars.Mappers;
using Taxi.Contracts.Common;
using Taxi.Contracts.Requests.Cars;
using Taxi.Domain.Cars;
using Taxi.Domain.Common.Results;

public class GetCarsQueryHandler(IAppDbContext context, ILanguageContext languageContext)
    : IRequestHandler<GetCarsQuery, Result<PaginatedList<CarDto>>>
{
    private readonly IAppDbContext context = context;
    private readonly ILanguageContext languageContext = languageContext;

    public async Task<Result<PaginatedList<CarDto>>> Handle(GetCarsQuery request, CancellationToken cancellationToken)
    {
        var query = this.context.Cars.AsNoTracking();

        // ToLower() on both sides translates to lower(...) in SQL, so the match is case-insensitive
        // on any provider without reaching for Npgsql's ILIKE from the Application layer.
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(c => c.Make.ToLower().Contains(term) || c.Model.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(request.Make))
        {
            var make = request.Make.Trim().ToLower();
            query = query.Where(c => c.Make.ToLower() == make);
        }

        if (request.Year is int year)
        {
            query = query.Where(c => c.Year == year);
        }

        var page = await PaginatedList<Car>.CreateAsync(
            ApplySort(query, request.SortBy, request.SortDirection),
            request.Page,
            request.PageSize,
            cancellationToken);

        // LocalizedText is resolved in memory, after materialisation — see CarMapper.
        return page.Map(car => car.ToDto(this.languageContext.Language));
    }

    /// <summary>
    /// Maps the whitelisted <see cref="CarSortFields"/> value to a key selector. The validator has
    /// already rejected anything else. Id is always the final tie-breaker so paging is deterministic.
    /// </summary>
    private static IOrderedQueryable<Car> ApplySort(IQueryable<Car> query, string? sortBy, string? sortDirection)
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
