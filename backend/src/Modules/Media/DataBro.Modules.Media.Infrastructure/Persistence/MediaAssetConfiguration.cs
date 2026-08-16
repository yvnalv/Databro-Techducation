using DataBro.Modules.Media.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataBro.Modules.Media.Infrastructure.Persistence;

internal sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        builder.ToTable("media_assets");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.StorageKey).HasMaxLength(500).IsRequired();
        // Filtered for the same reason as the variant index below: a soft-deleted asset keeps its
        // key. Collision is near-impossible since keys carry the asset's GUID, but an unfiltered
        // unique index over soft-deleted rows is a trap worth not leaving lying around.
        builder.HasIndex(a => a.StorageKey).IsUnique().HasFilter("is_deleted = false");

        builder.Property(a => a.FileName).HasMaxLength(MediaLimits.MaxFileNameLength).IsRequired();
        builder.Property(a => a.MimeType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.AltText).HasMaxLength(MediaLimits.MaxAltTextLength).IsRequired();
        builder.Property(a => a.Checksum).HasMaxLength(64).IsRequired();
        builder.Property(a => a.ProcessingError).HasMaxLength(1000);

        builder.Property(a => a.ProcessingStatus).HasConversion<string>().HasMaxLength(20);

        builder.Property(a => a.ByteSize);
        builder.Property(a => a.Width);
        builder.Property(a => a.Height);
        builder.Property(a => a.UploadedBy);

        // The picker lists newest first, and the variant job sweeps by status.
        builder.HasIndex(a => a.CreatedAt);
        builder.HasIndex(a => a.ProcessingStatus);

        // Deliberately not unique: two articles legitimately use the same image, and uploading it
        // twice is a duplicate to report in the picker, not an error to refuse at the database.
        builder.HasIndex(a => a.Checksum);

        builder.HasMany(a => a.Variants)
            .WithOne()
            .HasForeignKey(v => v.MediaAssetId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(a => a.Variants)
            .HasField("_variants")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(a => a.DomainEvents);
    }
}

internal sealed class MediaVariantConfiguration : IEntityTypeConfiguration<MediaVariant>
{
    public void Configure(EntityTypeBuilder<MediaVariant> builder)
    {
        builder.ToTable("media_variants");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Name).HasMaxLength(30).IsRequired();
        builder.Property(v => v.StorageKey).HasMaxLength(500).IsRequired();
        builder.Property(v => v.Width);
        builder.Property(v => v.Height);
        builder.Property(v => v.ByteSize);

        // One live variant per width per asset. Filtered on `is_deleted` because deletes here are
        // soft: without the filter, a variant that was removed and later regenerated would collide
        // with its own tombstone.
        builder.HasIndex(v => new { v.MediaAssetId, v.Name })
            .IsUnique()
            .HasFilter("is_deleted = false");
    }
}
