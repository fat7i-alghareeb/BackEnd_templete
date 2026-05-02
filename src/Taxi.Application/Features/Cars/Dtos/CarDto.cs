namespace Taxi.Application.Features.Cars.Dtos;

public record CarDto(Guid Id, string Make, string Model, int Year, string Description);
