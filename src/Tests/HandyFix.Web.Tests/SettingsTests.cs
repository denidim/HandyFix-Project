namespace HandyFix.Web.Tests
{
    using System.Collections.Generic;

    using HandyFix.Services.Data.Emails;
    using HandyFix.Services.Data.Payments;
    using HandyFix.Web.Settings;

    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    using Xunit;

    // The settings classes are filled from the keys the servers set: appsettings.json, and the
    // compose files' environment variables (PROJECT_STATE.md Section 3dk). A wrong section name
    // would still build, and would leave every value in it empty without a word.
    public class SettingsTests
    {
        [Fact]
        public void EachSettingIsReadFromItsOwnKey()
        {
            using var root = new SqliteWebApplicationFactory();
            using WebApplicationFactory<Program> host = root.WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((context, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string>
                    {
                        ["Business:ShowOnSiteReviews"] = "true",
                        ["Business:GoogleReviewsUrl"] = "https://g.page/r/example",
                        ["Business:GoogleLeaveReviewUrl"] = "https://g.page/r/example/review",
                        ["Email:BookingsFromAddress"] = "hello@staging.example",
                        ["Email:SystemFromAddress"] = "system@staging.example",
                        ["Email:SubjectPrefix"] = "[STAGING]",
                        ["Admin:NotificationEmail"] = "inbox@staging.example",
                        ["Site:PublicUrl"] = "https://staging.example/",
                        ["Stripe:SecretKey"] = "rk_test_example",
                        ["Stripe:WebhookSecret"] = "whsec_example",
                        ["Stripe:AllowSandboxOutsideDevelopment"] = "true",
                    })));

            BusinessOptions business = host.Services.GetRequiredService<IOptions<BusinessOptions>>().Value;
            Assert.True(business.ShowOnSiteReviews);
            Assert.Equal("https://g.page/r/example", business.GoogleReviewsUrl);
            Assert.Equal("https://g.page/r/example/review", business.GoogleLeaveReviewUrl);

            EmailOptions email = host.Services.GetRequiredService<IOptions<EmailOptions>>().Value;
            Assert.Equal("hello@staging.example", email.BookingsFromAddress);
            Assert.Equal("system@staging.example", email.SystemFromAddress);
            Assert.Equal("[STAGING]", email.SubjectPrefix);
            Assert.Equal("inbox@staging.example", email.AdminNotificationAddress);
            Assert.Equal("https://staging.example", email.SiteUrl);

            StripeOptions stripe = host.Services.GetRequiredService<IOptions<StripeOptions>>().Value;
            Assert.Equal("rk_test_example", stripe.SecretKey);
            Assert.Equal("whsec_example", stripe.WebhookSecret);
            Assert.True(stripe.AllowSandboxOutsideDevelopment);
        }

        // What the live site runs with: appsettings.json's own Business section, and the email
        // defaults where its compose file passes an empty string.
        [Fact]
        public void TheLiveSitesValuesAreTheDefaults()
        {
            using var root = new SqliteWebApplicationFactory();
            using WebApplicationFactory<Program> host = root.WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((context, configuration) => configuration.AddInMemoryCollection(
                    new Dictionary<string, string>
                    {
                        ["Email:SubjectPrefix"] = string.Empty,
                        ["Admin:NotificationEmail"] = " ",
                    })));

            BusinessOptions business = host.Services.GetRequiredService<IOptions<BusinessOptions>>().Value;
            Assert.False(business.ShowOnSiteReviews);
            Assert.Equal("https://g.page/r/CRa4VR4BecsDEBM", business.GoogleReviewsUrl);
            Assert.Equal("https://g.page/r/CRa4VR4BecsDEBM/review", business.GoogleLeaveReviewUrl);

            EmailOptions email = host.Services.GetRequiredService<IOptions<EmailOptions>>().Value;
            Assert.Null(email.SubjectPrefix);
            Assert.Equal("info@plumbing-handyman-surrey.co.uk", email.AdminNotificationAddress);
            Assert.Equal("https://plumbing-handyman-surrey.co.uk", email.SiteUrl);
        }
    }
}
