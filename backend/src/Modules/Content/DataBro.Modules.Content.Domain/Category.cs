using DataBro.Platform.Results;
using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Domain;

/// <summary>
/// A hierarchical content category (docs/BUSINESS_RULES.md TX-1 … TX-3). An article belongs to at
/// most one category (CT-11).
///
/// <para>
/// A separate aggregate from <see cref="Article"/>: categories have their own lifecycle and are
/// referenced by id only, never by navigation property, so the Article aggregate boundary holds.
/// </para>
/// </summary>
public sealed class Category : AggregateRoot
{
    public Slug Slug { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid? ParentId { get; private set; }

    /// <summary>Sibling ordering for navigation; lower sorts first.</summary>
    public int Order { get; private set; }

    private Category() { } // EF

    public static Category Create(
        Guid id,
        Slug slug,
        string name,
        Guid? parentId = null,
        string? description = null,
        int order = 0)
    {
        return new Category
        {
            Id = id,
            Slug = slug,
            Name = name.Trim(),
            ParentId = parentId,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Order = order,
        };
    }

    /// <summary>
    /// Renames and repositions a category. The slug is deliberately not editable: it is a public URL
    /// (<c>/categories/{slug}</c>), and changing it would break links without a 301 redirect record
    /// (CT-3). Slug changes arrive with the redirects slice.
    /// </summary>
    public void Update(string name, string? description, int order)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Order = order;
    }

    /// <summary>
    /// Re-parents the category. TX-3: cycles are disallowed, so the caller supplies the prospective
    /// parent's ancestor chain (the domain cannot query) and this rejects any chain containing self.
    /// </summary>
    public Result MoveTo(Guid? newParentId, IReadOnlyCollection<Guid> newParentAncestry)
    {
        if (newParentId == Id)
            return Result.Failure(Error.Rule("A category cannot be its own parent."));

        if (newParentId is not null && newParentAncestry.Contains(Id))
            return Result.Failure(Error.Rule(
                "A category cannot be moved beneath one of its own descendants (this would create a cycle)."));

        ParentId = newParentId;
        return Result.Success();
    }
}
