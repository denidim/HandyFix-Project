using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTipToPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Tip",
                table: "Payments",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tip",
                table: "Payments");
        }
    }
}
