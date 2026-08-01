using DataBro.Modules.Content.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataBro.Modules.Content.Infrastructure.Persistence;

internal sealed class RedirectConfiguration : IEntityTypeConfiguration<Redirect>
{
    public void Configure(EntityTypeBuilder<Redirect> builder)
    {
        builder.ToTable("redirects");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.FromPath).HasColumnName("from_path").HasMaxLength(2048).IsRequired();
        builder.Property(r => r.ToPath).HasColumnName("to_path").HasMaxLength(2048).IsRequired();
        builder.Property(r => r.StatusCode).HasDefaultValue(301);
        builder.Property(r => r.Reason).HasMaxLength(200);

        // One live destination per source path. Filtered so a path that is redirected away, freed
        // (redirect soft-deleted), then moved again can be redirected once more without colliding
        // with the tombstone row.
        builder.HasIndex(r => r.FromPath)
            .IsUnique()
            .HasFilter("is_deleted = false");

        // Chain-collapse looks up redirects by their destination.
        builder.HasIndex(r => r.ToPath);
    }
}
