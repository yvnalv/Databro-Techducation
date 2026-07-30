using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Domain;

/// <summary>
/// An immutable, append-only snapshot of an article at publish time (rule CT-8). Full audit of
/// what was published and when.
/// </summary>
public sealed class ArticleVersion : Entity
{
    public Guid ArticleId { get; private set; }
    public int Version { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public ContentDocument Blocks { get; private set; } = ContentDocument.Empty;

    private ArticleVersion() { } // EF

    internal ArticleVersion(Guid id, Guid articleId, int version, string title, string summary, ContentDocument blocks)
        : base(id)
    {
        ArticleId = articleId;
        Version = version;
        Title = title;
        Summary = summary;
        Blocks = blocks;
    }
}
