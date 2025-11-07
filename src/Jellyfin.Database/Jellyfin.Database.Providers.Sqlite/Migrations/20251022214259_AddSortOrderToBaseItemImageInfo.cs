using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jellyfin.Server.Implementations.Migrations
{
    /// <summary>
    /// Adds SortOrder column and creates temporary 2-column index.
    /// The app migration PopulateImageSortOrder will create the final 3-column index
    /// (ItemId, ImageType, SortOrder) after populating SortOrder values to avoid
    /// index update overhead during backfill.
    /// </summary>
    public partial class AddSortOrderToBaseItemImageInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BaseItemImageInfos_ItemId",
                table: "BaseItemImageInfos");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "BaseItemImageInfos",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_BaseItemImageInfos_ItemId_ImageType",
                table: "BaseItemImageInfos",
                columns: new[] { "ItemId", "ImageType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BaseItemImageInfos_ItemId_ImageType",
                table: "BaseItemImageInfos");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "BaseItemImageInfos");

            migrationBuilder.CreateIndex(
                name: "IX_BaseItemImageInfos_ItemId",
                table: "BaseItemImageInfos",
                column: "ItemId");
        }
    }
}
