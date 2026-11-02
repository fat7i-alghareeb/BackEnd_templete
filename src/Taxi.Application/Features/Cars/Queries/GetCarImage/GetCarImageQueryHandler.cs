namespace Taxi.Application.Features.Cars.Queries.GetCarImage;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Taxi.Application.Common.Files;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Models;
using Taxi.Domain.Cars;
using Taxi.Domain.Common.Results;

public class GetCarImageQueryHandler(IAppDbContext context, IFileStorage storage)
    : IRequestHandler<GetCarImageQuery, Result<FileContent>>
{
    private readonly IAppDbContext context = context;
    private readonly IFileStorage storage = storage;

    public async Task<Result<FileContent>> Handle(GetCarImageQuery request, CancellationToken cancellationToken)
    {
        var imageKey = await this.context.Cars
            .AsNoTracking()
            .Where(c => c.Id == request.CarId)
            .Select(c => new { c.ImageKey })
            .FirstOrDefaultAsync(cancellationToken);

        if (imageKey is null)
        {
            return CarErrors.NotFound;
        }

        if (imageKey.ImageKey is null)
        {
            return CarErrors.ImageNotFound;
        }

        var stream = await this.storage.OpenReadAsync(imageKey.ImageKey, cancellationToken);
        if (stream is null)
        {
            return CarErrors.ImageNotFound;
        }

        return new FileContent(stream, ImageFormat.ContentTypeFor(imageKey.ImageKey));
    }
}
