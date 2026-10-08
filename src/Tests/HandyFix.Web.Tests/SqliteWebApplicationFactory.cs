namespace HandyFix.Web.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using HandyFix.Data;
    using HandyFix.Web.Services.Forms;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    /// <summary>
    /// Boots the real application against a private Sqlite in-memory database instead of whatever
    /// <c>ConnectionStrings:DefaultConnection</c> points at.
    /// <para>
    /// Without this the integration tests migrate and seed the developer's actual dev database on
    /// every run, and cannot run at all on a machine with no SQL Server - which would make them
    /// fail the moment the CI pipeline exists (PROJECT_STATE.md section 4, Tier 4).
    /// </para>
    /// <para>
    /// Sqlite is deliberate rather than the EF InMemory provider: these tests exercise real
    /// queries through the whole stack, and InMemory silently ignores relational behaviour. It
    /// does not enforce the <c>RowVersion</c> concurrency token either, so anything that needs
    /// real double-booking rejection still belongs in the service-layer tests, which construct
    /// their own Sqlite context for exactly that reason.
    /// </para>
    /// </summary>
    public class SqliteWebApplicationFactory : WebApplicationFactory<Program>
    {
        // The administrator this host seeds. Set here because a Development host also reads the
        // developer's own user secrets: left to those, the admin's login would be one thing on
        // the developer's machine and another on the CI runner (PROJECT_STATE.md Section 3cc).
        public const string AdminEmail = "owner@example.com";

        public const string AdminPassword = "correct horse battery";

        // Held open for the lifetime of the factory: a Sqlite in-memory database exists only as
        // long as at least one connection to it is open, so closing this would drop the schema
        // and the seeded data between requests.
        private readonly SqliteConnection connection = new SqliteConnection("DataSource=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Seeding needs a Development environment: outside it, AdminUserSeeder throws rather
            // than falling back to its dev-only password, by design. UseEnvironment() alone is not
            // enough -- AdminUserSeeder reads the raw ASPNETCORE_ENVIRONMENT process variable
            // directly (HandyFix.Data has no ASP.NET Core hosting reference to check
            // IWebHostEnvironment with instead), which UseEnvironment() does not touch. Both were
            // set the same by coincidence on machines with ASPNETCORE_ENVIRONMENT=Development set
            // globally, masking this until a clean CI runner (no such variable) exposed it.
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", Environments.Development);
            builder.UseEnvironment(Environments.Development);

            // A test sends a form the instant it has fetched it, which is exactly what the form
            // guard takes for a program, and a class of tests sends more forms in a second than
            // one visitor is allowed in ten minutes (PROJECT_STATE.md Section 3cb). So this host
            // asks for no minimum time and all but lifts the limit; the tests of the guard and of
            // the limit put the real values back.
            builder.ConfigureAppConfiguration((context, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string>
                {
                    [FormGuard.MinimumSecondsKey] = "0",
                    [RateLimits.FormPostsKey] = "100000",
                    [RateLimits.SlotLookupsKey] = "100000",
                    ["Admin:SeedEmail"] = AdminEmail,
                    ["Admin:SeedPassword"] = AdminPassword,
                }));

            builder.ConfigureServices(services =>
            {
                RemoveDbContextRegistrations(services);

                this.connection.Open();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(this.connection));
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.connection.Dispose();
            }

            base.Dispose(disposing);
        }

        private static void RemoveDbContextRegistrations(IServiceCollection services)
        {
            // Removing DbContextOptions<ApplicationDbContext> alone is the widely-cited recipe and
            // it is no longer sufficient. Since .NET 9, AddDbContext also registers an
            // IDbContextOptionsConfiguration<TContext> that *accumulates* rather than replaces, so
            // calling AddDbContext again applies UseSqlServer and UseSqlite to the same options
            // object and EF throws "Only a single database provider can be registered".
            //
            // Matching on the service type mentioning ApplicationDbContext catches all of them
            // without naming EF internals that may be renamed again. Identity's stores are generic
            // over ApplicationDbContext in their *implementation* type only - their service types
            // (IUserStore<ApplicationUser> and friends) do not match, so they survive.
            var doomed = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(DbContextOptions) ||
                    descriptor.ServiceType.FullName?.Contains(nameof(ApplicationDbContext), StringComparison.Ordinal) == true)
                .ToList();

            foreach (var descriptor in doomed)
            {
                services.Remove(descriptor);
            }
        }
    }
}
