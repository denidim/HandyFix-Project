namespace HandyFix.Data.Seeding
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;

    internal class ServiceCategoriesSeeder : ISeeder
    {
        public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider)
        {
            var categories = new[]
            {
                new { Slug = "plumbing", Name = "Plumbing", Description = "Professional plumbing and heating services for London homes." },
                new { Slug = "handyman", Name = "Handyman", Description = "Reliable home maintenance, mounting, and repair tasks." },
            };

            foreach (var item in categories)
            {
                if (!dbContext.ServiceCategories.Any(x => x.Name == item.Name))
                {
                    await dbContext.ServiceCategories.AddAsync(new ServiceCategory
                    {
                        Slug = item.Slug,
                        Name = item.Name,
                        Description = item.Description,
                    });
                }
            }
        }
    }
}
