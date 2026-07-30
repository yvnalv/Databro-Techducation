using DataBro.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

internal sealed class ArticleConfiguration : IEntityTypeConfiguration<Article>
{
    public void Configure(EntityTypeBuilder<Article> builder)
    {
        builder.ToTable("articles");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Slug)
            .HasConversion(s => s.Value, v => Slug.Create(v))
            .HasColumnName("slug")
            .HasMaxLength(280)
            .IsRequired();
        builder.HasIndex(a => a.Slug).IsUnique();

        builder.Property(a => a.Title).HasMaxLength(300).IsRequired();
        builder.Property(a => a.Summary).HasMaxLength(1000);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Visibility).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Locale).HasMaxLength(10).IsRequired();

        builder.Property(a => a.DraftBlocks).HasJsonbConversion().IsRequired();
        builder.Property(a => a.PublishedBlocks).HasJsonbConversion();
        builder.Property(a => a.Seo).HasJsonbConversion().IsRequired();

        builder.Property(a => a.CurrentVersion);
        builder.Property(a => a.ReadingTimeMinutes);
        builder.Property(a => a.PublishedAt);
        builder.Property(a => a.ScheduledFor);

        builder.HasIndex(a => new { a.Status, a.PublishedAt });
        builder.HasIndex(a => a.CategoryId);
        builder.HasIndex(a => a.TranslationGroupId);

        // Aggregate-owned version history (append-only), mapped via the backing field.
        builder.HasMany(a => a.Versions)
            .WithOne()
            .HasForeignKey(v => v.ArticleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Versions)
            .HasField("_versions")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        // Not persisted.
        builder.Ignore(a => a.DomainEvents);
    }
}

internal sealed class ArticleVersionConfiguration : IEntityTypeConfiguration<ArticleVersion>
{
    public void Configure(EntityTypeBuilder<ArticleVersion> builder)
    {
        builder.ToTable("article_versions");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Version);
        builder.Property(v => v.Title).HasMaxLength(300);
        builder.Property(v => v.Summary).HasMaxLength(1000);
        builder.Property(v => v.Blocks).HasJsonbConversion().IsRequired();

        builder.HasIndex(v => new { v.ArticleId, v.Version }).IsUnique();
    }
}
