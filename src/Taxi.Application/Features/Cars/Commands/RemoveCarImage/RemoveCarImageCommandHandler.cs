namespace Taxi.Application.Features.Cars.Commands.RemoveCarImage;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Domain.Cars;
using Taxi.Domain.Common.Results;

public class RemoveCarImageCommandHandler(IAppDbContext context, IFileStorage storage, HybridCache cache)
    : IRequestHandler<RemoveCarImageCommand, Result<Deleted>>
{
    private readonly IAppDbContext context = context;
    private readonly IFileStorage storage = storage;
    private readonly HybridCache cache = cache;

    public async Task<Result<Deleted>> Handle(RemoveCarImageCommand request, CancellationToken cancellationToken)
    {
        var car = await this.context.Cars.FirstOrDefaultAsync(c => c.Id == request.CarId, cancellationToken);
        if (car is null)
        {
            return CarErrors.NotFound;
        }

        var key = car.ImageKey;

        var removeResult = car.RemoveImage();
        if (removeResult.IsError)
        {
            return removeResult.Errors;
        }

        await this.context.SaveChangesAsync(cancellationToken);

        // After the commit: a failed save must not lose the file the row still points at.
        await this.storage.DeleteAsync(key!, cancellationToken);
        await this.cache.RemoveByTagAsync(CacheTags.Cars, cancellationToken);

        return Result.Deleted;
    }
}
