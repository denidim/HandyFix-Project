using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTechnicianFromAvailabilitySlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AvailabilitySlots_Technicians_TechnicianId",
                table: "AvailabilitySlots");

            migrationBuilder.DropIndex(
                name: "IX_AvailabilitySlots_TechnicianId",
                table: "AvailabilitySlots");

            migrationBuilder.DropColumn(
                name: "TechnicianId",
                table: "AvailabilitySlots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TechnicianId",
                table: "AvailabilitySlots",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilitySlots_TechnicianId",
                table: "AvailabilitySlots",
                column: "TechnicianId");

            migrationBuilder.AddForeignKey(
                name: "FK_AvailabilitySlots_Technicians_TechnicianId",
                table: "AvailabilitySlots",
                column: "TechnicianId",
                principalTable: "Technicians",
                principalColumn: "Id");
        }
    }
}
