using System.ComponentModel.DataAnnotations.Schema;

namespace Taxi.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; }

    private readonly List<DomainEvent> domainEvents = [];

    [NotMapped]
    public IReadOnlyCollection<DomainEvent> DomainEvents => this.domainEvents.AsReadOnly();

    protected Entity()
    {
    }

    protected Entity(Guid id)
    {
        this.Id = id == Guid.Empty ? Guid.NewGuid() : id;
    }

    public void AddDomainEvent(DomainEvent domainEvent)
    {
        this.domainEvents.Add(domainEvent);
    }

    public void RemoveDomainEvent(DomainEvent domainEvent)
    {
        this.domainEvents.Remove(domainEvent);
    }

    public void ClearDomainEvents()
    {
        this.domainEvents.Clear();
    }
}