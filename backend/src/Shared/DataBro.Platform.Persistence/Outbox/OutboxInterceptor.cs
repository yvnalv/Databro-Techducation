using System.Text.Json;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Messaging;
using DataBro.Platform.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DataBro.Platform.Persistence.Outbox;

/// <summary>
/// Turns domain events raised during a unit of work into outbox rows, <b>in the same
/// <c>SaveChanges</c></b> as the state change that raised them.
///
/// <para>
/// That is the whole mechanism. Because the rows are written by the same call, they are committed by
/// the same transaction: either the course is complete and the message is queued, or neither
/// happened. No application code has to remember to publish, and no publish can succeed against a
/// change that rolled back.
/// </para>
/// <para>
/// Only events the registry knows are written. A domain event is internal until someone deliberately
/// gives it a contract name — see <see cref="OutboxRegistry"/>.
/// </para>
/// </summary>
public sealed class OutboxInterceptor(OutboxRegistry registry, IClock clock) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            Collect(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            Collect(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    private void Collect(DbContext context)
    {
        // Materialised before adding anything: adding outbox rows mutates the change tracker, and
        // enumerating it lazily while doing so throws.
        var roots = context.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        if (roots.Count == 0) return;

        var now = clock.UtcNow;

        foreach (var root in roots)
        {
            foreach (var domainEvent in root.DomainEvents)
            {
                if (domainEvent is not IIntegrationEvent integrationEvent) continue;

                var name = registry.NameFor(domainEvent.GetType());
                if (name is null) continue;

                context.Add(OutboxMessage.Create(
                    integrationEvent.EventId,
                    name,
                    JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions),
                    integrationEvent.OccurredAt));
            }

            // Cleared here rather than after the commit: the events have been recorded durably by
            // this point, and leaving them on a tracked entity would republish them on the next save
            // of the same instance.
            root.ClearDomainEvents();
        }
    }
}
