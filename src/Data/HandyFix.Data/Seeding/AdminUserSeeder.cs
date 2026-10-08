namespace HandyFix.Data.Seeding
{
    using System;
    using System.Collections.Generic;
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
        public const string SeedEmailKey = "Admin:SeedEmail";

        public const string SeedPasswordKey = "Admin:SeedPassword";

        // Both only ever used when ASPNETCORE_ENVIRONMENT is Development and the setting is not
        // set. ReadSetting throws in every other environment instead.
        private const string DevelopmentOnlyFallbackEmail = "admin@handyfix.co.uk";

        private const string DevelopmentOnlyFallbackPassword = "DevAdmin123!";

        public async Task SeedAsync(ApplicationDbContext dbContext, IServiceProvider serviceProvider)
        {
            UserManager<ApplicationUser> userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // An administrator, under whatever login email, means there is nothing to seed. The
            // login email can be changed in the admin panel, so looking for one fixed address
            // here would make a second administrator at the first start after such a change. It
            // also means the two settings below are read once in a database's life, at the start
            // that finds no administrator; changing either afterwards changes nothing
            // (PROJECT_STATE.md Section 3cc).
            IList<ApplicationUser> administrators = await userManager.GetUsersInRoleAsync(GlobalConstants.AdministratorRoleName);
            if (administrators.Count > 0)
            {
                return;
            }

            var adminEmail = ReadSetting(serviceProvider, SeedEmailKey, DevelopmentOnlyFallbackEmail, "login email").Trim();
            ApplicationUser adminUser = await userManager.FindByEmailAsync(adminEmail);

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

                var seedPassword = ReadSetting(serviceProvider, SeedPasswordKey, DevelopmentOnlyFallbackPassword, "password");

                IdentityResult result = await userManager.CreateAsync(adminUser, seedPassword);
                if (!result.Succeeded)
                {
                    throw new Exception(string.Join(Environment.NewLine, result.Errors.Select(e => e.Description)));
                }
            }

            if (!await userManager.IsInRoleAsync(adminUser, GlobalConstants.AdministratorRoleName))
            {
                IdentityResult result = await userManager.AddToRoleAsync(adminUser, GlobalConstants.AdministratorRoleName);
                if (!result.Succeeded)
                {
                    throw new Exception(string.Join(Environment.NewLine, result.Errors.Select(e => e.Description)));
                }
            }
        }

        private static string ReadSetting(IServiceProvider serviceProvider, string key, string developmentOnlyFallback, string what)
        {
            IConfiguration configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            // HandyFix.Data is a plain class library with no ASP.NET Core hosting reference
            // (by design: see the layering in PROJECT_STATE.md Section 1), so this
            // reads the same environment variable IWebHostEnvironment.IsDevelopment() checks,
            // rather than pulling a hosting-abstractions dependency into the data layer.
            var isDevelopment = string.Equals(
                Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                "Development",
                StringComparison.OrdinalIgnoreCase);
            if (!isDevelopment)
            {
                throw new InvalidOperationException(
                    $"{key} is not configured. Set it (User Secrets locally, an environment variable in staging/production) before the app can seed an administrator account outside Development.");
            }

            serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AdminUserSeeder))
                .LogWarning("{Key} is not configured: seeding the Development-only fallback admin {What}. Set {Key} via 'dotnet user-secrets set' to use your own.", key, what, key);

            return developmentOnlyFallback;
        }
    }
}
