namespace Taxi.Application.Features.Cars.Queries.GetCarById;

using MediatR;
using Taxi.Application.Common.Interfaces;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Domain.Common.Results;

public record GetCarByIdQuery(Guid Id) : ICachedQuery<Result<CarDto>>
{
    public string CacheKey => $"cars-{this.Id}";

    public string[] Tags => ["car"];

    public TimeSpan Expiration => TimeSpan.FromMinutes(10);
}
