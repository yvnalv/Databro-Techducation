using DataBro.Modules.Assessment.Domain;
using DataBro.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Assessment.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Assessment module. Owns the <c>assessment</c> schema. Lessons live in
/// Learning and are referenced by id only — no cross-module tables, no foreign key.
/// </summary>
public sealed class AssessmentDbContext(DbContextOptions<AssessmentDbContext> options) : DbContext(options)
{
    public const string Schema = "assessment";

    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<QuizAttempt> Attempts => Set<QuizAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AssessmentDbContext).Assembly);
        modelBuilder.ApplyClientGeneratedKeys();
        modelBuilder.ApplySoftDeleteQueryFilter();
    }
}
