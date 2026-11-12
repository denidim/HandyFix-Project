using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsEmergencyToService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEmergency",
                table: "Services",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Emergency plumbing is taken by phone and WhatsApp at any hour, at the emergency
            // rate of £150 an hour (the owner's decision of 11 Nov 2026, PROJECT_STATE Section 3eg).
            migrationBuilder.Sql(@"
UPDATE [Services]
SET [IsEmergency] = 1, [BasePrice] = 150.00
WHERE [Slug] = N'emergency-plumbing';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE [Services]
SET [BasePrice] = 90.00
WHERE [Slug] = N'emergency-plumbing';");

            migrationBuilder.DropColumn(
                name: "IsEmergency",
                table: "Services");
        }
    }
}
