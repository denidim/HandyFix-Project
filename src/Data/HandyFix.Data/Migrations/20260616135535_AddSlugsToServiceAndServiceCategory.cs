using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSlugsToServiceAndServiceCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Services",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "ServiceCategories",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Services_Slug",
                table: "Services",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCategories_Slug",
                table: "ServiceCategories",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Services_Slug",
                table: "Services");

            migrationBuilder.DropIndex(
                name: "IX_ServiceCategories_Slug",
                table: "ServiceCategories");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "ServiceCategories");
        }
    }
}
