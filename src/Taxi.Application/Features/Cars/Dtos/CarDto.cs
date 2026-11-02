namespace Taxi.Application.Features.Cars.Dtos;

/// <param name="HasImage">Whether <c>GET /api/v1/cars/{id}/image</c> returns an image.</param>
public record CarDto(Guid Id, string Make, string Model, int Year, string Description, bool HasImage = false);
