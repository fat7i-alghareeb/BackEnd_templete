namespace Taxi.Application.Features.Drivers.Commands.UpdateDriver;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Common.Results;
using Taxi.Domain.Drivers;

public class UpdateDriverCommandHandler(IAppDbContext context, HybridCache cache)
    : IRequestHandler<UpdateDriverCommand, Result<Updated>>
{
    private readonly IAppDbContext context = context;
    private readonly HybridCache cache = cache;

    public async Task<Result<Updated>> Handle(UpdateDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await this.context.Drivers.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);
        if (driver is null)
        {
            return DriverErrors.NotFound;
        }

        var updateResult = driver.Update(request.FullName, request.LicenseNumber);
        if (updateResult.IsError)
        {
            return updateResult.Errors;
        }

        if (await this.context.Drivers.AnyAsync(d => d.Id != driver.Id && d.LicenseNumber == driver.LicenseNumber, cancellationToken))
        {
            return DriverErrors.LicenseNumberTaken;
        }

        await this.context.SaveChangesAsync(cancellationToken);
        await this.cache.RemoveByTagAsync(CacheTags.Drivers, cancellationToken);

        return Result.Updated;
    }
}
