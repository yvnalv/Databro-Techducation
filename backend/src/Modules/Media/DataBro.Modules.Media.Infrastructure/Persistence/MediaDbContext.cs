using DataBro.Modules.Media.Domain;
using DataBro.Platform.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Modules.Media.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Media module. Owns the <c>media</c> schema; no cross-module tables
/// (docs/ARCHITECTURE.md — module boundaries).
/// </summary>
public sealed class MediaDbContext(DbContextOptions<MediaDbContext> options) : DbContext(options)
{
    public const string Schema = "media";

    public DbSet<MediaAsset> Assets => Set<MediaAsset>();
    public DbSet<MediaVariant> Variants => Set<MediaVariant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MediaDbContext).Assembly);
        modelBuilder.ApplyClientGeneratedKeys();
        modelBuilder.ApplySoftDeleteQueryFilter();
    }
}
