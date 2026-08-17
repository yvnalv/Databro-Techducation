using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Domain;

/// <summary>
/// An immutable, append-only snapshot of a content unit at publish time (rule CT-8). Full audit of
/// what was published and when.
///
/// <para>
/// Abstract, with one concrete type per content unit type, because each unit type keeps its history
/// in its own table — the same table-per-concrete-type rule as the units themselves (ADR-0012). A
/// single shared version table is not an option: two owner tables cannot share one foreign-key
/// column, so the relationship EF needs to load history through an aggregate could not be expressed.
/// </para>
/// <para>
/// The shared shape is what matters to everything above the domain: the version DTOs map from this
/// base, so the API contract does not know there is more than one table.
/// </para>
/// </summary>
public abstract class ContentVersion : Entity
{
    public Guid ContentUnitId { get; protected set; }
    public int Version { get; protected set; }
    public string Title { get; protected set; } = string.Empty;
    public string Summary { get; protected set; } = string.Empty;
    public ContentDocument Blocks { get; protected set; } = ContentDocument.Empty;

    protected ContentVersion() { } // EF

    protected ContentVersion(
        Guid id, Guid contentUnitId, int version, string title, string summary, ContentDocument blocks)
        : base(id)
    {
        ContentUnitId = contentUnitId;
        Version = version;
        Title = title;
        Summary = summary;
        Blocks = blocks;
    }
}

/// <summary>A published snapshot of an <see cref="Article"/>. Stored in <c>article_versions</c>.</summary>
public sealed class ArticleVersion : ContentVersion
{
    private ArticleVersion() { } // EF

    internal ArticleVersion(
        Guid id, Guid articleId, int version, string title, string summary, ContentDocument blocks)
        : base(id, articleId, version, title, summary, blocks) { }
}

/// <summary>
/// A published snapshot of a <see cref="LessonContent"/>. Stored in <c>lesson_content_versions</c>.
/// </summary>
public sealed class LessonContentVersion : ContentVersion
{
    private LessonContentVersion() { } // EF

    internal LessonContentVersion(
        Guid id, Guid lessonContentId, int version, string title, string summary, ContentDocument blocks)
        : base(id, lessonContentId, version, title, summary, blocks) { }
}
