using DataBro.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DataBro.Platform.SharedKernel;
using DataBro.Platform.Persistence;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

/// <summary>
/// The engine's columns, configured once on the root of the hierarchy (ADR-0012).
///
/// <para>
/// EF requires the key and the inherited properties to be configured here rather than on each
/// derived type — and that is the right place anyway: every content unit table carries exactly these
/// columns, and defining them twice would let the two drift. Each concrete type then contributes
/// only its own table name and its own extra columns.
/// </para>
/// </summary>
internal sealed class ContentUnitConfiguration : IEntityTypeConfiguration<ContentUnit>
{
    public void Configure(EntityTypeBuilder<ContentUnit> builder)
    {
        // Table-per-concrete-type: each unit type gets its own table, so a query over articles can
        // never return a lesson body. TPH would put them in one table and reintroduce precisely the
        // leak this design exists to prevent; TPT would add a join to the hottest read path.
        builder.UseTpcMappingStrategy();

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Slug)
            .HasConversion(s => s.Value, v => Slug.Create(v))
            .HasColumnName("slug")
            .HasMaxLength(280)
            .IsRequired();

        builder.Property(c => c.Title).HasMaxLength(300).IsRequired();
        builder.Property(c => c.Summary).HasMaxLength(1000);

        // Published snapshots of the title and summary (CT-6), mirroring published_blocks. Nullable
        // because a unit has none until it is first published.
        builder.Property(c => c.PublishedTitle).HasMaxLength(300);
        builder.Property(c => c.PublishedSummary).HasMaxLength(1000);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(c => c.DraftBlocks).HasJsonbConversion().IsRequired();
        builder.Property(c => c.PublishedBlocks).HasJsonbConversion();
        builder.Property(c => c.SearchText);

        builder.Property(c => c.CurrentVersion);
        builder.Property(c => c.ReadingTimeMinutes);
        builder.Property(c => c.PublishedAt);
        builder.Property(c => c.ScheduledFor);

        builder.Ignore(c => c.DomainEvents);

        // `Versions` is a computed projection over each concrete type's own list, not a navigation.
        // The real relationship is mapped per type against its backing field, which is what points
        // each foreign key at the right table.
        builder.Ignore(c => c.Versions);
    }
}

/// <summary>
/// The version hierarchy, configured on its root for the same reason. Both version tables carry the
/// same columns; only the table they live in differs.
/// </summary>
internal sealed class ContentVersionConfiguration : IEntityTypeConfiguration<ContentVersion>
{
    public void Configure(EntityTypeBuilder<ContentVersion> builder)
    {
        builder.UseTpcMappingStrategy();

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Version);
        builder.Property(v => v.Title).HasMaxLength(300);
        builder.Property(v => v.Summary).HasMaxLength(1000);
        builder.Property(v => v.Blocks).HasJsonbConversion().IsRequired();

        builder.HasIndex(v => new { v.ContentUnitId, v.Version }).IsUnique();
    }
}
