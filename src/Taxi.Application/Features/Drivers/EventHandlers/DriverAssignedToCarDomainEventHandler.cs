namespace Taxi.Application.Features.Drivers.EventHandlers;

using MediatR;
using Microsoft.Extensions.Logging;
using Taxi.Domain.Drivers.Events;

/// <summary>Runs from the outbox after the assignment committed (at least once — keep it idempotent).</summary>
public sealed class DriverAssignedToCarDomainEventHandler(ILogger<DriverAssignedToCarDomainEventHandler> logger)
    : INotificationHandler<DriverAssignedToCarDomainEvent>
{
    private readonly ILogger<DriverAssignedToCarDomainEventHandler> logger = logger;

    public Task Handle(DriverAssignedToCarDomainEvent notification, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "Driver {DriverId} assigned to car {CarId} (previously {PreviousCarId})",
            notification.DriverId,
            notification.CarId,
            notification.PreviousCarId);

        return Task.CompletedTask;
    }
}
