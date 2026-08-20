using DataBro.Modules.Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataBro.Modules.Learning.Infrastructure.Persistence;

internal sealed class LearnerStreakConfiguration : IEntityTypeConfiguration<LearnerStreak>
{
    public void Configure(EntityTypeBuilder<LearnerStreak> builder)
    {
        builder.ToTable("learner_streaks");
        builder.HasKey(s => s.Id);

        // No foreign key: the learner is Identity's (rule 10).
        builder.Property(s => s.UserId).IsRequired();
        builder.Property(s => s.Current);
        builder.Property(s => s.Longest);

        // `date`, not `timestamptz`. A streak counts days, and storing an instant would reintroduce
        // exactly the timezone ambiguity the service exists to resolve.
        builder.Property(s => s.LastActiveOn).HasColumnType("date");

        // One row per learner, filtered on is_deleted like every other unique index here.
        builder.HasIndex(s => s.UserId)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_learner_streaks_user");

        builder.Ignore(s => s.DomainEvents);
    }
}
