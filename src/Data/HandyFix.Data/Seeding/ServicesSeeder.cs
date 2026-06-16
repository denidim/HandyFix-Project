namespace HandyFix.Data.Seeding
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;

    internal class ServicesSeeder : ISeeder
    {
        public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider)
        {
            ServiceCategory plumbingCategory = dbContext.ServiceCategories.FirstOrDefault(x => x.Name == "Plumbing");
            ServiceCategory handymanCategory = dbContext.ServiceCategories.FirstOrDefault(x => x.Name == "Handyman");

            if (plumbingCategory == null || handymanCategory == null)
            {
                // Wait for CategoriesSeeder to complete, or seed categories if not present
                return;
            }

            var services = new[]
            {
                // Plumbing Services
                new { Slug = "emergency-plumbing", Name = "Emergency Plumbing", Description = "Urgent response for burst pipes, flooding, or critical plumbing failures across South London.", Price = 90.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "leak-repairs", Name = "Leak Repairs", Description = "Locating and resolving water leaks in pipes, under sinks, or behind walls quickly.", Price = 75.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "tap-repairs", Name = "Tap Repairs", Description = "Fixing dripping taps, replacing seals, cartridges, or installing brand new basin mixers.", Price = 60.00m, Duration = 45, CategoryId = plumbingCategory.Id },
                new { Slug = "toilet-repairs", Name = "Toilet Repairs", Description = "Repairing running toilets, faulty flush valves, fill mechanisms, or toilet leaks.", Price = 80.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "pipe-repairs", Name = "Pipe Repairs", Description = "Replacing damaged copper, plastic, or lead piping to restore secure water flow.", Price = 110.00m, Duration = 90, CategoryId = plumbingCategory.Id },
                new { Slug = "shower-installation", Name = "Shower Installation", Description = "Full assembly and electrical/plumbing integration of power showers and mixers.", Price = 250.00m, Duration = 180, CategoryId = plumbingCategory.Id },
                new { Slug = "bathroom-plumbing", Name = "Bathroom Plumbing", Description = "Installing baths, basins, bidets, or replacing complete bathroom sanitaryware.", Price = 150.00m, Duration = 120, CategoryId = plumbingCategory.Id },
                new { Slug = "kitchen-plumbing", Name = "Kitchen Plumbing", Description = "Connecting washing machines, dishwashers, garbage disposals, and kitchen sink units.", Price = 140.00m, Duration = 120, CategoryId = plumbingCategory.Id },
                new { Slug = "blocked-drains", Name = "Blocked Drains", Description = "Clearing blocked interior drains, sinks, showers, or baths using professional tools.", Price = 85.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "general-plumbing-maintenance", Name = "General Plumbing Maintenance", Description = "Routine checks, radiator bleeding, valve replacements, and plumbing health checks.", Price = 70.00m, Duration = 60, CategoryId = plumbingCategory.Id },

                // Handyman Services
                new { Slug = "furniture-assembly", Name = "Furniture Assembly", Description = "Professional flat pack furniture assembly for wardrobes, beds, desks, and tables.", Price = 60.00m, Duration = 90, CategoryId = handymanCategory.Id },
                new { Slug = "tv-mounting", Name = "TV Mounting", Description = "Securely mounting flat-screen TVs to drywall, concrete, or brick walls with cable tidying.", Price = 65.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "shelf-installation", Name = "Shelf Installation", Description = "Fitting floating shelves, heavy-duty brackets, or bookcase shelving systems.", Price = 45.00m, Duration = 45, CategoryId = handymanCategory.Id },
                new { Slug = "curtain-and-blind-fitting", Name = "Curtain and Blind Fitting", Description = "Hanging curtain rods, tracks, roller blinds, Venetian blinds, or vertical blinds.", Price = 50.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "door-repairs", Name = "Door Repairs", Description = "Adjusting sticking doors, replacing hinges, handles, locks, or draft excluders.", Price = 55.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "wall-mounting", Name = "Wall Mounting", Description = "Mounting pictures, heavy mirrors, whiteboards, or gallery walls with correct anchors.", Price = 45.00m, Duration = 45, CategoryId = handymanCategory.Id },
                new { Slug = "minor-electrical-tasks", Name = "Minor Electrical Tasks", Description = "Replacing light switches, power outlets, light fixtures, or fitting new smoke alarms.", Price = 70.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "minor-home-repairs", Name = "Minor Home Repairs", Description = "Addressing minor plaster cracks, squeaky floors, loose handles, or silicone sealant replacements.", Price = 50.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "painting-touch-ups", Name = "Painting Touch-Ups", Description = "Filling holes, sanding down, and repainting damaged walls, doors, or skirting boards.", Price = 100.00m, Duration = 120, CategoryId = handymanCategory.Id },
                new { Slug = "property-maintenance", Name = "Property Maintenance", Description = "Comprehensive handyman checkups, gutter clearing, lock changes, and general repairs.", Price = 180.00m, Duration = 180, CategoryId = handymanCategory.Id },
            };

            foreach (var item in services)
            {
                if (!dbContext.Services.Any(x => x.Name == item.Name))
                {
                    await dbContext.Services.AddAsync(new Service
                    {
                        Slug = item.Slug,
                        Name = item.Name,
                        Description = item.Description,
                        BasePrice = item.Price,
                        EstimatedDurationMinutes = item.Duration,
                        CategoryId = item.CategoryId,
                        IsActive = true,
                    });
                }
            }
        }
    }
}
