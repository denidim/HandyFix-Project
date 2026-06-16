namespace HandyFix.Data.Seeding
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;

    internal class PaymentStatusesSeeder : ISeeder
    {
        public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider)
        {
            var statusNames = new[] { "Pending", "DepositPaid", "Confirmed", "Completed", "Cancelled", "Refunded" };

            foreach (var name in statusNames)
            {
                if (!dbContext.PaymentStatuses.Any(x => x.Name == name))
                {
                    await dbContext.PaymentStatuses.AddAsync(new PaymentStatus { Name = name });
                }
            }
        }
    }
}
