using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDiagramPlaceToServiceArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DiagramBearing",
                table: "ServiceAreas",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiagramLabel",
                table: "ServiceAreas",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiagramLabelSide",
                table: "ServiceAreas",
                type: "int",
                nullable: true);

            // The places that were written into _AreaCoverageMap.cshtml until now, moved here
            // unchanged so the diagram looks exactly as before. A new database has no rows yet at
            // this point and gets the same values from ServiceAreasSeeder. An area whose slug the
            // admin has changed, or one the admin added, is left empty and is placed from its
            // postcode the next time it is saved. Sides: 1 right, 2 left, 3 above, 4 below.
            var placesBySlug = new (string Slug, int Bearing, int Side, string Label)[]
            {
                ("kingston-upon-thames", 10, 1, "Kingston"),
                ("wimbledon", 42, 1, null),
                ("worcester-park-ewell", 61, 1, "Worcester Park"),
                ("sutton", 88, 1, null),
                ("banstead", 122, 1, null),
                ("epsom", 138, 2, null),
                ("reigate", 154, 1, null),
                ("dorking", 189, 4, null),
                ("leatherhead", 196, 2, null),
                ("guildford", 234, 2, null),
                ("cobham", 247, 2, null),
                ("esher", 284, 2, null),
                ("walton-on-thames-weybridge", 290, 2, "Walton & Weybridge"),
                ("surbiton", 345, 3, null),
            };

            foreach ((string slug, int bearing, int side, string label) in placesBySlug)
            {
                string labelSql = label == null ? "NULL" : $"N'{label}'";
                migrationBuilder.Sql(
                    $"UPDATE ServiceAreas SET DiagramBearing = {bearing}, DiagramLabelSide = {side}, DiagramLabel = {labelSql} WHERE Slug = '{slug}'");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiagramBearing",
                table: "ServiceAreas");

            migrationBuilder.DropColumn(
                name: "DiagramLabel",
                table: "ServiceAreas");

            migrationBuilder.DropColumn(
                name: "DiagramLabelSide",
                table: "ServiceAreas");
        }
    }
}
