using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDisplayOrderAndIsPopularToService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 100, not the generated 0: existing rows take Service.DefaultDisplayOrder, so they
            // stay in alphabetical order behind the services given a lower number below.
            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "Services",
                type: "int",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<bool>(
                name: "IsPopular",
                table: "Services",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Starting values for services already in the database, set here and not in the
            // seeder: the seeder runs on every start and would undo what an admin changes in the
            // panel, a migration runs once. A new database has no rows yet at this point and
            // gets the same values from ServicesSeeder when it inserts them.
            migrationBuilder.Sql(
                "UPDATE Services SET DisplayOrder = 0 WHERE Slug IN ('general-plumbing-maintenance', 'general-handyman-call-out')");

            migrationBuilder.Sql(
                "UPDATE Services SET IsPopular = 1 WHERE Slug IN ('full-bathroom-refurbishment', 'kitchen-fitting-alterations', 'emergency-plumbing', 'furniture-assembly')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "IsPopular",
                table: "Services");
        }
    }
}
