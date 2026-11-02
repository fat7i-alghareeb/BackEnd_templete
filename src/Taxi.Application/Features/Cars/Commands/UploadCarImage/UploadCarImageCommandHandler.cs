namespace Taxi.Application.Features.Cars.Commands.UploadCarImage;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Files;
using Taxi.Application.Common.Interfaces;
using Taxi.Contracts.Common;
using Taxi.Domain.Cars;
using Taxi.Domain.Common.Results;

public class UploadCarImageCommandHandler(IAppDbContext context, IFileStorage storage, HybridCache cache)
    : IRequestHandler<UploadCarImageCommand, Result<Updated>>
{
    private const string Folder = "cars";

    private readonly IAppDbContext context = context;
    private readonly IFileStorage storage = storage;
    private readonly HybridCache cache = cache;

    public async Task<Result<Updated>> Handle(UploadCarImageCommand request, CancellationToken cancellationToken)
    {
        var car = await this.context.Cars.FirstOrDefaultAsync(c => c.Id == request.CarId, cancellationToken);
        if (car is null)
        {
            return CarErrors.NotFound;
        }

        var header = new byte[ImageFormat.HeaderLength];
        var read = await request.Content.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);

        if (ImageFormat.Detect(header.AsSpan(0, read)) is not { } format)
        {
            return Error.ValidationForProperty("File", LocalizationKeys.Validation.FileTypeInvalid);
        }

        // Re-assemble the stream: the header bytes already read, then the rest.
        await using var content = new MemoryStream();
        await content.WriteAsync(header.AsMemory(0, read), cancellationToken);
        await request.Content.CopyToAsync(content, cancellationToken);
        content.Position = 0;

        var newKey = await this.storage.SaveAsync(content, Folder, format.Extension, cancellationToken);
        var oldKey = car.ImageKey;

        var setResult = car.SetImage(newKey);
        if (setResult.IsError)
        {
            await this.storage.DeleteAsync(newKey, cancellationToken);
            return setResult.Errors;
        }

        try
        {
            await this.context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // The row still points at the old file; do not leave the new one orphaned.
            await this.storage.DeleteAsync(newKey, CancellationToken.None);
            throw;
        }

        if (oldKey is not null)
        {
            await this.storage.DeleteAsync(oldKey, cancellationToken);
        }

        await this.cache.RemoveByTagAsync(CacheTags.Cars, cancellationToken);

        return Result.Updated;
    }
}
