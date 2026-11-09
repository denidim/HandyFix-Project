using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsQuoteOnlyToService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsQuoteOnly",
                table: "Services",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Boiler installation and servicing, and every service in Small Building, are quoted
            // after a survey rather than booked as an hourly slot with a deposit.
            migrationBuilder.Sql(@"
UPDATE s
SET s.[IsQuoteOnly] = 1
FROM [Services] s
INNER JOIN [ServiceCategories] c ON s.[CategoryId] = c.[Id]
WHERE s.[Slug] = N'boiler-installation-servicing'
   OR c.[Slug] = N'small-building-works';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsQuoteOnly",
                table: "Services");
        }
    }
}
