namespace DataBro.Platform.Abstractions;

/// <summary>
/// Ambient accessor for the authenticated user. Populated from the JWT once Identity lands;
/// used by the auditing interceptor to stamp CreatedBy/UpdatedBy.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAuthenticated => UserId is not null;
}
