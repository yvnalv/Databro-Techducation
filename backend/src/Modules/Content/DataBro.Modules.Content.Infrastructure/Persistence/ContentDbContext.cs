using DataBro.Modules.Content.Domain;
using DataBro.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Content module. Owns the <c>content</c> schema; no cross-module tables
/// (docs/ARCHITECTURE.md — module boundaries).
/// </summary>
public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options) : DbContext(options)
{
    public const string Schema = "content";

    public DbSet<Article> Articles => Set<Article>();
    public DbSet<ArticleVersion> ArticleVersions => Set<ArticleVersion>();
    public DbSet<LessonContent> LessonContents => Set<LessonContent>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ArticleTag> ArticleTags => Set<ArticleTag>();
    public DbSet<Redirect> Redirects => Set<Redirect>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // Backs the trigram similarity fallback for queries full-text search cannot match — a
        // misspelt title (ADR-0006, ADR-0010). Declared here so `dotnet ef` emits the CREATE
        // EXTENSION rather than it being a manual step someone has to remember on a new database.
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContentDbContext).Assembly);
        modelBuilder.ApplyClientGeneratedKeys();
        modelBuilder.ApplySoftDeleteQueryFilter();
    }
}
