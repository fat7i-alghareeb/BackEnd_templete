namespace Taxi.Application.Features.Cars.EventHandlers;

using MediatR;
using Microsoft.Extensions.Logging;
using Taxi.Domain.Cars.Events;

/// <summary>
/// Reference domain-event handler. Runs in the background, from the outbox, after the car's
/// transaction committed — not inside the HTTP request. Throwing here retries the message with
/// backoff; it never affects the request that created the car. Replace the log line with a real
/// side effect (notify, sync, index) as needed, and keep it idempotent: a message can be delivered
/// more than once if the process dies after the handler ran but before the message was marked.
/// </summary>
public sealed class CarCreatedDomainEventHandler(ILogger<CarCreatedDomainEventHandler> logger)
    : INotificationHandler<CarCreatedDomainEvent>
{
    private readonly ILogger<CarCreatedDomainEventHandler> logger = logger;

    public Task Handle(CarCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "Car created: {CarId} {Make} {Model} ({Year})",
            notification.CarId,
            notification.Make,
            notification.Model,
            notification.Year);

        return Task.CompletedTask;
    }
}
