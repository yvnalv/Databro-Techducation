namespace DataBro.Platform.Messaging;

/// <summary>
/// A domain-meaningful event published across module boundaries (e.g. ArticlePublished).
/// Delivered in-process via a mediator today; reliable effects are dispatched through the
/// transactional outbox. See docs/ARCHITECTURE.md — Inter-module communication.
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
