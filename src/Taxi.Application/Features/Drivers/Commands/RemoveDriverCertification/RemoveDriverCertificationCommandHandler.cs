namespace Taxi.Application.Features.Drivers.Commands.RemoveDriverCertification;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Common.Results;
using Taxi.Domain.Drivers;

public class RemoveDriverCertificationCommandHandler(IAppDbContext context, HybridCache cache)
    : IRequestHandler<RemoveDriverCertificationCommand, Result<Deleted>>
{
    private readonly IAppDbContext context = context;
    private readonly HybridCache cache = cache;

    public async Task<Result<Deleted>> Handle(RemoveDriverCertificationCommand request, CancellationToken cancellationToken)
    {
        var driver = await this.context.Drivers.FirstOrDefaultAsync(d => d.Id == request.DriverId, cancellationToken);
        if (driver is null)
        {
            return DriverErrors.NotFound;
        }

        var removed = driver.RemoveCertification(request.CertificationId);
        if (removed.IsError)
        {
            return removed.Errors;
        }

        await this.context.SaveChangesAsync(cancellationToken);
        await this.cache.RemoveByTagAsync(CacheTags.Drivers, cancellationToken);

        return Result.Deleted;
    }
}
