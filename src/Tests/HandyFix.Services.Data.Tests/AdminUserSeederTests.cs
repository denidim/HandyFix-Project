namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Seeding;

    using Microsoft.AspNetCore.Identity;
    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    using Xunit;

    // The admin account is made at the one start that finds no administrator, from two settings.
    // Each test here is a start of the application: the same database, the settings that start
    // was given (PROJECT_STATE.md Section 3cc).
    public sealed class AdminUserSeederTests : IDisposable
    {
        private const string EnvironmentVariable = "ASPNETCORE_ENVIRONMENT";

        private readonly SqliteConnection connection;
        private readonly string environmentBefore;

        public AdminUserSeederTests()
        {
            this.connection = new SqliteConnection("DataSource=:memory:");
            this.connection.Open();
            this.environmentBefore = Environment.GetEnvironmentVariable(EnvironmentVariable);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(EnvironmentVariable, this.environmentBefore);
            this.connection.Dispose();
        }

        [Fact]
        public async Task TheFirstStartMakesTheAdministratorFromTheTwoSettings()
        {
            await this.StartAsync("Production", email: " owner@example.com ", password: "correct horse battery");

            await this.WithUsersAsync(async users =>
            {
                ApplicationUser admin = Assert.Single(users.Users.ToList());
                Assert.Equal("owner@example.com", admin.Email);
                Assert.Equal("owner@example.com", admin.UserName);
                Assert.True(await users.IsInRoleAsync(admin, GlobalConstants.AdministratorRoleName));
                Assert.True(await users.CheckPasswordAsync(admin, "correct horse battery"));

                // A wrong password has to be able to pause this account's login.
                Assert.True(admin.LockoutEnabled);
            });
        }

        // The seeder used to look for one address written in the code. Once the login email can
        // be changed in the admin panel, that would have made a second administrator, with the
        // seed password, at the first start after a change.
        [Fact]
        public async Task AStartAfterTheLoginEmailWasChangedDoesNotMakeASecondAdministrator()
        {
            await this.StartAsync("Production", email: "owner@example.com", password: "correct horse battery");

            await this.WithUsersAsync(async users =>
            {
                ApplicationUser admin = users.Users.Single();
                admin.UserName = "new.owner@example.com";
                admin.Email = "new.owner@example.com";
                Assert.True((await users.UpdateAsync(admin)).Succeeded);
            });

            await this.StartAsync("Production", email: "owner@example.com", password: "correct horse battery");

            await this.WithUsersAsync(users =>
            {
                Assert.Equal("new.owner@example.com", Assert.Single(users.Users.ToList()).Email);
                return Task.CompletedTask;
            });
        }

        // Both settings count at the first start only. This is why choosing the real password
        // has to happen before a new database starts for the first time.
        [Fact]
        public async Task ALaterStartWithOtherSettingsChangesNeitherTheLoginNorThePassword()
        {
            await this.StartAsync("Production", email: "owner@example.com", password: "correct horse battery");
            await this.StartAsync("Production", email: "someone.else@example.com", password: "another password entirely");

            await this.WithUsersAsync(async users =>
            {
                ApplicationUser admin = Assert.Single(users.Users.ToList());
                Assert.Equal("owner@example.com", admin.Email);
                Assert.True(await users.CheckPasswordAsync(admin, "correct horse battery"));
                Assert.False(await users.CheckPasswordAsync(admin, "another password entirely"));
            });
        }

        [Theory]
        [InlineData(null, "correct horse battery", "Admin:SeedEmail")]
        [InlineData("owner@example.com", null, "Admin:SeedPassword")]
        [InlineData("   ", "correct horse battery", "Admin:SeedEmail")]
        public async Task OutsideDevelopmentAFirstStartWithASettingMissingFailsAndNamesIt(string email, string password, string missing)
        {
            InvalidOperationException failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => this.StartAsync("Staging", email, password));

            Assert.Contains(missing, failure.Message);
            await this.WithUsersAsync(users =>
            {
                Assert.Empty(users.Users.ToList());
                return Task.CompletedTask;
            });
        }

        // Staging and the live site after their first start: the settings can be gone and the
        // application still starts.
        [Fact]
        public async Task OutsideDevelopmentAStartThatFindsAnAdministratorNeedsNeitherSetting()
        {
            await this.StartAsync("Production", email: "owner@example.com", password: "correct horse battery");

            await this.StartAsync("Production", email: null, password: null);

            await this.WithUsersAsync(users =>
            {
                Assert.Single(users.Users.ToList());
                return Task.CompletedTask;
            });
        }

        [Fact]
        public async Task ASeedPasswordShorterThanTheRuleIsRefusedAtTheStart()
        {
            var tooShort = new string('a', GlobalConstants.PasswordMinimumLength - 1);

            Exception failure = await Assert.ThrowsAnyAsync<Exception>(
                () => this.StartAsync("Production", email: "owner@example.com", password: tooShort));

            Assert.Contains("at least " + GlobalConstants.PasswordMinimumLength, failure.Message);
        }

        // With neither setting, a developer's machine still gets an admin to sign in with. The
        // fallback password has to pass the same rule as any other, or a fresh clone cannot start.
        [Fact]
        public async Task InDevelopmentAFirstStartWithNoSettingsFallsBackToAnAdministratorThatCanSignIn()
        {
            await this.StartAsync("Development", email: null, password: null);

            await this.WithUsersAsync(async users =>
            {
                ApplicationUser admin = Assert.Single(users.Users.ToList());
                Assert.True(await users.IsInRoleAsync(admin, GlobalConstants.AdministratorRoleName));
                Assert.True(await users.HasPasswordAsync(admin));
            });
        }

        // One start of the application, as far as the roles and the admin account go: a fresh
        // set of services over the same database, then the two seeders in their real order.
        private async Task StartAsync(string environment, string email, string password)
        {
            Environment.SetEnvironmentVariable(EnvironmentVariable, environment);

            using ServiceProvider services = this.BuildServices(new Dictionary<string, string>
            {
                [AdminUserSeeder.SeedEmailKey] = email,
                [AdminUserSeeder.SeedPasswordKey] = password,
            });
            using IServiceScope scope = services.CreateScope();

            ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

            await new RolesSeeder().SeedAsync(dbContext, scope.ServiceProvider);
            await dbContext.SaveChangesAsync();
            await new AdminUserSeeder().SeedAsync(dbContext, scope.ServiceProvider);
            await dbContext.SaveChangesAsync();
        }

        private async Task WithUsersAsync(Func<UserManager<ApplicationUser>, Task> look)
        {
            using ServiceProvider services = this.BuildServices(new Dictionary<string, string>());
            using IServiceScope scope = services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();

            await look(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
        }

        private ServiceProvider BuildServices(Dictionary<string, string> settings)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(this.connection));
            services.AddIdentityCore<ApplicationUser>(IdentityOptionsProvider.GetIdentityOptions)
                .AddRoles<ApplicationRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            return services.BuildServiceProvider();
        }
    }
}
