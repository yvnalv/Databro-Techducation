using DataBro.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

/// <summary>
/// Lesson bodies (ADR-0012). Its own table beside <c>articles</c>, which is the whole point: no
/// query over articles can return one.
///
/// <para>
/// Notably absent compared to <see cref="ArticleConfiguration"/>: no author, category, tags, SEO or
/// locale columns — a lesson body carries none of them — and <b>no search vector</b>. Lesson bodies
/// are reached through their course, and indexing them into the same vector that feeds
/// <c>/api/v1/search</c> would put them in the public article search results. Making lessons
/// findable is a Phase 2 decision of its own, tied to the outbox (ADR-0010).
/// </para>
/// </summary>
internal sealed class LessonContentConfiguration : IEntityTypeConfiguration<LessonContent>
{
    public void Configure(EntityTypeBuilder<LessonContent> builder)
    {
        // Only what is specific to a lesson body. The engine's columns are configured once on
        // ContentUnit (ADR-0012) — this type adds no columns of its own, which is the design.
        builder.ToTable("lesson_contents");

        // Unique within this table. Uniqueness *across* articles and lesson bodies is enforced by
        // IContentSlugRegistry, because a database constraint cannot span two tables — the one thing
        // this design pays for that a single-table discriminator got for free (ADR-0012).
        builder.HasIndex(l => l.Slug).IsUnique();

        builder.HasIndex(l => new { l.Status, l.PublishedAt });

        builder.HasMany<LessonContentVersion>("_versions")
            .WithOne()
            .HasForeignKey(v => v.ContentUnitId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_versions").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
