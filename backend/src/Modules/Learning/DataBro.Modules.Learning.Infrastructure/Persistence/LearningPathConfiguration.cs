using DataBro.Modules.Learning.Domain;
using DataBro.Platform.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataBro.Modules.Learning.Infrastructure.Persistence;

internal sealed class LearningPathConfiguration : IEntityTypeConfiguration<LearningPath>
{
    public void Configure(EntityTypeBuilder<LearningPath> builder)
    {
        builder.ToTable("learning_paths");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Slug)
            .HasConversion(s => s.Value, v => Slug.Create(v))
            .HasColumnName("slug")
            .HasMaxLength(280)
            .IsRequired();
        builder.HasIndex(p => p.Slug).IsUnique();

        builder.Property(p => p.Title).HasMaxLength(300).IsRequired();
        builder.Property(p => p.Summary).HasMaxLength(1000);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Difficulty).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.PublishedAt);

        builder.HasIndex(p => new { p.Status, p.PublishedAt });

        builder.HasMany<PathCourse>("_courses")
            .WithOne()
            .HasForeignKey(pc => pc.LearningPathId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_courses").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.DomainEvents);
        builder.Ignore(p => p.CourseIds);
    }
}

/// <summary>
/// A course's place in a path. Deliberately <b>no foreign key to <c>courses</c></b>: a path
/// references courses it does not own, and the aggregate boundary is the point. Removing a course
/// leaves a row here pointing at nothing, which the read path filters out — the alternative is a
/// cascade that silently rewrites curricula an author did not touch.
/// </summary>
internal sealed class PathCourseConfiguration : IEntityTypeConfiguration<PathCourse>
{
    public void Configure(EntityTypeBuilder<PathCourse> builder)
    {
        builder.ToTable("path_courses");
        builder.HasKey(pc => pc.Id);

        builder.Property(pc => pc.CourseId).IsRequired();
        builder.Property(pc => pc.Order);

        builder.HasIndex(pc => new { pc.LearningPathId, pc.CourseId })
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasIndex(pc => pc.CourseId);
    }
}
