namespace Taxi.Contracts.Responses.Drivers;

public record DriverResponse(
    Guid Id,
    string Name,
    string LicenseNumber,
    string Status);