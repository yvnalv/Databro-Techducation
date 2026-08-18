using DataBro.Modules.Learning.Domain;
using DataBro.Platform.Persistence;
using DataBro.Platform.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Learning.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Learning module. Owns the <c>learning</c> schema; no cross-module tables
/// (docs/ARCHITECTURE.md). Lesson bodies live in Content and are read through
/// <c>ILessonContentReader</c>, never joined to.
/// </summary>
public sealed class LearningDbContext(DbContextOptions<LearningDbContext> options) : DbContext(options)
{
    public const string Schema = "learning";

    public DbSet<Course> Courses => Set<Course>();
    public DbSet<LearningPath> LearningPaths => Set<LearningPath>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LearningDbContext).Assembly);
        modelBuilder.ApplyClientGeneratedKeys();
        modelBuilder.ApplySoftDeleteQueryFilter();

        // Learning's own queue, in Learning's schema. Written by this context so an outbox row and
        // the state change that caused it share one transaction.
        modelBuilder.ApplyOutbox();
    }
}
