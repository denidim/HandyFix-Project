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
    using HandyFix.Services.Data.Services;
    using HandyFix.Services.Mapping;
    using HandyFix.Services.Messaging;
    using HandyFix.Web.BackgroundServices;
    using HandyFix.Web.ViewModels;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Http;
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
            var builder = WebApplication.CreateBuilder(args);
            ConfigureServices(builder.Services, builder.Configuration);
            var app = builder.Build();
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
                    options.MinimumSameSitePolicy = SameSiteMode.None;
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
            services.AddTransient<IEmailSender, NullMessageSender>();
            services.AddTransient<ISettingsService, SettingsService>();
            services.AddTransient<ICategoriesService, CategoriesService>();
            services.AddTransient<IServicesService, ServicesService>();
            services.AddTransient<IReviewsService, ReviewsService>();
            services.AddTransient<IInquiriesService, InquiriesService>();
            services.AddTransient<IAvailabilityService, AvailabilityService>();
            services.AddTransient<IBookingsService, BookingsService>();
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
            // Seed data on application startup
            using (var serviceScope = app.Services.CreateScope())
            {
                var dbContext = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                dbContext.Database.Migrate();
                new ApplicationDbContextSeeder().SeedAsync(dbContext, serviceScope.ServiceProvider).GetAwaiter().GetResult();

                var imageStorageService = serviceScope.ServiceProvider.GetRequiredService<IImageStorageService>();
                imageStorageService.ConvertExistingJpgServiceImages();
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
