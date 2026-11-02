using MediatR;

namespace Taxi.Domain.Common;

/// <summary>
/// Something that has happened in the domain. Raise it with <c>AddDomainEvent</c> inside an entity
/// method; Infrastructure's <c>OutboxInterceptor</c> stores it in the same transaction as the change
/// and <c>OutboxProcessor</c> publishes it to its <c>INotificationHandler</c>s afterwards.
/// <para>
/// A <c>record</c> with only serializable, primitive-ish properties (ids, strings, numbers, dates):
/// it is persisted as JSON and rehydrated later, possibly on another instance. Never put an entity in
/// an event.
/// </para>
/// </summary>
public abstract record DomainEvent : INotification;
