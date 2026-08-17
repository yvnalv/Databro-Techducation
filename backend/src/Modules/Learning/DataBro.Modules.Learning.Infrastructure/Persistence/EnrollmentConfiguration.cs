using DataBro.Modules.Learning.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataBro.Modules.Learning.Infrastructure.Persistence;

internal sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("enrollments");
        builder.HasKey(e => e.Id);

        // The learner, from Identity, and the course. Neither gets a foreign key: UserId crosses a
        // module boundary (rule 10), and CourseId is left unconstrained for the same reason the
        // aggregates are separate — a course is authoring-owned and an enrollment is learner-owned,
        // and a cascade between them is not a behaviour anyone wants discovered at delete time.
        builder.Property(e => e.UserId).IsRequired();
        builder.Property(e => e.CourseId).IsRequired();

        builder.Property(e => e.EnrolledAt).IsRequired();
        builder.Property(e => e.CompletedAt);
        builder.Property(e => e.LastLessonId);
        builder.Property(e => e.LastAccessedAt);

        // One enrollment per learner per course, and **this one is genuinely unique** — unlike the
        // curriculum ordering indexes, which cannot be. Nothing legitimately writes a second row, so
        // the constraint only ever fires on the race it exists to stop: two concurrent enrol clicks
        // both finding no existing row and both inserting. The service catches the violation and
        // returns the existing enrollment, because a double-tapped button is not an error.
        //
        // Filtered on is_deleted so an un-enrolled learner can enrol again; the tombstone must not
        // hold the slot forever.
        builder.HasIndex(e => new { e.UserId, e.CourseId })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_enrollments_user_course");

        // The dashboard query: this learner's enrollments, most recently touched first.
        builder.HasIndex(e => new { e.UserId, e.LastAccessedAt });

        builder.HasMany<LessonProgress>("_progress")
            .WithOne()
            .HasForeignKey(p => p.EnrollmentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_progress").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(e => e.DomainEvents);
        builder.Ignore(e => e.Progress);
        builder.Ignore(e => e.IsCompleted);
        builder.Ignore(e => e.CompletedLessonCount);
    }
}

internal sealed class LessonProgressConfiguration : IEntityTypeConfiguration<LessonProgress>
{
    public void Configure(EntityTypeBuilder<LessonProgress> builder)
    {
        builder.ToTable("lesson_progress");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.LessonId).IsRequired();
        builder.Property(p => p.CompletedAt);

        // One row per lesson per enrollment. Unique for the same reason the enrollment pair is: the
        // aggregate already prevents a duplicate, so the constraint only catches concurrency — two
        // devices ticking the same lesson at once.
        builder.HasIndex(p => new { p.EnrollmentId, p.LessonId })
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_lesson_progress_enrollment_lesson");

        builder.Ignore(p => p.IsCompleted);
    }
}
