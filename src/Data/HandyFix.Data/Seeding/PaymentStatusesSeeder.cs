namespace HandyFix.Data.Seeding
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data.Models;

    internal class PaymentStatusesSeeder : ISeeder
    {
        public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider)
        {
            var statusNames = new[]
            {
                PaymentStatusNames.Pending,
                PaymentStatusNames.DepositPaid,
                PaymentStatusNames.Confirmed,
                PaymentStatusNames.Completed,
                PaymentStatusNames.Cancelled,
                PaymentStatusNames.Refunded,
            };

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
