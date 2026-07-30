using DataBro.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Slug)
            .HasConversion(s => s.Value, v => Slug.Create(v))
            .HasColumnName("slug")
            .HasMaxLength(280)
            .IsRequired();

        // TX-1: unique within categories only — a tag may share the slug.
        builder.HasIndex(c => c.Slug).IsUnique();

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(1000);
        builder.Property(c => c.Order);

        // Self-reference for the hierarchy. Restrict, not Cascade: deleting a parent must never
        // silently take its children (and their articles' category) with it.
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.ParentId);
        builder.Ignore(c => c.DomainEvents);
    }
}

internal sealed class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("tags");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Slug)
            .HasConversion(s => s.Value, v => Slug.Create(v))
            .HasColumnName("slug")
            .HasMaxLength(280)
            .IsRequired();

        builder.HasIndex(t => t.Slug).IsUnique();

        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Ignore(t => t.DomainEvents);
    }
}

internal sealed class ArticleTagConfiguration : IEntityTypeConfiguration<ArticleTag>
{
    public void Configure(EntityTypeBuilder<ArticleTag> builder)
    {
        builder.ToTable("article_tags");
        builder.HasKey(at => at.Id);

        // One link per (article, tag) — the domain de-duplicates, this enforces it in the schema.
        builder.HasIndex(at => new { at.ArticleId, at.TagId }).IsUnique();
        builder.HasIndex(at => at.TagId);

        // No navigation to Tag: ArticleTag belongs to the Article aggregate and references Tag by id
        // only. The FK still exists so the database rejects a link to a nonexistent tag.
        builder.HasOne<Tag>()
            .WithMany()
            .HasForeignKey(at => at.TagId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
