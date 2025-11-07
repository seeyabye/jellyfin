using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Server.Implementations.Migrations
{
    /// <summary>
    /// Adds SortOrder column while keeping the existing single-column index on ItemId.
    /// The app migration PopulateImageSortOrder will create the final 3-column index
    /// (ItemId, ImageType, SortOrder) after populating SortOrder values to avoid
    /// index update overhead during backfill.
    /// </summary>
    public partial class AddSortOrderToBaseItemImageInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keep existing single-column index on ItemId for backfill performance
            // The final 3-column index will be created after PopulateImageSortOrder

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "BaseItemImageInfos",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "BaseItemImageInfos");
        }
    }
}
