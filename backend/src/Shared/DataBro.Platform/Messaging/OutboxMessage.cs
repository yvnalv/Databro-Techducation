namespace DataBro.Platform.Messaging;

/// <summary>
/// One integration event, written in the <b>same transaction</b> as the state change that caused it.
///
/// <para>
/// That transaction is the entire point. An effect published from application code after a commit
/// can be lost — the process dies between the two, and nothing records that anything was owed. An
/// effect published <i>before</i> the commit can be a lie: the mail goes out and the transaction
/// rolls back. Writing a row in the same transaction makes the fact and its consequence atomic, and
/// leaves delivery to a process that can retry.
/// </para>
/// <para>
/// <b>Delivery is at-least-once.</b> A handler can and will run twice — the process can die between
/// the effect and the row being marked processed, and there is no ordering of those two writes that
/// avoids it. Handlers must be idempotent; that is the price of the guarantee, not an oversight.
/// </para>
/// <para>
/// Deliberately <b>not</b> an <c>Entity</c>: audit columns and a soft-delete filter make no sense on
/// a queue row. It has its own tiny shape and no interest in who created it.
/// </para>
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>Table name, shared by every module's copy. Each module owns its own (rule 10).</summary>
    public const string TableName = "outbox_messages";

    public Guid Id { get; private set; }

    /// <summary>
    /// The event's contract name, resolved back to a type through the registry rather than by
    /// <c>Type.GetType</c>. An assembly-qualified name baked into a row outlives the assembly that
    /// wrote it: rename a namespace and every unprocessed message becomes undeliverable.
    /// </summary>
    public string Type { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public int Attempts { get; private set; }

    /// <summary>When to try again. Null means "now"; set into the future on each failure.</summary>
    public DateTimeOffset? NextAttemptAt { get; private set; }

    /// <summary>The last failure, kept after success too — a message that eventually worked but
    /// failed four times first is worth being able to see.</summary>
    public string? Error { get; private set; }

    /// <summary>
    /// True when the message has failed enough times to stop retrying. Parked, never deleted: a
    /// dead-lettered effect is exactly the thing someone needs to read afterwards.
    /// </summary>
    public bool IsDeadLettered { get; private set; }

    private OutboxMessage() { } // EF

    public static OutboxMessage Create(Guid id, string type, string payload, DateTimeOffset occurredAt) =>
        new()
        {
            Id = id,
            Type = type,
            Payload = payload,
            OccurredAt = occurredAt,
        };

    public void MarkProcessed(DateTimeOffset now)
    {
        ProcessedAt = now;
        NextAttemptAt = null;
    }

    /// <summary>
    /// Records a failure and schedules the next attempt with exponential backoff, giving up after
    /// <paramref name="maxAttempts"/>.
    ///
    /// <para>
    /// Backoff rather than a fixed interval because the common failure is a dependency that is down:
    /// hammering it every ten seconds neither helps it recover nor delivers the message sooner.
    /// </para>
    /// </summary>
    public void MarkFailed(string error, DateTimeOffset now, int maxAttempts)
    {
        Attempts++;
        // Truncated: a stack trace from a deep failure can be tens of kilobytes, and a queue table
        // is not a log store.
        Error = error.Length > 2000 ? error[..2000] : error;

        if (Attempts >= maxAttempts)
        {
            IsDeadLettered = true;
            NextAttemptAt = null;
            return;
        }

        // 2s, 4s, 8s, 16s… capped so a long-parked message still retries within a working day.
        var delay = TimeSpan.FromSeconds(Math.Min(Math.Pow(2, Attempts), 3600));
        NextAttemptAt = now.Add(delay);
    }
}
