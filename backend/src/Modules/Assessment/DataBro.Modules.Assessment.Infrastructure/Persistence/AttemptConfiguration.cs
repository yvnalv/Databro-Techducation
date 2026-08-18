using DataBro.Modules.Assessment.Domain;
using DataBro.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataBro.Modules.Assessment.Infrastructure.Persistence;

internal sealed class AttemptConfiguration : IEntityTypeConfiguration<QuizAttempt>
{
    public void Configure(EntityTypeBuilder<QuizAttempt> builder)
    {
        builder.ToTable("quiz_attempts");
        builder.HasKey(a => a.Id);

        // No foreign key on UserId: it crosses a module boundary (rule 10).
        builder.Property(a => a.UserId).IsRequired();
        builder.Property(a => a.QuizId).IsRequired();

        builder.Property(a => a.StartedAt).IsRequired();
        builder.Property(a => a.SubmittedAt);
        builder.Property(a => a.Score);
        builder.Property(a => a.TotalPoints);
        builder.Property(a => a.Passed);

        // The two queries that exist: this learner's history for a quiz, and their open attempt.
        // Deliberately **not** unique — retakes are the point, so a learner has many attempts at one
        // quiz and only the *unsubmitted* one is at most singular, which a partial unique index
        // could express but at the cost of failing a legitimate concurrent start rather than
        // resuming it. The service resolves that by resuming, which is the behaviour we want anyway.
        builder.HasIndex(a => new { a.UserId, a.QuizId, a.StartedAt });

        builder.HasMany<AttemptAnswer>("_answers")
            .WithOne()
            .HasForeignKey(a => a.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_answers").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(a => a.DomainEvents);
        builder.Ignore(a => a.Answers);
        builder.Ignore(a => a.IsSubmitted);
        builder.Ignore(a => a.Percentage);
    }
}

internal sealed class AttemptAnswerConfiguration : IEntityTypeConfiguration<AttemptAnswer>
{
    public void Configure(EntityTypeBuilder<AttemptAnswer> builder)
    {
        builder.ToTable("attempt_answers");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.QuestionId).IsRequired();
        builder.Property(a => a.PointsEarned);

        // A short list on a row that is always loaded whole. A join table would mean another join on
        // every attempt read for data nothing ever queries across.
        builder.Property<List<Guid>>("_selectedChoiceIds")
            .HasColumnName("selected_choice_ids")
            .HasJsonbConversion()
            .IsRequired();
        builder.Ignore(a => a.SelectedChoiceIds);

        builder.HasIndex(a => new { a.AttemptId, a.QuestionId }).IsUnique()
            .HasFilter("is_deleted = false")
            .HasDatabaseName("ix_attempt_answers_attempt_question");
    }
}
