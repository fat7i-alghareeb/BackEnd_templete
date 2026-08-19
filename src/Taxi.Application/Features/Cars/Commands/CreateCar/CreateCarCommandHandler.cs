namespace Taxi.Application.Features.Cars.Commands.CreateCar;

using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Application.Features.Cars.Mappers;
using Taxi.Domain.Cars;
using Taxi.Domain.Common.Results;

public class CreateCarCommandHandler(
    IAppDbContext context,
    HybridCache cache,
    ILanguageContext languageContext) : IRequestHandler<CreateCarCommand, Result<CarDto>>
{
    private readonly IAppDbContext context = context;
    private readonly HybridCache cache = cache;
    private readonly ILanguageContext languageContext = languageContext;

    public async Task<Result<CarDto>> Handle(CreateCarCommand request, CancellationToken cancellationToken)
    {
        var carResult = Car.Create(
            Guid.NewGuid(),
            request.Make,
            request.Model,
            request.Year,
            request.DescriptionEn,
            request.DescriptionAr);

        if (carResult.IsError)
        {
            return carResult.Errors;
        }

        var car = carResult.Value;

        this.context.Cars.Add(car);

        await this.context.SaveChangesAsync(cancellationToken);

        await this.cache.RemoveByTagAsync(CacheTags.Cars, cancellationToken);

        return car.ToDto(this.languageContext.Language);
    }
}
