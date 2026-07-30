namespace HandyFix.Data.Seeding
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;

    using HandyFix.Data.Models;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    internal class AdminUserSeeder : ISeeder
    {
        // Only ever used when ASPNETCORE_ENVIRONMENT is Development and Admin:SeedPassword
        // is not set — GetSeedPassword throws in every other environment instead.
        private const string DevelopmentOnlyFallbackPassword = "Admin123!";

        public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var adminEmail = "admin@handyfix.co.uk";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);

            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FirstName = "Admin",
                    LastName = "User",
                };

                var seedPassword = GetSeedPassword(serviceProvider);

                var result = await userManager.CreateAsync(adminUser, seedPassword);
                if (!result.Succeeded)
                {
                    throw new Exception(string.Join(Environment.NewLine, result.Errors.Select(e => e.Description)));
                }
            }

            if (!await userManager.IsInRoleAsync(adminUser, GlobalConstants.AdministratorRoleName))
            {
                var result = await userManager.AddToRoleAsync(adminUser, GlobalConstants.AdministratorRoleName);
                if (!result.Succeeded)
                {
                    throw new Exception(string.Join(Environment.NewLine, result.Errors.Select(e => e.Description)));
                }
            }
        }

        private static string GetSeedPassword(IServiceProvider serviceProvider)
        {
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var seedPassword = configuration["Admin:SeedPassword"];
            if (!string.IsNullOrWhiteSpace(seedPassword))
            {
                return seedPassword;
            }

            // HandyFix.Data is a plain class library with no ASP.NET Core hosting reference
            // (by design — see Clean Architecture layering in PROJECT_STATE.md §1), so this
            // reads the same environment variable IWebHostEnvironment.IsDevelopment() checks,
            // rather than pulling a hosting-abstractions dependency into the data layer.
            var isDevelopment = string.Equals(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                "Development",
                StringComparison.OrdinalIgnoreCase);
            if (!isDevelopment)
            {
                throw new InvalidOperationException(
                    "Admin:SeedPassword is not configured. Set it (User Secrets locally, an environment variable in staging/production) before the app can seed an administrator account outside Development.");
            }

            serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AdminUserSeeder))
                .LogWarning("Admin:SeedPassword is not configured — seeding the Development-only fallback admin password. Set Admin:SeedPassword via 'dotnet user-secrets set' to use your own.");

            return DevelopmentOnlyFallbackPassword;
        }
    }
}
