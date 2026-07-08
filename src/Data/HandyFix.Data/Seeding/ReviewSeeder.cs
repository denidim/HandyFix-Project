namespace HandyFix.Data.Seeding
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;

    internal class ReviewSeeder : ISeeder
    {
        public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider)
        {
            if (dbContext.Reviews.Any())
            {
                return;
            }

            var review = new[]
            {
                new { CustomerName = "Alice Johnson", Comment = "The product quality is absolutely outstanding and exceeded my expectations.", Rating = 5, IsApproved = true },
                new { CustomerName = "Mark Thompson", Comment = "Good overall experience, though delivery took a little longer than expected.", Rating = 4, IsApproved = true },
                new { CustomerName = "Sarah Williams", Comment = "Decent item for the price, fits the description perfectly.", Rating = 3, IsApproved = true },
                new { CustomerName = "David Chen", Comment = "Fantastic service! Will definitely be purchasing again in the near future.", Rating = 5, IsApproved = true },
                new { CustomerName = "Elena Rodriguez", Comment = "Solid build quality and very easy to set up. Highly recommended to others.", Rating = 4, IsApproved = true },
            };

            foreach (var item in review)
            {
                await dbContext.Reviews.AddAsync(new Review
                {
                    CustomerName = item.CustomerName,
                    Comment = item.Comment,
                    Rating = item.Rating,
                    IsApproved = item.IsApproved,
                });
            }
        }
    }
}
