namespace DataBro.Platform.Abstractions;

/// <summary>
/// The public profile of a user, as other modules are allowed to see it. Deliberately minimal:
/// only what a byline needs. Anything richer belongs to Identity's own API surface.
/// </summary>
public sealed record UserSummary(Guid Id, string DisplayName, string? AvatarUrl = null);

/// <summary>
/// Read-only cross-module lookup of user profiles, owned by Identity and consumed by any module
/// that needs to attribute a record to a person (Content bylines today; Learning and Community
/// later).
/// <para>
/// This contract lives in <c>Platform</c> rather than in Identity on purpose. Modules must not
/// depend on one another (docs/ARCHITECTURE.md; enforced by
/// <c>Application_should_not_depend_on_other_modules</c>), so the shared kernel holds the interface
/// and Identity supplies the implementation through DI. Consumers never learn that Identity exists.
/// </para>
/// <para>
/// Deliberately batch-shaped: list endpoints resolve many authors at once, and a per-item interface
/// would invite N+1 lookups on the read-heavy public path. See ADR-0008.
/// </para>
/// </summary>
public interface IUserDirectory
{
    /// <summary>
    /// Resolves the given user ids. Ids with no matching user are simply absent from the result,
    /// so callers must tolerate a partial map: a deleted author must not break an article page.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, UserSummary>> GetUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken ct = default);
}
