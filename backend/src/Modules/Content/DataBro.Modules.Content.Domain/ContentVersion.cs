using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Domain;

/// <summary>
/// An immutable, append-only snapshot of a content unit at publish time (rule CT-8). Full audit of
/// what was published and when.
///
/// <para>
/// Named for the engine rather than for articles (ADR-0012): version history belongs to
/// <see cref="ContentUnit"/>, so a lesson body gets the same history as an article without a second
/// implementation. Each concrete unit type keeps its rows in its own table, so
/// <see cref="ContentUnitId"/> is unique only within that table.
/// </para>
/// </summary>
public sealed class ContentVersion : Entity
{
    public Guid ContentUnitId { get; private set; }
    public int Version { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public ContentDocument Blocks { get; private set; } = ContentDocument.Empty;

    private ContentVersion() { } // EF

    internal ContentVersion(
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
