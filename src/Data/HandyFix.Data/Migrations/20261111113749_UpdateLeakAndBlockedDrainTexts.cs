using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLeakAndBlockedDrainTexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // We do not find hidden leaks ("behind walls" read as leak detection, which we do not
            // offer), and we clear blockages inside the home only. The seeder fills an empty table
            // only, so the live and staging rows change here (PROJECT_STATE Section 3dz).
            migrationBuilder.Sql(@"
UPDATE [Services]
SET [Description] = N'Fixing leaking pipes, joints and fittings under sinks and around baths, toilets and radiators.'
WHERE [Slug] = N'leak-repairs';

UPDATE [Services]
SET [Description] = N'Clearing blocked sinks, toilets, baths and showers inside the home. We do not clear outside drains.'
WHERE [Slug] = N'blocked-drains';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE [Services]
SET [Description] = N'Locating and resolving water leaks in pipes, under sinks, or behind walls quickly.'
WHERE [Slug] = N'leak-repairs';

UPDATE [Services]
SET [Description] = N'Clearing blocked interior drains, sinks, showers, or baths using professional tools.'
WHERE [Slug] = N'blocked-drains';");
        }
    }
}
