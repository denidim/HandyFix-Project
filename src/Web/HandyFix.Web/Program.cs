namespace HandyFix.Web
{
    using System;
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
    using HandyFix.Services.Data.Common;
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
    using HandyFix.Web.Services.Forms;
    using HandyFix.Web.ViewModels;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.HttpOverrides;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Hosting;

    using WebOptimizer;

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

            // WebOptimizer: the public stylesheets combined into one minified file, in the order
            // SiteStylesheets lists them (PROJECT_STATE Section 3bs). Its tag helper stamps the
            // layout's link with a hash of the combined content, so a change in any source file
            // changes the address and no browser keeps a stale copy.
            services.AddWebOptimizer(pipeline =>
            {
                pipeline.AddCssBundle(SiteStylesheets.BundleRoute, SiteStylesheets.SourceFiles);
                pipeline.AddJavaScriptBundle("/js/site.min.js", "js/site.js");
            });

            // Set here rather than left to the package's defaults, so what each environment does
            // is written down. Live: pages link the one combined file, and browsers may keep it
            // for a year, since its address changes when its content does. Development: pages link
            // each source file by itself, which is easier to debug. No disk cache anywhere: the
            // bundle is rebuilt in memory after a start, and nothing is written next to the app.
            services.AddOptions<WebOptimizerOptions>().Configure<IHostEnvironment>((options, environment) =>
            {
                bool isLive = !environment.IsDevelopment();
                options.EnableTagHelperBundling = isLive;
                options.EnableCaching = isLive;
                options.EnableDiskCache = false;
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
                    // Staging sends from the live site's address, so it marks its subjects.
                    IEmailSender brevo = new BrevoEmailSender(apiKey);
                    var subjectPrefix = EmailSettings.SubjectPrefix(configuration);
                    return subjectPrefix == null ? brevo : new SubjectPrefixEmailSender(brevo, subjectPrefix);
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

            // Abuse protection on the public forms (PROJECT_STATE Section 3cb). The clock is a
            // service so a test can set the time a form was shown and the time it came back.
            services.TryAddSingleton(TimeProvider.System);
            services.AddTransient<IFormGuard, FormGuard>();
            services.AddRateLimiter(RateLimits.Configure);

            // Five seconds to ask Cloudflare about a Turnstile token. If no answer comes the
            // submission is let through (TurnstileVerifier says why), so this is also the longest
            // a visitor waits on it.
            services.AddHttpClient<ITurnstileVerifier, TurnstileVerifier>(client => client.Timeout = TimeSpan.FromSeconds(5));

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

            MappingConfig.RegisterMappings(typeof(StatusPageViewModel).GetTypeInfo().Assembly);

            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseMigrationsEndPoint();
            }
            else
            {
                // ErrorsController, not HomeController: the page that says something broke must
                // not need the services that may be what broke.
                app.UseExceptionHandler("/StatusPage/500");
                app.UseHsts();
            }

            // A response that ends in an error status with nothing in it (a page that is not
            // there, a form sent too often, a form whose token expired) is shown as the site's
            // own page for that status, which keeps the status. Without this the visitor got the
            // browser's blank error screen. Only for a request that asked for a page: a script
            // fetching data, a crawler probing for files and a payment provider calling back get
            // the bare status as before, and cost no page render (PROJECT_STATE Section 3cb).
            app.UseWhen(
                context => AsksForAPage(context.Request),
                branch => branch.UseStatusCodePagesWithReExecute("/StatusPage/{0}"));

            app.UseHttpsRedirection();
            app.UseWebOptimizer();
            app.UseStaticFiles(new StaticFileOptions
            {
                // Site images get replaced in place under the same file name (PROJECT_STATE Section 3bd).
                // With no Cache-Control, browsers cached them heuristically and kept showing the old
                // pictures; "no-cache" makes them re-check every time (a cheap 304 when unchanged).
                OnPrepareResponse = context =>
                {
                    if (context.Context.Request.Path.StartsWithSegments("/images"))
                    {
                        context.Context.Response.Headers.CacheControl = "no-cache";
                    }
                },
            });
            app.UseCookiePolicy();

            app.UseRouting();

            // After routing, because the limits are set per action ([EnableRateLimiting]) and the
            // limiter has to know which action a request is for.
            app.UseRateLimiter();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllerRoute("areaRoute", "{area:exists}/{controller=Home}/{action=Index}/{id?}");
            app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
            app.MapRazorPages();
        }

        // A browser navigating to a page says it will take HTML; nothing else does.
        private static bool AsksForAPage(HttpRequest request)
        {
            foreach (var accept in request.Headers.Accept)
            {
                if (accept != null && accept.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
