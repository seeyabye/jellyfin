using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jellyfin.Database.Implementations.ModelConfiguration;

/// <summary>
/// Configuration for BaseItemImageInfo.
/// </summary>
public class BaseItemImageInfoConfiguration : IEntityTypeConfiguration<BaseItemImageInfo>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<BaseItemImageInfo> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasOne(e => e.Item).WithMany(e => e.Images).HasForeignKey(e => e.ItemId);

        // Unique index to prevent duplicate image entries
        builder.HasIndex(e => new { e.ItemId, e.ImageType, e.Path }).IsUnique();

        // Performance index for ordered image retrieval
        // Created by PopulateImageSortOrder migration after SortOrder values are populated
        builder.HasIndex(e => new { e.ItemId, e.ImageType, e.SortOrder });
    }
}
