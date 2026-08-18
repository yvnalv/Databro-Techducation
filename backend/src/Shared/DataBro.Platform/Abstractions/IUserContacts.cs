namespace DataBro.Platform.Abstractions;

/// <summary>How to reach one user. Contact data, not profile data.</summary>
public sealed record UserContact(Guid Id, string Email, string DisplayName);

/// <summary>
/// Resolves a user's contact details, owned by Identity and consumed by anything that needs to send
/// them something.
///
/// <para>
/// <b>Deliberately separate from <see cref="IUserDirectory"/>.</b> That contract is a byline — it is
/// resolved on public article pages, in bulk, on the cached read path. An email address is PII and
/// has no business travelling that route. Keeping them apart means a template that renders an author
/// card cannot accidentally have an address in hand, which is a stronger guarantee than remembering
/// not to use one.
/// </para>
/// <para>
/// Not batch-shaped, unlike the directory, and for a real reason rather than an oversight: contacting
/// is one-at-a-time by nature — an outbox message concerns one learner — whereas a listing resolves
/// twenty bylines at once. A batch API here would exist only to be misused for an export.
/// </para>
/// </summary>
public interface IUserContacts
{
    /// <summary>Null when the user no longer exists — a deleted account must not fail a job.</summary>
    Task<UserContact?> GetContactAsync(Guid userId, CancellationToken ct = default);
}
