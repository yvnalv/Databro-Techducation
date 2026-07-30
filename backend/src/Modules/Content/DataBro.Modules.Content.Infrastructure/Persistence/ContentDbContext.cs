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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContentDbContext).Assembly);
        modelBuilder.ApplyClientGeneratedKeys();
        modelBuilder.ApplySoftDeleteQueryFilter();
    }
}
