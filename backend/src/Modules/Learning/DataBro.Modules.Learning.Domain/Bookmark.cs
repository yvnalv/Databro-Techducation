using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Learning.Domain;

/// <summary>
/// What a bookmark points at.
///
/// <para>
/// A discriminator rather than one table per kind, because a learner's saved list is read as one
/// list ordered by when they saved things — and a UNION across three tables to render one page is a
/// worse trade than a column.
/// </para>
/// <para>
/// <b>Articles are deliberately absent for now.</b> They belong to Content, which today holds no
/// learner-owned data at all; saving one is a genuinely different change from saving a course.
/// The discriminator exists so adding it later is a new value and a resolver, not a migration.
/// </para>
/// </summary>
public enum BookmarkKind
{
    Course,
    Lesson,
}

/// <summary>
/// One thing a learner saved for later.
///
/// <para>
/// Its own aggregate root, like <see cref="Enrollment"/> and for the same reason: it is learner-owned
/// and written at a rate that has nothing to do with how often a course is edited. Nothing else in
/// the module has a reason to load a bookmark.
/// </para>
/// <para>
/// Deliberately thin — a user, a kind, a target id, and when. No title, no slug, no summary. Copying
/// those in would make a saved list that quietly disagrees with the thing it points at the moment an
/// author renames anything, and the whole value of a bookmark is that it still resolves.
/// </para>
/// </summary>
public sealed class Bookmark : AggregateRoot
{
    /// <summary>The learner, from Identity. An id across a module boundary, never a navigation.</summary>
    public Guid UserId { get; private set; }

    public BookmarkKind Kind { get; private set; }

    /// <summary>The course or lesson id. Resolved at read time, never denormalised.</summary>
    public Guid TargetId { get; private set; }

    public DateTimeOffset SavedAt { get; private set; }

    private Bookmark() { } // EF

    public static Bookmark Create(Guid id, Guid userId, BookmarkKind kind, Guid targetId, DateTimeOffset now) =>
        new()
        {
            Id = id,
            UserId = userId,
            Kind = kind,
            TargetId = targetId,
            SavedAt = now,
        };
}
