namespace DataBro.Platform.Messaging;

/// <summary>
/// Handles an integration event delivered from the outbox.
///
/// <para>
/// <b>Must be idempotent.</b> Delivery is at-least-once, so a handler will occasionally run twice for
/// the same message. Sending a second email is a nuisance; issuing a second certificate or charging a
/// second time is not, so the obligation is on the handler and is stated in the interface it
/// implements rather than in a document nobody reads at the point of writing one.
/// </para>
/// <para>
/// A handler that throws is retried with backoff and eventually dead-lettered. Throwing is therefore
/// the correct response to a transient failure and the wrong one to a permanent one — swallow what
/// cannot be fixed by trying again.
/// </para>
/// </summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : class, IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken ct = default);
}
