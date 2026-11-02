namespace Taxi.Application.Features.Drivers.Commands.AddDriverCertification;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Drivers.Dtos;
using Taxi.Application.Features.Drivers.Mappers;
using Taxi.Domain.Common.Results;
using Taxi.Domain.Drivers;

/// <summary>Changes to a child collection go through the aggregate root: load the driver, call its method, save.</summary>
public class AddDriverCertificationCommandHandler(IAppDbContext context, HybridCache cache, ILanguageContext languageContext)
    : IRequestHandler<AddDriverCertificationCommand, Result<DriverCertificationDto>>
{
    private readonly IAppDbContext context = context;
    private readonly HybridCache cache = cache;
    private readonly ILanguageContext languageContext = languageContext;

    public async Task<Result<DriverCertificationDto>> Handle(AddDriverCertificationCommand request, CancellationToken cancellationToken)
    {
        var driver = await this.context.Drivers.FirstOrDefaultAsync(d => d.Id == request.DriverId, cancellationToken);
        if (driver is null)
        {
            return DriverErrors.NotFound;
        }

        var added = driver.AddCertification(Guid.NewGuid(), request.NameEn, request.NameAr, request.ExpiresOn);
        if (added.IsError)
        {
            return added.Errors;
        }

        await this.context.SaveChangesAsync(cancellationToken);
        await this.cache.RemoveByTagAsync(CacheTags.Drivers, cancellationToken);

        return added.Value.ToDto(this.languageContext.Language);
    }
}
