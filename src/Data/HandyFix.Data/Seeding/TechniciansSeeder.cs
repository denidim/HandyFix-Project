namespace HandyFix.Data.Seeding
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using HandyFix.Data.Models;

    using Microsoft.EntityFrameworkCore;

    internal class TechniciansSeeder : ISeeder
    {
        public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider)
        {
            // Deleted rows count: with only the live ones counted, deleting the placeholder
            // before adding a real technician brought the placeholder back at the next start.
            if (!dbContext.Technicians.IgnoreQueryFilters().Any())
            {
                await dbContext.Technicians.AddAsync(new Technician
                {
                    FirstName = "John",
                    LastName = "Doe",
                    PhoneNumber = "07123456789",
                    IsActive = true,
                });
            }
        }
    }
}
