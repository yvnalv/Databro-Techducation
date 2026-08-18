namespace DataBro.Platform.Messaging;

/// <summary>
/// A domain-meaningful event published across module boundaries (e.g. CourseCompleted), carried by
/// the transactional outbox. See docs/ARCHITECTURE.md — Inter-module communication.
///
/// <para>
/// A domain event crosses a module boundary <b>only</b> by also implementing this interface. That is
/// deliberate opt-in at the type level: most domain events are internal bookkeeping, and publishing
/// everything an aggregate raises would make every internal rename someone else's breaking change.
/// </para>
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
}

/// <summary>Convenience base with populated identity/timestamp.</summary>
public abstract record IntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
