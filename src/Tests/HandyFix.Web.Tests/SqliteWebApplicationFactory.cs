namespace HandyFix.Web.Tests
{
    using System;
    using System.Linq;

    using HandyFix.Data;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore;
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
        // Held open for the lifetime of the factory: a Sqlite in-memory database exists only as
        // long as at least one connection to it is open, so closing this would drop the schema
        // and the seeded data between requests.
        private readonly SqliteConnection connection = new SqliteConnection("DataSource=:memory:");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Seeding needs a Development environment: outside it, AdminUserSeeder throws rather
            // than falling back to its dev-only password, by design.
            builder.UseEnvironment(Environments.Development);

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
