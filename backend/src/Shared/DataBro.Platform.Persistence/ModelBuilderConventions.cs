using System.Linq.Expressions;
using DataBro.Platform.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace DataBro.Platform.Persistence;

/// <summary>Shared EF Core model conventions applied by every module's DbContext.</summary>
public static class ModelBuilderConventions
{
    /// <summary>
    /// Adds a global query filter (`IsDeleted = false`) to every soft-deletable entity, so deleted
    /// rows are hidden by default. Bypass only in reviewed admin paths (docs/DATABASE.md).
    /// </summary>
    public static ModelBuilder ApplySoftDeleteQueryFilter(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var isDeleted = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var notDeleted = Expression.Equal(isDeleted, Expression.Constant(false));
            var filter = Expression.Lambda(notDeleted, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }

        return modelBuilder;
    }

    /// <summary>
    /// Declares every entity's GUID key as client-generated (<c>ValueGeneratedNever</c>). Domain
    /// aggregates assign their own ids (docs/DATABASE.md); this prevents EF's "key is set ⇒ existing
    /// row" heuristic from marking newly-created children of a tracked aggregate as Modified.
    /// </summary>
    public static ModelBuilder ApplyClientGeneratedKeys(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(Entity).IsAssignableFrom(entityType.ClrType))
                continue;

            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(Entity.Id))
                .ValueGeneratedNever();
        }

        return modelBuilder;
    }
}
