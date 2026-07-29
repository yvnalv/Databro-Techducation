namespace DataBro.Platform.SharedKernel;

/// <summary>
/// Base class for all domain entities. Uses a GUID identity (UUID) per the project's database
/// standards. Equality is identity-based.
/// </summary>
public abstract class Entity : IAuditable, ISoftDeletable
{
    protected Entity(Guid id) => Id = id;

    // Parameterless ctor for EF Core materialization.
    protected Entity() { }

    public Guid Id { get; protected set; }

    // Audit fields (see docs/DATABASE.md — Standard Audit Fields).
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    // Soft delete — business data is never physically deleted.
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public bool IsDeleted { get; set; }

    public override bool Equals(object? obj)
        => obj is Entity other && other.GetType() == GetType() && other.Id == Id && Id != Guid.Empty;

    public override int GetHashCode() => Id.GetHashCode();
}
