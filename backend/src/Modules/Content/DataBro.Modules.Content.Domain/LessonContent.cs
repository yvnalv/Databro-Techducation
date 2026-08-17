using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Domain;

/// <summary>
/// The renderable body of a lesson (ADR-0007, ADR-0012).
///
/// <para>
/// Deliberately almost empty. It <em>is</em> the engine — blocks, versioning, draft/publish — and
/// nothing else, because everything that makes a lesson a lesson belongs to the Learning module:
/// which course module it sits in, its objectives, prerequisites, difficulty and ordering. Learning
/// holds a <c>Lesson</c> that references one of these by id (MODULES.md), and reads it back through
/// the <c>IContentUnitReader</c> contract rather than touching this module's tables.
/// </para>
/// <para>
/// What it deliberately does <b>not</b> have is as informative as what it does: no author byline, no
/// category or tags, no SEO metadata, no locale. A lesson is discovered through its course, not as a
/// standalone indexed page, and giving it those fields would invite exactly the confusion of a
/// lesson turning up where an article belongs.
/// </para>
/// </summary>
public sealed class LessonContent : ContentUnit
{
    private readonly List<LessonContentVersion> _versions = [];

    private LessonContent() { } // EF

    public static LessonContent CreateDraft(
        Guid id, Slug slug, string title, string summary, ContentDocument blocks)
    {
        var lesson = new LessonContent();
        lesson.InitialiseDraft(id, slug, title, summary, blocks);
        return lesson;
    }

    protected override IReadOnlyList<ContentVersion> VersionsCore => _versions;

    protected override void AppendVersion(int version, string title, string summary, ContentDocument blocks)
        => _versions.Add(new LessonContentVersion(Guid.NewGuid(), Id, version, title, summary, blocks));

    protected override void OnPublished()
        => Raise(new LessonContentPublishedDomainEvent(Id, Slug.Value, CurrentVersion));

    protected override void OnUnpublished()
        => Raise(new LessonContentUnpublishedDomainEvent(Id, Slug.Value));
}
