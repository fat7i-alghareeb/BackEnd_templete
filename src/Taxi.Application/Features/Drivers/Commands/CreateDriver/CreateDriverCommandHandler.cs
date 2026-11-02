namespace Taxi.Application.Features.Drivers.Commands.CreateDriver;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Taxi.Application.Common.Caching;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Drivers.Dtos;
using Taxi.Application.Features.Drivers.Mappers;
using Taxi.Domain.Common.Results;
using Taxi.Domain.Drivers;

public class CreateDriverCommandHandler(IAppDbContext context, HybridCache cache, ILanguageContext languageContext)
    : IRequestHandler<CreateDriverCommand, Result<DriverDto>>
{
    private readonly IAppDbContext context = context;
    private readonly HybridCache cache = cache;
    private readonly ILanguageContext languageContext = languageContext;

    public async Task<Result<DriverDto>> Handle(CreateDriverCommand request, CancellationToken cancellationToken)
    {
        var driverResult = Driver.Create(Guid.NewGuid(), request.FullName, request.LicenseNumber);
        if (driverResult.IsError)
        {
            return driverResult.Errors;
        }

        var driver = driverResult.Value;

        // Uniqueness spans aggregates, so it is checked here (and backed by a unique index).
        if (await this.context.Drivers.AnyAsync(d => d.LicenseNumber == driver.LicenseNumber, cancellationToken))
        {
            return DriverErrors.LicenseNumberTaken;
        }

        this.context.Drivers.Add(driver);
        await this.context.SaveChangesAsync(cancellationToken);
        await this.cache.RemoveByTagAsync(CacheTags.Drivers, cancellationToken);

        return driver.ToDto(this.languageContext.Language);
    }
}
