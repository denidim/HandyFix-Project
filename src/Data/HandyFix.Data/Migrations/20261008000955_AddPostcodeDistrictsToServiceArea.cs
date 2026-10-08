using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPostcodeDistrictsToServiceArea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PostcodeDistricts",
                table: "ServiceAreas",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // Starting lists for the areas already in the database, set here and not in the
            // seeder: the seeder only fills an empty table. A new database has no rows yet at this
            // point and gets the same lists from ServiceAreasSeeder when it inserts them. An area
            // an admin has renamed the slug of, or added, is left with no list, to be filled in
            // on its admin page.
            var districtsBySlug = new (string Slug, string Districts)[]
            {
                ("chessington", "KT9"),
                ("surbiton", "KT5, KT6, KT7"),
                ("kingston-upon-thames", "KT1, KT2, KT3"),
                ("worcester-park-ewell", "KT4, KT17, KT19"),
                ("epsom", "KT17, KT18, KT19, KT21"),
                ("sutton", "SM1, SM2, SM3, SM5, SM6"),
                ("banstead", "SM7, KT20"),
                ("esher", "KT8, KT10"),
                ("leatherhead", "KT22, KT23"),
                ("wimbledon", "SW19, SW20, SM4"),
                ("cobham", "KT11"),
                ("walton-on-thames-weybridge", "KT12, KT13"),
                ("reigate", "RH1, RH2"),
                ("dorking", "RH4, RH5"),
                ("guildford", "GU1, GU2, GU4"),
            };

            foreach ((string slug, string districts) in districtsBySlug)
            {
                migrationBuilder.Sql(
                    $"UPDATE ServiceAreas SET PostcodeDistricts = '{districts}' WHERE Slug = '{slug}'");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PostcodeDistricts",
                table: "ServiceAreas");
        }
    }
}
