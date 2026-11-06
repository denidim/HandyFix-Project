namespace HandyFix.Data.Seeding
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data.Models;

    internal class BookingStatusesSeeder : ISeeder
    {
        public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider)
        {
            var statusNames = new[]
            {
                BookingStatusNames.Pending,
                BookingStatusNames.Approved,
                BookingStatusNames.InProgress,
                BookingStatusNames.Completed,
                BookingStatusNames.Cancelled,
                BookingStatusNames.Abandoned,
            };

            foreach (var name in statusNames)
            {
                if (!dbContext.BookingStatuses.Any(x => x.Name == name))
                {
                    await dbContext.BookingStatuses.AddAsync(new BookingStatus { Name = name });
                }
            }
        }
    }
}
