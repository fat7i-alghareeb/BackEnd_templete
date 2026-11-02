namespace Taxi.Application.Features.Cars.Queries.GetCarDetails;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Models;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Application.Features.Cars.Mappers;
using Taxi.Domain.Cars;
using Taxi.Domain.Common.Results;

public class GetCarDetailsQueryHandler(IAppDbContext context)
    : IRequestHandler<GetCarDetailsQuery, Result<PaginatedList<CarDetailsDto>>>
{
    private readonly IAppDbContext context = context;

    public async Task<Result<PaginatedList<CarDetailsDto>>> Handle(GetCarDetailsQuery request, CancellationToken cancellationToken)
    {
        var query = this.context.Cars
            .AsNoTracking()
            .ApplyFilters(request.Search, request.Make, request.Year)
            .ApplySort(request.SortBy, request.SortDirection);

        var page = await PaginatedList<Car>.CreateAsync(query, request.Page, request.PageSize, cancellationToken);

        return page.Map(car => car.ToDetailsDto());
    }
}
