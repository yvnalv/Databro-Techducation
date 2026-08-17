namespace DataBro.Modules.Content.Application;

/// <summary>
/// Answers whether a slug is already taken by <b>any</b> content unit — an article or a lesson body
/// (ADR-0012).
///
/// <para>
/// This exists because option B stores each unit type in its own table, and a unique index cannot
/// span two tables. It is the single cost that a one-table discriminator would not have had, and it
/// is deliberately paid here — in one guard, on the write path — rather than as a
/// <c>kind = Article</c> predicate repeated on every read path, where forgetting one is silent and
/// public.
/// </para>
/// <para>
/// Slugs must be unique across both because both are URLs on one origin: <c>/articles/rag</c> and a
/// lesson reachable at <c>rag</c> cannot both exist without one shadowing the other, and the
/// redirect table keys on paths too.
/// </para>
/// </summary>
public interface IContentSlugRegistry
{
    /// <summary>
    /// True when the slug is in use by any content unit other than <paramref name="excluding"/>.
    /// The exclusion matters on rename: a unit must not collide with itself.
    /// </summary>
    Task<bool> IsTakenAsync(string slug, Guid? excluding = null, CancellationToken ct = default);
}
