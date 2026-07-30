using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Domain;

/// <summary>
/// Links an article to a tag. Owned by the <see cref="Article"/> aggregate (tags are part of the
/// article's own state, CT-11), so it holds a bare <see cref="TagId"/> rather than a navigation to
/// <see cref="Tag"/> — a navigation would let callers traverse from one aggregate into another and
/// erode the boundary.
/// </summary>
public sealed class ArticleTag : Entity
{
    public Guid ArticleId { get; private set; }
    public Guid TagId { get; private set; }

    private ArticleTag() { } // EF

    internal ArticleTag(Guid id, Guid articleId, Guid tagId) : base(id)
    {
        ArticleId = articleId;
        TagId = tagId;
    }
}
