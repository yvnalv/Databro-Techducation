namespace DataBro.Platform.SharedKernel;

/// <summary>
/// Base class for aggregate roots. Aggregates are the consistency boundary and the only entry point
/// for modifying their internals. They record domain events raised during a unit of work.
/// </summary>
public abstract class AggregateRoot : Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(Guid id) : base(id) { }
    protected AggregateRoot() { }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

/// <summary>A domain event raised inside an aggregate during a state change.</summary>
public interface IDomainEvent
{
    Guid EventId => Guid.NewGuid();
    DateTimeOffset OccurredAt => DateTimeOffset.UtcNow;
}
