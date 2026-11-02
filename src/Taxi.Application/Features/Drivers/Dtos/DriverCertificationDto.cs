namespace Taxi.Application.Features.Drivers.Dtos;

/// <param name="Name">Resolved to the request language.</param>
public record DriverCertificationDto(Guid Id, string Name, DateOnly ExpiresOn);
