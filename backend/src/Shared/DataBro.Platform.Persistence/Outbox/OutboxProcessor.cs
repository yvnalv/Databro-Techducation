using System.Text.Json;
using DataBro.Platform.Abstractions;
using DataBro.Platform.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DataBro.Platform.Persistence.Outbox;

/// <summary>
/// Drains one module's outbox.
///
/// <para>
/// Generic over the context because each module owns its own table (see
/// <see cref="OutboxModelBuilderExtensions.ApplyOutbox"/>), so there is one processor per module and
/// none of them knows about the others.
/// </para>
/// </summary>
public sealed class OutboxProcessor<TContext>(
    TContext db,
    OutboxRegistry registry,
    IServiceProvider services,
    IClock clock,
    ILogger<OutboxProcessor<TContext>> logger)
    where TContext : DbContext
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>How many attempts before a message is parked rather than retried forever.</summary>
    public const int MaxAttempts = 8;

    /// <summary>
    /// Processes one batch. Returns how many were handled successfully.
    ///
    /// <para>
    /// Batched rather than draining everything: a long sweep holds a connection and delays whatever
    /// else the job runner has queued, and the sweep runs often enough that a backlog clears over a
    /// few passes.
    /// </para>
    /// </summary>
    public async Task<int> ProcessBatchAsync(int batchSize = 20, CancellationToken ct = default)
    {
        var now = clock.UtcNow;

        var pending = await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null
                && !m.IsDeadLettered
                && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
            .OrderBy(m => m.OccurredAt)
            .Take(batchSize)
            .ToListAsync(ct);

        if (pending.Count == 0) return 0;

        var handled = 0;

        foreach (var message in pending)
        {
            try
            {
                await DispatchAsync(message, ct);
                message.MarkProcessed(clock.UtcNow);
                handled++;
            }
            catch (Exception ex)
            {
                message.MarkFailed(ex.ToString(), clock.UtcNow, MaxAttempts);

                // Logged per message rather than per batch: one poisoned message must not hide the
                // twenty that succeeded around it.
                logger.LogWarning(
                    ex,
                    "Outbox message {MessageId} ({Type}) failed on attempt {Attempts}.{Parked}",
                    message.Id, message.Type, message.Attempts,
                    message.IsDeadLettered ? " Dead-lettered." : string.Empty);
            }
        }

        // One save for the batch. A message handled twice is already the contract (at-least-once),
        // so batching the bookkeeping costs nothing that the design does not already promise.
        await db.SaveChangesAsync(ct);

        return handled;
    }

    private async Task DispatchAsync(OutboxMessage message, CancellationToken ct)
    {
        var eventType = registry.TypeFor(message.Type);

        if (eventType is null)
        {
            // Not a failure: a message queued by a newer deployment can be read by an older one
            // during a rollout. Leaving it alone lets the deployment that understands it take it,
            // where counting an attempt would burn its retries for being early.
            logger.LogDebug("Outbox message {MessageId} has unknown type {Type}; leaving it.",
                message.Id, message.Type);
            return;
        }

        var payload = JsonSerializer.Deserialize(message.Payload, eventType, SerializerOptions)
            ?? throw new InvalidOperationException(
                $"Outbox message {message.Id} of type {message.Type} deserialised to null.");

        var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
        var handlers = services.GetServices(handlerType).Where(h => h is not null).ToList();

        if (handlers.Count == 0)
        {
            // An event nobody listens to is a normal state — publishing is not conditional on
            // someone caring yet — so it is marked processed rather than retried into a dead letter.
            logger.LogDebug("No handler for {Type}; message {MessageId} processed.",
                message.Type, message.Id);
            return;
        }

        // Sequentially, and deliberately: handlers share the scope's DbContext, which is not
        // thread-safe, and one handler failing should not leave the others in an unknown state.
        foreach (var handler in handlers)
        {
            var method = handlerType.GetMethod(nameof(IIntegrationEventHandler<IIntegrationEvent>.HandleAsync))!;
            await (Task)method.Invoke(handler, [payload, ct])!;
        }
    }
}
