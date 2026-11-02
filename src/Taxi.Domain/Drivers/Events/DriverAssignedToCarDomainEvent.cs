using Taxi.Domain.Common;

namespace Taxi.Domain.Drivers.Events;

/// <summary>Raised by <see cref="Driver.AssignCar"/> when a driver is put behind the wheel of a car.</summary>
public sealed record DriverAssignedToCarDomainEvent(Guid DriverId, Guid CarId, Guid? PreviousCarId) : DomainEvent;
