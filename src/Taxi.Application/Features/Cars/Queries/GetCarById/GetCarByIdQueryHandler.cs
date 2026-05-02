namespace Taxi.Application.Features.Cars.Queries.GetCarById;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Application.Features.Cars.Mappers;
using Taxi.Domain.Cars;
using Taxi.Domain.Common.Results;

public class GetCarByIdQueryHandler(IAppDbContext context, ILanguageContext languageContext) : IRequestHandler<GetCarByIdQuery, Result<CarDto>>
{
    private readonly IAppDbContext context = context;
    private readonly ILanguageContext languageContext = languageContext;

    public async Task<Result<CarDto>> Handle(GetCarByIdQuery request, CancellationToken cancellationToken)
    {
        var car = await this.context.Cars
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (car is null)
        {
            return CarErrors.NotFound;
        }

        return car.ToDto(this.languageContext.Language);
    }
}
