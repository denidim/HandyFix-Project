namespace HandyFix.Web
{
    using System.Reflection;

    using HandyFix.Data;
    using HandyFix.Data.Common;
    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Data.Seeding;
    using HandyFix.Services;
    using HandyFix.Services.Data;
    using HandyFix.Services.Data.Availability;
    using HandyFix.Services.Data.Bookings;
    using HandyFix.Services.Data.Categories;
    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Services.Data.Reviews;
    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Services.Data.Services;
    using HandyFix.Services.Data.Technicians;
    using HandyFix.Services.Mapping;
    using HandyFix.Services.Messaging;
    using HandyFix.Web.BackgroundServices;
    using HandyFix.Web.Services;
    using HandyFix.Web.ViewModels;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.HttpOverrides;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    public class Program
    {
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
            ConfigureServices(builder.Services, builder.Configuration);
            WebApplication app = builder.Build();
            Configure(app);
            app.Run();
        }

        private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<ApplicationDbContext>(
                options => options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection"),
                    sqlOptions => sqlOptions.CommandTimeout(180))
                .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning)));

            services.AddDefaultIdentity<ApplicationUser>(IdentityOptionsProvider.GetIdentityOptions)
                .AddRoles<ApplicationRole>().AddEntityFrameworkStores<ApplicationDbContext>();

            services.Configure<CookiePolicyOptions>(
                options =>
                {
                    options.CheckConsentNeeded = context => true;

                    // Lax, not None: this is an ordinary first-party cookie with no cross-site
                    // need. None would additionally require Secure, which only reads correctly
                    // when the app knows a request arrived over HTTPS - see the
                    // ForwardedHeadersOptions comment below for why that isn't automatic here.
                    options.MinimumSameSitePolicy = SameSiteMode.Lax;
                });

            // Form confirmations ("we've received your enquiry") reach the next page through the
            // TempData cookie. With CheckConsentNeeded on, a non-essential cookie is never written
            // until the visitor accepts the banner, so the confirmation silently vanished. It carries
            // no tracking data and exists only to answer a request the visitor just made - the
            // strictly-necessary category, like the antiforgery cookie.
            services.Configure<CookieTempDataProviderOptions>(options => options.Cookie.IsEssential = true);

            // Staging/production run behind Caddy, which terminates TLS and proxies to this
            // container over plain HTTP on the internal Docker network - so without this,
            // Request.Scheme/IsHttps reads "http" for every request regardless of what the
            // browser actually used. That silently breaks anything that depends on knowing the
            // real scheme: Secure-flagged cookies, and absolute URLs built from Request.Scheme
            // (e.g. the Stripe Checkout success/cancel URLs in PaymentController).
            //
            // KnownNetworks/KnownProxies are cleared rather than pinned to Caddy's address
            // because Docker Compose assigns that address dynamically - there's no fixed IP to
            // allow-list. This is safe here specifically because the web container publishes no
            // ports of its own (see deploy/docker-compose.staging.yml): Caddy is the only thing
            // on the internal network that can reach it at all, so nothing else is in a position
            // to send it a spoofed X-Forwarded-* header.
            services.Configure<ForwardedHeadersOptions>(
                options =>
                {
                    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                        | ForwardedHeaders.XForwardedProto
                        | ForwardedHeaders.XForwardedHost;
                    options.KnownNetworks.Clear();
                    options.KnownProxies.Clear();
                });

            services.AddControllersWithViews(
                options =>
                {
                    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
                }).AddRazorRuntimeCompilation();
            services.AddRazorPages();
            services.AddDatabaseDeveloperPageExceptionFilter();

            services.AddSingleton(configuration);

            // WebOptimizer (bundling and minification)
            services.AddWebOptimizer(pipeline =>
            {
                pipeline.AddCssBundle("/css/site.min.css", "css/site.css");
                pipeline.AddJavaScriptBundle("/js/site.min.js", "js/site.js");
            });

            // Data repositories
            services.AddScoped(typeof(IDeletableEntityRepository<>), typeof(EfDeletableEntityRepository<>));
            services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
            services.AddScoped<IDbQueryRunner, DbQueryRunner>();

            // Application services
            services.AddTransient<IEmailSender>(sp =>
            {
                var apiKey = configuration["Brevo:ApiKey"];
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    return new BrevoEmailSender(apiKey);
                }

                Microsoft.AspNetCore.Hosting.IWebHostEnvironment env = sp.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
                if (env.IsDevelopment())
                {
                    return new NullMessageSender();
                }

                throw new System.InvalidOperationException("Brevo is not configured for this environment. Set Brevo:ApiKey before accepting real bookings.");
            });
            services.AddTransient<ICategoriesService, CategoriesService>();
            services.AddTransient<IServicesService, ServicesService>();
            services.AddTransient<IServiceAreasService, ServiceAreasService>();
            services.AddTransient<IReviewsService, ReviewsService>();
            services.AddTransient<IInquiriesService, InquiriesService>();
            services.AddTransient<IAvailabilityService, AvailabilityService>();
            services.AddTransient<IBookingsService, BookingsService>();
            services.AddTransient<ITechniciansService, TechniciansService>();
            services.AddTransient<IPaymentsService, PaymentsService>();
            services.AddTransient<ICloudflareR2Service, CloudflareR2Service>();
            services.AddTransient<IImageService, ImageService>();
            services.AddTransient<IImageStorageService>(sp =>
                new ImageStorageService(
                    System.IO.Path.Combine(
                        sp.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>().WebRootPath,
                        "images",
                        "services"),
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ImageStorageService>>()));

            // Background workers
            services.AddHostedService<StaleBookingCleanupService>();
        }

        private static void Configure(WebApplication app)
        {
            // Must run before everything else, including exception/HSTS handling: every later
            // middleware and every action that reads Request.Scheme/Request.Host/RemoteIpAddress
            // depends on this having already corrected them from the forwarded headers.
            app.UseForwardedHeaders();

            // Seed data on application startup
            using (IServiceScope serviceScope = app.Services.CreateScope())
            {
                ApplicationDbContext dbContext = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                // The migrations are SQL Server-specific (rowversion in particular), so they can
                // only be replayed against SQL Server. A host running on any other provider is a
                // test host pointed at Sqlite in-memory, which builds the schema from the model
                // instead. Production and development both take the Migrate() branch.
                if (dbContext.Database.IsSqlServer())
                {
                    dbContext.Database.Migrate();
                }
                else
                {
                    dbContext.Database.EnsureCreated();
                }

                new ApplicationDbContextSeeder().SeedAsync(dbContext, serviceScope.ServiceProvider).GetAwaiter().GetResult();

                DevelopmentCapacitySeeder.SeedAsync(serviceScope.ServiceProvider, app.Environment).GetAwaiter().GetResult();
            }

            MappingConfig.RegisterMappings(typeof(ErrorViewModel).GetTypeInfo().Assembly);

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseWebOptimizer();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute("areaRoute", "{area:exists}/{controller=Home}/{action=Index}/{id?}");
            app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            app.MapRazorPages();
        }
    }
}
