using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Server.ServerSetupApp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Migrations.Routines;

/// <summary>
/// Clean up duplicate BaseItemImageInfo entries caused by scanning bugs.
/// Keeps the oldest entry for each (ItemId, ImageType, Path) combination.
/// </summary>
#pragma warning disable CS0618 // Type or member is obsolete
[JellyfinMigration("2025-10-22T02:00:00", nameof(CleanupDuplicateBaseItemImageInfo), "B1C2D3E4-5F6A-7B8C-9D0E-1F2A3B4C5D6E")]
[JellyfinMigrationBackup(JellyfinDb = true)]
internal class CleanupDuplicateBaseItemImageInfo : IAsyncMigrationRoutine
#pragma warning restore CS0618 // Type or member is obsolete
{
    private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;
    private readonly IStartupLogger<CleanupDuplicateBaseItemImageInfo> _logger;

    public CleanupDuplicateBaseItemImageInfo(
        IDbContextFactory<JellyfinDbContext> dbProvider,
        IStartupLogger<CleanupDuplicateBaseItemImageInfo> logger)
    {
        _dbProvider = dbProvider;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task PerformAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting cleanup of duplicate BaseItemImageInfo entries");

        using var context = _dbProvider.CreateDbContext();

        // Find items with duplicate image paths (case-insensitive to catch all duplicates)
        // Load all images into memory first to avoid ToUpperInvariant() translation issues
        var allImages = await context.BaseItemImageInfos
            .Select(i => new { i.ItemId, i.ImageType, i.Path })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var itemsWithDuplicates = allImages
            .GroupBy(i => new { i.ItemId, i.ImageType, Path = i.Path.ToUpperInvariant() })
            .Where(g => g.Count() > 1)
            .Select(g => g.Key.ItemId)
            .Distinct()
            .ToList();

        if (itemsWithDuplicates.Count == 0)
        {
            _logger.LogInformation("No duplicate image entries found, skipping cleanup");
            return;
        }

        _logger.LogInformation("Found {Count} items with duplicate image entries", itemsWithDuplicates.Count);

        const int batchSize = 500;
        int processedCount = 0;
        int removedCount = 0;
        int errorCount = 0;
        var sw = Stopwatch.StartNew();

        // Process items in batches
        for (int i = 0; i < itemsWithDuplicates.Count; i += batchSize)
        {
            var batch = itemsWithDuplicates.Skip(i).Take(batchSize).ToList();

            _logger.LogInformation(
                "Processing batch {BatchNumber} - ({ProcessedSoFar}/{TotalRecords}) - Time: {Time}",
                (i / batchSize) + 1,
                Math.Min(i + batchSize, itemsWithDuplicates.Count),
                itemsWithDuplicates.Count,
                sw.Elapsed);

            foreach (var itemId in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Load all images for this item
                    var images = await context.BaseItemImageInfos
                        .Where(i => i.ItemId.Equals(itemId))
                        .ToListAsync(cancellationToken)
                        .ConfigureAwait(false);

                    // Group by (ImageType, Path) to find duplicates
                    var duplicateGroups = images
                        .GroupBy(i => (i.ImageType, Path: i.Path.ToUpperInvariant()))
                        .Where(g => g.Count() > 1);

                    // For each duplicate group, keep the oldest entry and remove others
                    foreach (var group in duplicateGroups)
                    {
                        // Treat null DateModified as oldest (DateTime.MinValue) to preserve entries with unknown dates
                        var imagesToKeep = group
                            .OrderBy(i => i.DateModified ?? DateTime.MinValue)
                            .First();

                        // Log when null DateModified values are encountered
                        if (group.Any(i => i.DateModified == null))
                        {
                            _logger.LogDebug("Null DateModified found during duplicate cleanup for item {ItemId}", itemId);
                        }

                        var imagesToRemove = group
                            .Where(i => !i.Id.Equals(imagesToKeep.Id))
                            .ToList();

                        if (imagesToRemove.Count > 0)
                        {
                            context.BaseItemImageInfos.RemoveRange(imagesToRemove);
                            removedCount += imagesToRemove.Count;
                        }
                    }

                    processedCount++;

                    // Save changes every batch
                    if (processedCount % batchSize == 0)
                    {
                        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                        context.ChangeTracker.Clear();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cleaning duplicates for item {ItemId}", itemId);
                    errorCount++;
                    context.ChangeTracker.Clear();
                }
            }
        }

        // Save any remaining changes
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            context.ChangeTracker.Clear();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving final batch");
        }

        _logger.LogInformation(
            "Duplicate cleanup completed. Processed: {Processed}, Removed: {Removed}, Errors: {Errors}",
            processedCount,
            removedCount,
            errorCount);
    }
}
