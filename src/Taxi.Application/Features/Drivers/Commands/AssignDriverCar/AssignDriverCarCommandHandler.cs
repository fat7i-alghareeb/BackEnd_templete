namespace Taxi.Application.Features.Drivers.Commands.AssignDriverCar;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Cars;
using Taxi.Domain.Common.Results;
using Taxi.Domain.Drivers;

/// <summary>
/// The reference for a rule that spans two aggregates. The driver enforces what it can know about
/// itself (not already on this car); the handler checks what needs a query — the car exists (and is
/// not soft-deleted) and no other driver holds it. A unique index on <c>AssignedCarId</c> closes the
/// race between that check and the save.
/// </summary>
public class AssignDriverCarCommandHandler(IAppDbContext context, HybridCache cache)
    : IRequestHandler<AssignDriverCarCommand, Result<Updated>>
{
    private readonly IAppDbContext context = context;
    private readonly HybridCache cache = cache;

    public async Task<Result<Updated>> Handle(AssignDriverCarCommand request, CancellationToken cancellationToken)
    {
        var driver = await this.context.Drivers.FirstOrDefaultAsync(d => d.Id == request.DriverId, cancellationToken);
        if (driver is null)
        {
            return DriverErrors.NotFound;
        }

        if (!await this.context.Cars.AnyAsync(c => c.Id == request.CarId, cancellationToken))
        {
            return CarErrors.NotFound;
        }

        if (await this.context.Drivers.AnyAsync(d => d.Id != driver.Id && d.AssignedCarId == request.CarId, cancellationToken))
        {
            return DriverErrors.CarTaken;
        }

        var assigned = driver.AssignCar(request.CarId);
        if (assigned.IsError)
        {
            return assigned.Errors;
        }

        await this.context.SaveChangesAsync(cancellationToken);
        await this.cache.RemoveByTagAsync(CacheTags.Drivers, cancellationToken);

        return Result.Updated;
    }
}
