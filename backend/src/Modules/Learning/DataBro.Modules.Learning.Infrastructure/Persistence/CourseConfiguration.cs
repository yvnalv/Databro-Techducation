using DataBro.Modules.Learning.Domain;
using DataBro.Platform.Persistence;
using DataBro.Platform.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;

namespace DataBro.Modules.Learning.Infrastructure.Persistence;

internal sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.ToTable("courses");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Slug)
            .HasConversion(s => s.Value, v => Slug.Create(v))
            .HasColumnName("slug")
            .HasMaxLength(280)
            .IsRequired();
        builder.HasIndex(c => c.Slug).IsUnique();

        builder.Property(c => c.Title).HasMaxLength(300).IsRequired();
        builder.Property(c => c.Summary).HasMaxLength(1000);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Difficulty).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.PublishedAt);

        builder.HasIndex(c => new { c.Status, c.PublishedAt });

        ConfigureSearch(builder);

        // Owned modules, mapped through the backing field. `Modules` is a sorted projection, not a
        // navigation, so EF must be pointed at the list itself.
        builder.HasMany<CourseModule>("_modules")
            .WithOne()
            .HasForeignKey(m => m.CourseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_modules").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(c => c.DomainEvents);
        builder.Ignore(c => c.Modules);

        // Derived from the lessons rather than stored, so they cannot drift from the curriculum.
        builder.Ignore(c => c.LessonCount);
        builder.Ignore(c => c.EstimatedMinutes);
    }

    /// <summary>The shadow property name for the generated tsvector column.</summary>
    internal const string SearchVectorProperty = "SearchVector";

    /// <summary>
    /// Full-text search over courses (ADR-0014).
    ///
    /// <para>
    /// The same generated-column pattern Content proved: PostgreSQL recomputes the vector on every
    /// write, so it cannot fall out of step with the row, and there is no reindex job. Title carries
    /// weight <b>A</b> and summary <b>B</b>; a course has no body of its own, so there is no C.
    /// </para>
    /// <para>
    /// No locale <c>CASE</c> here, unlike articles: a course has no locale column. Courses are
    /// English-only until the curriculum is translated, and pretending otherwise with a stemmer
    /// chosen from nothing would be worse than being explicit about it.
    /// </para>
    /// </summary>
    private static void ConfigureSearch(EntityTypeBuilder<Course> builder)
    {
        builder.Property<NpgsqlTsVector>(SearchVectorProperty)
            .HasColumnName("search_vector")
            // Line endings normalised, or a CRLF working copy and an LF CI runner build different
            // models and CI reports pending changes against a clean checkout.
            .HasComputedColumnSql(SearchVectorSql.ReplaceLineEndings(" "), stored: true);

        builder.HasIndex(SearchVectorProperty)
            .HasDatabaseName("ix_courses_search_vector")
            .HasMethod("gin");
    }

    private const string SearchVectorSql = """
        setweight(to_tsvector('english'::regconfig, coalesce(title, '')), 'A') ||
        setweight(to_tsvector('english'::regconfig, coalesce(summary, '')), 'B')
        """;
}

internal sealed class CourseModuleConfiguration : IEntityTypeConfiguration<CourseModule>
{
    public void Configure(EntityTypeBuilder<CourseModule> builder)
    {
        builder.ToTable("course_modules");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Title).HasMaxLength(300).IsRequired();
        builder.Property(m => m.Summary).HasMaxLength(1000);
        builder.Property(m => m.Order);

        // Ordering index, deliberately **not** unique — see the note on Lesson's equivalent below.
        builder.HasIndex(m => new { m.CourseId, m.Order });

        builder.HasMany<Lesson>("_lessons")
            .WithOne()
            .HasForeignKey(l => l.CourseModuleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation("_lessons").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(m => m.Lessons);
    }
}

internal sealed class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("lessons");
        builder.HasKey(l => l.Id);

        // The body in Content. An id and nothing more — deliberately no foreign key, because it
        // crosses a module boundary and a database constraint there would couple the two schemas
        // exactly as tightly as the direct table access rule 10 forbids.
        builder.Property(l => l.ContentUnitId).IsRequired();
        builder.HasIndex(l => l.ContentUnitId);

        builder.Property(l => l.Order);
        builder.Property(l => l.EstimatedMinutes);
        builder.Property(l => l.Difficulty).HasConversion<string>().HasMaxLength(20);

        // Ordering index, and deliberately **not unique**, though contiguity is an invariant
        // (ADR-0013). A unique constraint here is unenforceable in practice and actively breaks the
        // operation it was meant to protect:
        //
        //   * Reordering rewrites every sibling's position, and EF issues those UPDATEs one at a
        //     time — so an intermediate state legitimately has two rows at the same position and a
        //     unique index rejects the whole save. Reordering three lessons failed outright.
        //   * PostgreSQL can defer a unique *constraint* to commit time, which would solve that, but
        //     a deferrable constraint cannot be partial — and this one must be filtered on
        //     `is_deleted`, or a soft-deleted lesson's tombstone would hold its position forever.
        //
        // So the invariant stays where it is already enforced: the aggregate normalises after every
        // structural change, the lists are private, and no caller can construct a gap or a duplicate.
        builder.HasIndex(l => new { l.CourseModuleId, l.Order });

        // Short lists on a row that is always loaded whole. Separate tables would mean two more
        // joins on the curriculum read for data nothing ever queries across.
        builder.Property<List<string>>("_objectives")
            .HasColumnName("objectives")
            .HasJsonbConversion()
            .IsRequired();
        builder.Ignore(l => l.Objectives);

        builder.Property<List<Guid>>("_prerequisiteLessonIds")
            .HasColumnName("prerequisite_lesson_ids")
            .HasJsonbConversion()
            .IsRequired();
        builder.Ignore(l => l.PrerequisiteLessonIds);
    }
}
