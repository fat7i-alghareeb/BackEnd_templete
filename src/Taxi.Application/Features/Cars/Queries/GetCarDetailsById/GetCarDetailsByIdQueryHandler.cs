namespace Taxi.Application.Features.Cars.Queries.GetCarDetailsById;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Application.Features.Cars.Mappers;
using Taxi.Domain.Cars;
using Taxi.Domain.Common.Results;

public class GetCarDetailsByIdQueryHandler(IAppDbContext context)
    : IRequestHandler<GetCarDetailsByIdQuery, Result<CarDetailsDto>>
{
    private readonly IAppDbContext context = context;

    public async Task<Result<CarDetailsDto>> Handle(GetCarDetailsByIdQuery request, CancellationToken cancellationToken)
    {
        var car = await this.context.Cars
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        return car is null ? CarErrors.NotFound : car.ToDetailsDto();
    }
}
