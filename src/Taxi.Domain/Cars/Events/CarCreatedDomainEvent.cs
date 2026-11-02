using Taxi.Domain.Common;

namespace Taxi.Domain.Cars.Events;

/// <summary>Raised by <see cref="Car.Create"/>.</summary>
public sealed record CarCreatedDomainEvent(Guid CarId, string Make, string Model, int Year) : DomainEvent;
