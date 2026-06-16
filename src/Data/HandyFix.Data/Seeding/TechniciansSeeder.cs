namespace HandyFix.Data.Seeding
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using HandyFix.Data.Models;

    internal class TechniciansSeeder : ISeeder
    {
        public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider)
        {
            if (!dbContext.Technicians.Any())
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
