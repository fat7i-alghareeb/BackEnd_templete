namespace Taxi.Application.Features.Drivers.Commands.UnassignDriverCar;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Common.Results;
using Taxi.Domain.Drivers;

public class UnassignDriverCarCommandHandler(IAppDbContext context, HybridCache cache)
    : IRequestHandler<UnassignDriverCarCommand, Result<Updated>>
{
    private readonly IAppDbContext context = context;
    private readonly HybridCache cache = cache;

    public async Task<Result<Updated>> Handle(UnassignDriverCarCommand request, CancellationToken cancellationToken)
    {
        var driver = await this.context.Drivers.FirstOrDefaultAsync(d => d.Id == request.DriverId, cancellationToken);
        if (driver is null)
        {
            return DriverErrors.NotFound;
        }

        var unassigned = driver.UnassignCar();
        if (unassigned.IsError)
        {
            return unassigned.Errors;
        }

        await this.context.SaveChangesAsync(cancellationToken);
        await this.cache.RemoveByTagAsync(CacheTags.Drivers, cancellationToken);

        return Result.Updated;
    }
}
