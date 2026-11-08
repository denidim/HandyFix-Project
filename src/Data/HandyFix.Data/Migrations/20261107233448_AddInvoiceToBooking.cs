using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceToBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InvoiceNumber",
                table: "Bookings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceWorkDone",
                table: "Bookings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvoicedOn",
                table: "Bookings",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_InvoiceNumber",
                table: "Bookings",
                column: "InvoiceNumber",
                unique: true,
                filter: "[InvoiceNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bookings_InvoiceNumber",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "InvoiceWorkDone",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "InvoicedOn",
                table: "Bookings");
        }
    }
}
