namespace Taxi.Contracts.Responses.Rides;

public record RideResponse(
    Guid Id,
    string PickupLocation,
    string Destination,
    decimal EstimatedFare,
    string Status,
    Guid? DriverId = null);