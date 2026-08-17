using DataBro.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

internal sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        // Only what is article-specific. The engine's columns — slug, titles, block pair, published
        // snapshot, version counters — are configured once on ContentUnit (ADR-0012).
        builder.ToTable("articles");
        builder.HasIndex(a => a.Slug).IsUnique();

        builder.Property(a => a.Visibility).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Locale).HasMaxLength(10).IsRequired();
        builder.Property(a => a.Seo).HasJsonbConversion().IsRequired();

        ConfigureSearch(builder);

        builder.HasIndex(a => new { a.Status, a.PublishedAt });
        builder.HasIndex(a => a.TranslationGroupId);

        // Category reference (CT-11: at most one). Restrict so a category that still classifies
        // articles cannot be deleted out from under them (TX-2) — the application surfaces this as
        // a conflict with the referencing count rather than letting the database throw.
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(a => a.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => a.CategoryId);

        // Aggregate-owned version history (append-only), mapped through the backing field rather
        // than the `Versions` property. The property is typed to the base `ContentVersion` and lives
        // on ContentUnit; the *field* is this type's own `List<ArticleVersion>`, which is what makes
        // the foreign key point at this table and only this table.
        builder.HasMany<ArticleVersion>("_versions")
            .WithOne()
            .HasForeignKey(v => v.ContentUnitId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_versions").UsePropertyAccessMode(PropertyAccessMode.Field);

        // Aggregate-owned tag links, mapped via the backing field. TagIds is a projection over it.
        builder.HasMany<ArticleTag>("_tags")
            .WithOne()
            .HasForeignKey(at => at.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_tags").UsePropertyAccessMode(PropertyAccessMode.Field);

        // Not persisted.
        builder.Ignore(a => a.DomainEvents);
        builder.Ignore(a => a.TagIds);
    }

    /// <summary>
    /// Full-text search over the articles table (ADR-0010).
    ///
    /// The vector is a <c>GENERATED ALWAYS … STORED</c> column rather than a value the application
    /// writes. That is the whole point: a generated column is recomputed by PostgreSQL on every
    /// write to the row, so it *cannot* fall out of step with the title, summary or body. There is
    /// no reindex job to run and no drift to detect.
    /// </summary>
    private static void ConfigureSearch(EntityTypeBuilder<Article> builder)
    {
        builder.Property(a => a.SearchText).HasColumnName("search_text");

        builder.Property<NpgsqlTsVector>(SearchVectorProperty)
            .HasColumnName("search_vector")
            .HasComputedColumnSql(SearchVectorSql, stored: true);

        // GIN is the right index for tsvector matching: bigger and slower to update than GiST, but
        // this table is written rarely and searched often.
        builder.HasIndex(SearchVectorProperty)
            .HasDatabaseName("ix_articles_search_vector")
            .HasMethod("gin");

        // No trigram index on `title`, deliberately. A `gin_trgm_ops` index answers the `<%`
        // operator, which takes its threshold from the `pg_trgm.word_similarity_threshold` session
        // setting (0.6) rather than an explicit one — and 0.6 is too strict to catch the typos the
        // fallback exists for (see ArticleRepository.FuzzyThreshold). An index that the only query
        // touching it cannot use is pure write-time cost, so it is left out until either the
        // threshold moves to the operator form or scale forces the OpenSearch upgrade.
    }

    /// <summary>The shadow property name for the generated tsvector column.</summary>
    internal const string SearchVectorProperty = "SearchVector";

    /// <summary>
    /// Weighted, locale-aware search vector.
    ///
    /// Weights: title <b>A</b>, summary <b>B</b>, body <b>C</b>. A title match should outrank a
    /// passing mention in paragraph forty, and PostgreSQL's default weight multipliers
    /// (1.0/0.4/0.2/0.1) express exactly that.
    ///
    /// Built from the <b>published</b> title and summary, not the draft ones (CT-6). Indexing the
    /// draft columns made an in-progress headline searchable the moment it was typed.
    ///
    /// The <c>CASE</c> picks the stemmer from the row's own locale, so "belajar" and "pembelajaran"
    /// collapse to one Indonesian stem instead of being treated as unrelated English words. Both
    /// branches are literal <c>regconfig</c> casts because only <c>to_tsvector(regconfig, text)</c>
    /// is IMMUTABLE — the one-argument form reads a session setting and a generated column may not
    /// depend on one.
    /// </summary>
    private const string SearchVectorSql = """
        setweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(published_title, '')), 'A') ||
        setweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(published_summary, '')), 'B') ||
        setweight(to_tsvector(CASE WHEN locale = 'id' THEN 'indonesian'::regconfig ELSE 'english'::regconfig END, coalesce(search_text, '')), 'C')
        """;
}

/// <summary>
/// Version history for articles. The entity is shared with every other content unit type
/// (<see cref="ContentVersion"/>, ADR-0012), but the rows are not: each unit type keeps its history
/// in its own table, matching the table-per-concrete-type mapping of the units themselves.
/// </summary>
// Version tables carry only their table name; the shared columns are configured on ContentVersion.

internal sealed class ArticleVersionConfiguration : IEntityTypeConfiguration<ArticleVersion>
{
    public void Configure(EntityTypeBuilder<ArticleVersion> builder) => builder.ToTable("article_versions");
}

internal sealed class LessonContentVersionConfiguration : IEntityTypeConfiguration<LessonContentVersion>
{
    public void Configure(EntityTypeBuilder<LessonContentVersion> builder)
        => builder.ToTable("lesson_content_versions");
}
