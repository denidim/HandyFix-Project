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
            ServiceCategory plumbingCategory = dbContext.ServiceCategories.FirstOrDefault(x => x.Slug == "plumbing" || x.Name == "Plumbing");
            ServiceCategory handymanCategory = dbContext.ServiceCategories.FirstOrDefault(x => x.Slug == "handyman" || x.Name == "Handyman");
            ServiceCategory smallBuildingCategory = dbContext.ServiceCategories.FirstOrDefault(x => x.Slug == "small-building-works" || x.Name == "Small Building & Refurbishments");

            if (plumbingCategory == null || handymanCategory == null || smallBuildingCategory == null)
            {
                // Wait for CategoriesSeeder to complete, or seed categories if not present
                return;
            }

            var services = new[]
            {
                // Plumbing Services
                new { Slug = "emergency-plumbing", Name = "Emergency Plumbing", Description = "Urgent response for burst pipes, flooding, or critical plumbing failures across South London.", Price = 80.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "leak-repairs", Name = "Leak Repairs", Description = "Locating and resolving water leaks in pipes, under sinks, or behind walls quickly.", Price = 80.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "tap-repairs", Name = "Tap Repairs", Description = "Fixing dripping taps, replacing seals, cartridges, or installing brand new basin mixers.", Price = 80.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "toilet-repairs", Name = "Toilet Repairs", Description = "Repairing running toilets, faulty flush valves, fill mechanisms, or toilet leaks.", Price = 80.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "pipe-repairs", Name = "Pipe Repairs", Description = "Replacing damaged copper, plastic, or lead piping to restore secure water flow.", Price = 80.00m, Duration = 90, CategoryId = plumbingCategory.Id },
                new { Slug = "shower-installation", Name = "Shower Installation", Description = "Full assembly and electrical/plumbing integration of power showers and mixers.", Price = 80.00m, Duration = 180, CategoryId = plumbingCategory.Id },
                new { Slug = "bathroom-plumbing", Name = "Bathroom Plumbing", Description = "Installing baths, basins, bidets, or replacing complete bathroom sanitaryware.", Price = 80.00m, Duration = 120, CategoryId = plumbingCategory.Id },
                new { Slug = "kitchen-plumbing", Name = "Kitchen Plumbing", Description = "Installing kitchen sinks, garbage disposals, and other kitchen plumbing fixtures and fittings.", Price = 80.00m, Duration = 120, CategoryId = plumbingCategory.Id },
                new { Slug = "blocked-drains", Name = "Blocked Drains", Description = "Clearing blocked interior drains, sinks, showers, or baths using professional tools.", Price = 80.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "general-plumbing-maintenance", Name = "General Plumbing Maintenance", Description = "Routine plumbing checks, minor valve adjustments, and general system health checks.", Price = 80.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "washing-machine-dishwasher-install", Name = "Washing Machine & Dishwasher Install", Description = "Connecting new washing machines or dishwashers to the water supply and waste, including hose and valve checks.", Price = 80.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "radiator-trv-replacement", Name = "Radiator & TRV Replacement", Description = "Replacing an old radiator or upgrading manual valves to thermostatic radiator valves (TRVs) for better heating control.", Price = 80.00m, Duration = 90, CategoryId = plumbingCategory.Id },
                new { Slug = "outside-garden-tap-installation", Name = "Outside Garden Tap Installation", Description = "Fitting an external garden tap with a double-check valve, connected to the internal water supply.", Price = 80.00m, Duration = 90, CategoryId = plumbingCategory.Id },
                new { Slug = "silicone-mastic-resealing", Name = "Silicone & Mastic Resealing", Description = "Removing old, mouldy silicone and resealing around baths, showers, and sinks to stop leaks and damp.", Price = 80.00m, Duration = 60, CategoryId = plumbingCategory.Id },
                new { Slug = "bath-shower-screen-fitting", Name = "Bath & Shower Screen Fitting", Description = "Fitting a glass bath or shower screen, including tile drilling and sealing for a watertight finish.", Price = 80.00m, Duration = 60, CategoryId = plumbingCategory.Id },

                // Handyman Services
                new { Slug = "furniture-assembly", Name = "Furniture Assembly", Description = "Professional flat pack furniture assembly for wardrobes, beds, desks, and tables.", Price = 60.00m, Duration = 90, CategoryId = handymanCategory.Id },
                new { Slug = "tv-mounting", Name = "TV Mounting", Description = "Securely mounting flat-screen TVs to drywall, concrete, or brick walls with cable tidying.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "shelf-installation", Name = "Shelf Installation", Description = "Fitting floating shelves, heavy-duty brackets, or bookcase shelving systems.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "curtain-and-blind-fitting", Name = "Curtain and Blind Fitting", Description = "Hanging curtain rods, tracks, roller blinds, Venetian blinds, or vertical blinds.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "door-repairs", Name = "Door Repairs", Description = "Adjusting sticking doors, replacing hinges, or fitting draft excluders and door closers.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "wall-mounting", Name = "Wall Mounting", Description = "Mounting pictures, heavy mirrors, whiteboards, or gallery walls with correct anchors.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "minor-electrical-tasks", Name = "Minor Electrical Tasks", Description = "Replacing light switches, power outlets, light fixtures, or fitting new smoke alarms.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "minor-home-repairs", Name = "Minor Home Repairs", Description = "Addressing minor plaster cracks, squeaky floors, loose handles, or other general small repairs.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "painting-touch-ups", Name = "Painting Touch-Ups", Description = "Filling holes, sanding down, and repainting damaged walls, doors, or skirting boards.", Price = 60.00m, Duration = 120, CategoryId = handymanCategory.Id },
                new { Slug = "property-maintenance", Name = "Property Maintenance", Description = "Comprehensive handyman checkups and general repairs across multiple areas of the property in a single visit.", Price = 60.00m, Duration = 180, CategoryId = handymanCategory.Id },
                new { Slug = "door-trimming-shaving", Name = "Door Trimming & Shaving", Description = "Trimming or easing a door that catches on new carpet or flooring so it opens and closes freely.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "lock-handle-replacement", Name = "Lock & Handle Replacement", Description = "Replacing worn or broken door locks, handles, and latches for security or a tenancy changeover.", Price = 60.00m, Duration = 60, CategoryId = handymanCategory.Id },
                new { Slug = "gutter-clearing", Name = "Gutter Clearing", Description = "Clearing leaves and debris from gutters and downpipes to prevent overflow and water damage.", Price = 60.00m, Duration = 90, CategoryId = handymanCategory.Id },

                // Small Building & Refurbishment Services
                new { Slug = "full-bathroom-refurbishment", Name = "Full Bathroom Refurbishment", Description = "Complete bathroom renovation including removal of old suite, plumbing, wall and floor tiling, sanitaryware fitting, and electrical/lighting prep.", Price = 650.00m, Duration = 480, CategoryId = smallBuildingCategory.Id },
                new { Slug = "kitchen-fitting-alterations", Name = "Kitchen Fitting & Alterations", Description = "Fitting kitchen units, worktops, sink plumbing, splashbacks, and integrated appliances for partial or complete kitchen renovations.", Price = 650.00m, Duration = 480, CategoryId = smallBuildingCategory.Id },
                new { Slug = "partition-walls-drylining", Name = "Partition Walls & Stud Work", Description = "Constructing timber or metal stud partition walls, acoustic insulation, plasterboarding, and room reconfigurations for home offices or layouts.", Price = 320.00m, Duration = 480, CategoryId = smallBuildingCategory.Id },
                new { Slug = "plastering-ceiling-repairs", Name = "Plastering & Ceiling Repairs", Description = "Full room skimming, plaster patching, ceiling boarding after water damage, and coving to create clean, smooth surfaces ready for painting.", Price = 280.00m, Duration = 480, CategoryId = smallBuildingCategory.Id },
                new { Slug = "wall-floor-tiling", Name = "Wall & Floor Tiling", Description = "Professional tiling for kitchens, bathrooms, and hallways using ceramic, porcelain, natural stone, or metro tiles with clean grouting and sealing.", Price = 280.00m, Duration = 480, CategoryId = smallBuildingCategory.Id },
                new { Slug = "flooring-installation", Name = "Flooring Installation", Description = "Supplying and fitting laminate, luxury vinyl tiles (LVT), and engineered wood flooring, complete with underlay, beading, and threshold trims.", Price = 280.00m, Duration = 480, CategoryId = smallBuildingCategory.Id },
                new { Slug = "external-brickwork-paving", Name = "External Brickwork & Paving", Description = "Mortar repointing, garden wall repairs, patio relaying, outdoor step rebuilds, and concrete shed bases built on a manageable domestic scale.", Price = 320.00m, Duration = 480, CategoryId = smallBuildingCategory.Id },
                new { Slug = "custom-carpentry-boxing-in", Name = "Custom Carpentry & Boxing-In", Description = "Bespoke alcove cupboards, floating shelving units, pipework boxing-in, and tailored interior woodwork crafted to suit your room dimensions.", Price = 280.00m, Duration = 480, CategoryId = smallBuildingCategory.Id },
            };

            foreach (var item in services)
            {
                Service service = dbContext.Services.FirstOrDefault(x => x.Name == item.Name);
                if (service == null)
                {
                    service = new Service
                    {
                        Slug = item.Slug,
                        Name = item.Name,
                        Description = item.Description,
                        BasePrice = item.Price,
                        EstimatedDurationMinutes = item.Duration,
                        CategoryId = item.CategoryId,
                        IsActive = true,
                    };

                    await dbContext.Services.AddAsync(service);
                }
                else
                {
                    service.BasePrice = item.Price;
                    service.EstimatedDurationMinutes = item.Duration;
                }

                await dbContext.SaveChangesAsync();
            }

            // Sync ServiceImage entities for any service missing a ServiceImage record
            var allServices = dbContext.Services.ToList();
            foreach (Service service in allServices)
            {
                if (!dbContext.ServiceImages.Any(x => x.ServiceId == service.Id))
                {
                    await dbContext.ServiceImages.AddAsync(new ServiceImage
                    {
                        ServiceId = service.Id,
                        ImageUrl = $"/images/services/{service.Slug}-hero.webp",
                    });
                }
            }
        }
    }
}
