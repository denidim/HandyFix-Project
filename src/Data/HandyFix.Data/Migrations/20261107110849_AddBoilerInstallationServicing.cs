using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBoilerInstallationServicing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGasSafeWork",
                table: "Services",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // The boiler service, for a database that already has services: ServicesSeeder fills
            // only an empty table (PROJECT_STATE Section 3cb). A new database has no categories
            // yet at this point, so nothing is added here, and the seeder adds the same service
            // with the rest of the list. Nothing is added either where the slug is already taken,
            // by a live or a deleted row. Its picture row is added at the next start by the
            // seeder, which gives every service without one its built-in picture.
            const string description =
                "We fit new boilers and service existing ones. All boiler work is carried out by a Gas Safe registered engineer, as UK law requires for any work on gas appliances.\n\n" +
                "A new boiler starts with a survey. We look at how much heating and hot water your home needs, check the existing pipework and flue, and help you choose the right type of boiler: combi, system or conventional.\n\n" +
                "On installation day we turn off the water, drain the system and remove the old boiler, along with any tanks or cylinders you no longer need. The new boiler is mounted level and secure, and the flue is fitted and sealed through the wall. We flush the system to clear out sludge, fit a magnetic filter, and connect the flow, return, gas and condensate pipes. Finally we test everything, carry out the gas safety checks, set up your controls, including smart thermostats, and hand over your compliance and warranty certificates.\n\n" +
                "A like-for-like swap usually takes a day. Changing a regular boiler to a combi, or moving the boiler to a new place, takes 1.5 to 4 days. You get a clear quote with the price and timings before any work starts.\n\n" +
                "A yearly service keeps your boiler safe and running efficiently, and most manufacturers need it to keep the warranty valid. We check that the boiler burns gas safely, test the flue and pressure, clean the key parts and give you a record of the service.";

            migrationBuilder.Sql($@"
IF NOT EXISTS (SELECT 1 FROM [Services] WHERE [Slug] = N'boiler-installation-servicing')
INSERT INTO [Services]
    ([Id], [Slug], [Name], [Description], [BasePrice], [EstimatedDurationMinutes], [IsActive],
     [DisplayOrder], [IsPopular], [IsGasSafeWork], [CategoryId], [CreatedOn], [IsDeleted])
SELECT NEWID(), N'boiler-installation-servicing', N'Boiler Installation & Servicing', N'{description}',
       90.00, 60, 1, 100, 0, 1, c.[Id], SYSUTCDATETIME(), 0
FROM [ServiceCategories] c
WHERE c.[Slug] = N'plumbing';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The boiler service goes again unless a booking or a slot has used it.
            migrationBuilder.Sql(@"
DECLARE @id uniqueidentifier = (SELECT [Id] FROM [Services] WHERE [Slug] = N'boiler-installation-servicing');
IF @id IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM [BookingServices] WHERE [ServiceId] = @id)
   AND NOT EXISTS (SELECT 1 FROM [AvailabilitySlots] WHERE [ServiceId] = @id)
BEGIN
    DELETE FROM [ServiceImages] WHERE [ServiceId] = @id;
    DELETE FROM [Services] WHERE [Id] = @id;
END");

            migrationBuilder.DropColumn(
                name: "IsGasSafeWork",
                table: "Services");
        }
    }
}
