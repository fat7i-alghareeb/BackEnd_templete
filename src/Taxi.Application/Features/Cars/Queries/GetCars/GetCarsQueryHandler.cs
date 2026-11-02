namespace Taxi.Application.Features.Cars.Queries.GetCars;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Models;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Application.Features.Cars.Mappers;
using Taxi.Domain.Cars;
using Taxi.Domain.Common.Results;

public class GetCarsQueryHandler(IAppDbContext context, ILanguageContext languageContext)
    : IRequestHandler<GetCarsQuery, Result<PaginatedList<CarDto>>>
{
    private readonly IAppDbContext context = context;
    private readonly ILanguageContext languageContext = languageContext;

    public async Task<Result<PaginatedList<CarDto>>> Handle(GetCarsQuery request, CancellationToken cancellationToken)
    {
        // A deterministic order is mandatory for paging; Id breaks ties between equal timestamps.
        var query = this.context.Cars
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAtUtc)
            .ThenBy(c => c.Id);

        var page = await PaginatedList<Car>.CreateAsync(
            query,
            request.Page,
            request.PageSize,
            cancellationToken);

        // LocalizedText is resolved in memory, after materialisation — see CarMapper.
        return page.Map(car => car.ToDto(this.languageContext.Language));
    }
}
