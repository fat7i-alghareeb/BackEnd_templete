namespace Taxi.Application.Features.Cars.Commands.UpdateCar;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Cars;
using Taxi.Domain.Common.Results;

public class UpdateCarCommandHandler(IAppDbContext context, HybridCache cache) : IRequestHandler<UpdateCarCommand, Result<Updated>>
{
    private readonly IAppDbContext context = context;
    private readonly HybridCache cache = cache;

    public async Task<Result<Updated>> Handle(UpdateCarCommand request, CancellationToken cancellationToken)
    {
        var car = await this.context.Cars
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (car is null)
        {
            return CarErrors.NotFound;
        }

        var updateResult = car.Update(
            request.Make,
            request.Model,
            request.Year,
            request.DescriptionEn,
            request.DescriptionAr);

        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        await this.context.SaveChangesAsync(cancellationToken);

        await this.cache.RemoveByTagAsync(CacheTags.Cars, cancellationToken);

        return Result.Updated;
    }
}
