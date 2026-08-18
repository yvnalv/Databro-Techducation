using DataBro.Platform.Messaging;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Platform.Persistence.Outbox;

public static class OutboxModelBuilderExtensions
{
    /// <summary>
    /// Maps <see cref="OutboxMessage"/> into the calling module's schema.
    ///
    /// <para>
    /// <b>One table per module, not one shared table.</b> The row must be written by the same
    /// <c>DbContext</c> as the state change or it is not in the same transaction, and two contexts
    /// mapping one physical table would also leave "whose migration creates it" with no good answer.
    /// Per-module keeps rule 10 intact and makes extraction mechanical: a module that becomes a
    /// service takes its queue with it.
    /// </para>
    /// </summary>
    public static ModelBuilder ApplyOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.ToTable(OutboxMessage.TableName);
            builder.HasKey(m => m.Id);

            builder.Property(m => m.Type).HasMaxLength(200).IsRequired();
            builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
            builder.Property(m => m.OccurredAt).IsRequired();
            builder.Property(m => m.ProcessedAt);
            builder.Property(m => m.Attempts);
            builder.Property(m => m.NextAttemptAt);
            builder.Property(m => m.Error).HasMaxLength(2000);
            builder.Property(m => m.IsDeadLettered);

            // The only query the processor runs: unprocessed, not parked, due now, oldest first.
            // Filtered so the index stays the size of the backlog rather than of all history —
            // processed rows are kept for audit and would otherwise dominate it.
            builder.HasIndex(m => new { m.NextAttemptAt, m.OccurredAt })
                .HasDatabaseName("ix_outbox_messages_pending")
                .HasFilter("processed_at IS NULL AND is_dead_lettered = false");
        });

        return modelBuilder;
    }
}
