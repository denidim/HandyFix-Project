namespace HandyFix.Data.Seeding
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data.Models;

    using Microsoft.EntityFrameworkCore;

    internal class TechniciansSeeder : ISeeder
    {
        public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider)
        {
            // A new database starts with the one technician the business launches with, on the
            // business's own number, so the live site never holds a made-up person a customer
            // could be sent (PROJECT_STATE.md Section 3cd). Anyone else is added in the admin
            // panel: this runs for an empty table only.
            //
            // Deleted rows count: with only the live ones counted, deleting this technician
            // before adding another brought the deleted one back at the next start.
            if (!dbContext.Technicians.IgnoreQueryFilters().Any())
            {
                await dbContext.Technicians.AddAsync(new Technician
                {
                    FirstName = "Zapryan",
                    PhoneNumber = GlobalConstants.BusinessPhone,
                    IsActive = true,
                });
            }
        }
    }
}
