namespace Taxi.Application.Features.Cars.Commands.CreateCar;

using MediatR;
using Taxi.Application.Features.Cars.Dtos;
using Taxi.Domain.Common.Results;

public record CreateCarCommand(
    string Make,
    string Model,
    int Year,
    string DescriptionEn,
    string DescriptionAr) : IRequest<Result<CarDto>>;
