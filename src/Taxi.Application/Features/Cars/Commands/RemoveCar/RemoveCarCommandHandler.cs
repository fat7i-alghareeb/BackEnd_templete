namespace Taxi.Application.Features.Cars.Commands.RemoveCar;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Cars;
using Taxi.Domain.Common.Results;

public class RemoveCarCommandHandler(IAppDbContext context, HybridCache cache) : IRequestHandler<RemoveCarCommand, Result<Deleted>>
{
    private readonly IAppDbContext context = context;
    private readonly HybridCache cache = cache;

    public async Task<Result<Deleted>> Handle(RemoveCarCommand request, CancellationToken cancellationToken)
    {
        var car = await this.context.Cars
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (car is null)
        {
            return CarErrors.NotFound;
        }

        this.context.Cars.Remove(car);

        await this.context.SaveChangesAsync(cancellationToken);

        await this.cache.RemoveByTagAsync(CacheTags.Cars, cancellationToken);

        return Result.Deleted;
    }
}
