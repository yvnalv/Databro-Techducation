using DataBro.Platform.Abstractions;
using DataBro.Platform.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DataBro.Platform.Persistence.Auditing;

/// <summary>
/// Populates the standard audit fields on save and converts hard deletes of soft-deletable
/// entities into soft deletes. See docs/DATABASE.md — Standard Audit Fields, and rule XC-1.
/// </summary>
public sealed class AuditingInterceptor(IClock clock, ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            Apply(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
            Apply(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    private void Apply(DbContext context)
    {
        var now = clock.UtcNow;
        var userId = currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries<Entity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;

                case EntityState.Deleted when entry.Entity is ISoftDeletable:
                    SoftDelete(entry, now, userId);
                    break;
            }
        }
    }

    private static void SoftDelete(EntityEntry<Entity> entry, DateTimeOffset now, Guid? userId)
    {
        entry.State = EntityState.Modified;
        var entity = (ISoftDeletable)entry.Entity;
        entity.IsDeleted = true;
        entity.DeletedAt = now;
        entity.DeletedBy = userId;
        entry.Entity.UpdatedAt = now;
        entry.Entity.UpdatedBy = userId;
    }
}
