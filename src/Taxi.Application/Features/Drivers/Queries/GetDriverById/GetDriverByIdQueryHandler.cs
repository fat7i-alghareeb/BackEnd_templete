namespace Taxi.Application.Features.Drivers.Queries.GetDriverById;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Drivers.Dtos;
using Taxi.Application.Features.Drivers.Mappers;
using Taxi.Domain.Common.Results;
using Taxi.Domain.Drivers;

public class GetDriverByIdQueryHandler(IAppDbContext context, ILanguageContext languageContext)
    : IRequestHandler<GetDriverByIdQuery, Result<DriverDto>>
{
    private readonly IAppDbContext context = context;
    private readonly ILanguageContext languageContext = languageContext;

    public async Task<Result<DriverDto>> Handle(GetDriverByIdQuery request, CancellationToken cancellationToken)
    {
        // Owned collections are loaded with their owner automatically — no Include needed.
        var driver = await this.context.Drivers
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        return driver is null
            ? DriverErrors.NotFound
            : driver.ToDto(this.languageContext.Language);
    }
}
