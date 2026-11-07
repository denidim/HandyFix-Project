using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHeroImageUrlToServiceArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HeroImageUrl",
                table: "ServiceAreas",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeroImageUrl",
                table: "ServiceAreas");
        }
    }
}
