namespace Taxi.Application.Features.Cars.Queries.GetCars;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Application.Features.Cars.Mappers;
using Taxi.Domain.Common.Results;

public class GetCarsQueryHandler(IAppDbContext context, ILanguageContext languageContext) : IRequestHandler<GetCarsQuery, Result<List<CarDto>>>
{
    private readonly IAppDbContext context = context;
    private readonly ILanguageContext languageContext = languageContext;

    public async Task<Result<List<CarDto>>> Handle(GetCarsQuery request, CancellationToken cancellationToken)
    {
        var cars = await this.context.Cars
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return cars.ToDto(this.languageContext.Language);
    }
}
