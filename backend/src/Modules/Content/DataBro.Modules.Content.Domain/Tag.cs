using DataBro.Platform.SharedKernel;

namespace DataBro.Modules.Content.Domain;

/// <summary>
/// A flat content tag (docs/BUSINESS_RULES.md TX-1). An article carries any number of tags (CT-11).
///
/// <para>
/// Uniqueness is per type, so a tag slug may coincide with a category slug — <c>/tags/python</c> and
/// <c>/categories/python</c> are different pages by design.
/// </para>
/// </summary>
public sealed class Tag : AggregateRoot
{
    public Slug Slug { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;

    private Tag() { } // EF

    public static Tag Create(Guid id, Slug slug, string name) =>
        new() { Id = id, Slug = slug, Name = name.Trim() };

    /// <summary>
    /// Renames the tag. As with <see cref="Category"/>, the slug is immutable: it is a public URL and
    /// changing it needs a 301 redirect record (CT-3), which arrives with the redirects slice.
    /// </summary>
    public void Rename(string name) => Name = name.Trim();
}
