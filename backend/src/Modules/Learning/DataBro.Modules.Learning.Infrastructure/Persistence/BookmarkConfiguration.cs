using DataBro.Modules.Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataBro.Modules.Learning.Infrastructure.Persistence;

internal sealed class BookmarkConfiguration : IEntityTypeConfiguration<Bookmark>
{
    public void Configure(EntityTypeBuilder<Bookmark> builder)
    {
        builder.ToTable("bookmarks");
        builder.HasKey(b => b.Id);

        // Neither id gets a foreign key: UserId crosses a module boundary (rule 10), and TargetId is
        // polymorphic, so there is no single table for a constraint to point at.
        builder.Property(b => b.UserId).IsRequired();
        builder.Property(b => b.TargetId).IsRequired();
        builder.Property(b => b.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.SavedAt).IsRequired();

        // One save per learner per thing, filtered so an un-saved row's tombstone does not stop it
        // being saved again. Unique for the same reason the enrollment pair is: nothing legitimately
        // writes a second row, so it only ever fires on two concurrent clicks - which the service
        // resolves by returning what is already there (idempotent, like enrolling).
        builder.HasIndex(b => new { b.UserId, b.Kind, b.TargetId })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_bookmarks_user_target");

        // The only query the list runs.
        builder.HasIndex(b => new { b.UserId, b.SavedAt });

        builder.Ignore(b => b.DomainEvents);
    }
}
