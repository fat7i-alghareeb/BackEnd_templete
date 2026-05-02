namespace Taxi.Application.Features.Cars.Commands.UpdateCar;

using MediatR;
using Taxi.Domain.Common.Results;

public record UpdateCarCommand(
    Guid Id,
    string Make,
    string Model,
    int Year,
    string DescriptionEn,
    string DescriptionAr) : IRequest<Result<Updated>>;
