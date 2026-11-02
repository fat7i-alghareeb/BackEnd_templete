namespace Taxi.Application.Features.Drivers.Dtos;

public record DriverDto(
    Guid Id,
    string FullName,
    string LicenseNumber,
    Guid? AssignedCarId,
    IReadOnlyList<DriverCertificationDto> Certifications);
