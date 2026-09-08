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
                new { Slug = "small-building-works", Name = "Small Building & Refurbishments", Description = "Planned home alterations, kitchen & bathroom refurbishments, partition walls, and specialist small building works." },
            };

            foreach (var item in categories)
            {
                var category = dbContext.ServiceCategories.FirstOrDefault(x => x.Slug == item.Slug || x.Name == item.Name);
                if (category == null)
                {
                    await dbContext.ServiceCategories.AddAsync(new ServiceCategory
                    {
                        Slug = item.Slug,
                        Name = item.Name,
                        Description = item.Description,
                    });
                }
                else
                {
                    category.Name = item.Name;
                    category.Description = item.Description;
                    category.Slug = item.Slug;
                }
            }
        }
    }
}
