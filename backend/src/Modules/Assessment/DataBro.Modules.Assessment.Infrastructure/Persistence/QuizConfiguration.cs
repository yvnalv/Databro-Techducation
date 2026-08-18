using DataBro.Modules.Assessment.Domain;
using DataBro.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataBro.Modules.Assessment.Infrastructure.Persistence;

internal sealed class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> builder)
    {
        builder.ToTable("quizzes");
        builder.HasKey(q => q.Id);

        // The lesson, from Learning. No foreign key: it crosses a module boundary, and a database
        // constraint there would couple the two schemas exactly as tightly as rule 10 forbids.
        builder.Property(q => q.LessonId).IsRequired();

        // One quiz per lesson, filtered so a deleted quiz's tombstone does not hold the slot.
        builder.HasIndex(q => q.LessonId)
            .IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_quizzes_lesson");

        builder.Property(q => q.Title).HasMaxLength(300).IsRequired();
        builder.Property(q => q.PassingScore);
        builder.Property(q => q.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(q => q.PublishedAt);

        builder.HasMany<Question>("_questions")
            .WithOne()
            .HasForeignKey(q => q.QuizId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_questions").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(q => q.DomainEvents);
        builder.Ignore(q => q.Questions);
        builder.Ignore(q => q.TotalPoints);
    }
}

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions");
        builder.HasKey(q => q.Id);

        builder.Property(q => q.Prompt).HasMaxLength(1000).IsRequired();
        builder.Property(q => q.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(q => q.Order);
        builder.Property(q => q.Points);
        builder.Property(q => q.Explanation).HasMaxLength(2000);

        // Not unique, for the reason the curriculum's ordering indexes are not: reordering rewrites
        // every sibling one UPDATE at a time, so an intermediate state legitimately holds a
        // duplicate. Contiguity is enforced by the aggregate, which normalises after every change.
        builder.HasIndex(q => new { q.QuizId, q.Order });

        builder.HasMany<Choice>("_choices")
            .WithOne()
            .HasForeignKey(c => c.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_choices").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(q => q.Choices);
    }
}

internal sealed class ChoiceConfiguration : IEntityTypeConfiguration<Choice>
{
    public void Configure(EntityTypeBuilder<Choice> builder)
    {
        builder.ToTable("choices");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Text).HasMaxLength(1000).IsRequired();

        // The answer key, stored plainly. Nothing here hides it — the guarantee is that no
        // learner-facing DTO can carry it (see Contracts.cs), which is enforced by types rather
        // than by a column being secret.
        builder.Property(c => c.IsCorrect);
        builder.Property(c => c.Order);

        builder.HasIndex(c => new { c.QuestionId, c.Order });
    }
}
