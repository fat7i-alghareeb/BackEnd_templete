namespace Taxi.Application.Features.Drivers.Queries.GetDrivers;

using MediatR;
using Microsoft.EntityFrameworkCore;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Common.Models;
using Taxi.Application.Features.Drivers.Dtos;
using Taxi.Application.Features.Drivers.Mappers;
using Taxi.Domain.Common.Results;
using Taxi.Domain.Drivers;

public class GetDriversQueryHandler(IAppDbContext context, ILanguageContext languageContext)
    : IRequestHandler<GetDriversQuery, Result<PaginatedList<DriverDto>>>
{
    private readonly IAppDbContext context = context;
    private readonly ILanguageContext languageContext = languageContext;

    public async Task<Result<PaginatedList<DriverDto>>> Handle(GetDriversQuery request, CancellationToken cancellationToken)
    {
        var query = this.context.Drivers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim().ToLower();
            query = query.Where(d => d.FullName.ToLower().Contains(term) || d.LicenseNumber.ToLower().Contains(term));
        }

        var page = await PaginatedList<Driver>.CreateAsync(
            query.OrderByDescending(d => d.CreatedAtUtc).ThenBy(d => d.Id),
            request.Page,
            request.PageSize,
            cancellationToken);

        return page.Map(driver => driver.ToDto(this.languageContext.Language));
    }
}
