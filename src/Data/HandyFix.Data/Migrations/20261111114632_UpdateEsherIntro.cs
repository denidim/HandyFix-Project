using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyFix.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateEsherIntro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Esher's page opened with Cobham's sentence. The seeder fills an empty table only, so
            // the live and staging row changes here (PROJECT_STATE Section 3eb).
            migrationBuilder.Sql(@"
UPDATE [ServiceAreas]
SET [IntroCopy] = N'Esher has older cottages and period houses around the High Street and Esher Green, family homes in the roads towards Hinchley Wood and Claygate, and large detached houses on its quieter roads. Older pipework and original bathrooms bring the usual dripping taps, leaking joints and blocked sinks. We are about 15 minutes away in Chessington and bring the tools and common parts to finish most jobs in one visit.'
WHERE [Slug] = N'esher';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE [ServiceAreas]
SET [IntroCopy] = N'Cobham''s mix of period cottages around the village, larger family homes along Fairmile and Downside, and the newer executive developments near Stoke D''Abernon all come with their own maintenance quirks. Esher shares much of that same character, from the streets near Sandown Park to the homes towards Claygate and West End.'
WHERE [Slug] = N'esher';");
        }
    }
}
