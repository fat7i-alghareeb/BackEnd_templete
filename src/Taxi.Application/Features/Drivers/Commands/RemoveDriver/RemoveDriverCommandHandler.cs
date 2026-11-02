namespace Taxi.Application.Features.Drivers.Commands.RemoveDriver;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Common.Results;
using Taxi.Domain.Drivers;

/// <summary>Hard delete: the certifications are owned and go with the row; any car assignment ends with it.</summary>
public class RemoveDriverCommandHandler(IAppDbContext context, HybridCache cache)
    : IRequestHandler<RemoveDriverCommand, Result<Deleted>>
{
    private readonly IAppDbContext context = context;
    private readonly HybridCache cache = cache;

    public async Task<Result<Deleted>> Handle(RemoveDriverCommand request, CancellationToken cancellationToken)
    {
        var driver = await this.context.Drivers.FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);
        if (driver is null)
        {
            return DriverErrors.NotFound;
        }

        this.context.Drivers.Remove(driver);
        await this.context.SaveChangesAsync(cancellationToken);
        await this.cache.RemoveByTagAsync(CacheTags.Drivers, cancellationToken);

        return Result.Deleted;
    }
}
