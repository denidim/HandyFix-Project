namespace HandyFix.Web.Services.Forms
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.RateLimiting;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.RateLimiting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    // How often one visitor may send the public forms and ask for time slots (PROJECT_STATE.md
    // Section 3cb). ASP.NET Core's own rate limiter does the counting; this says who counts as
    // one visitor and how many is too many.
    public static class RateLimits
    {
        // One allowance for the three forms together: Contact, Join Our Team and Booking.
        public const string FormsPolicy = "forms";

        // The booking page asks for a day's slots each time a day is clicked.
        public const string SlotLookupsPolicy = "slot-lookups";

        // Signing in and resetting the admin password (PROJECT_STATE.md Section 3cc). The same
        // numbers as the forms but an allowance of its own, so a customer's enquiries and
        // somebody's guesses at the password do not use each other's up.
        public const string AccountPolicy = "account";

        public const string FormPostsKey = "RateLimiting:FormPostsPerWindow";

        public const string FormWindowMinutesKey = "RateLimiting:FormWindowMinutes";

        public const string SlotLookupsKey = "RateLimiting:SlotLookupsPerMinute";

        // Ten, not fewer: a submission the form's own rules refuse counts too, and a mobile
        // network can put many customers behind one address.
        private const int DefaultFormPosts = 10;

        private const int DefaultFormWindowMinutes = 10;

        private const int DefaultSlotLookupsPerMinute = 60;

        public static void Configure(RateLimiterOptions options)
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(FormsPolicy, ForForms);
            options.AddPolicy(SlotLookupsPolicy, ForSlotLookups);
            options.AddPolicy(AccountPolicy, ForAccount);
            options.OnRejected = OnRejectedAsync;
        }

        // Who counts as one visitor: the address the request came from. An IPv6 address is cut to
        // its first half, the part a provider gives to one customer; the second half is the
        // customer's to change at will, which would otherwise be a fresh allowance each time.
        public static string ClientKey(IPAddress address)
        {
            if (address == null)
            {
                return "unknown";
            }

            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            if (address.AddressFamily != AddressFamily.InterNetworkV6)
            {
                return address.ToString();
            }

            var bytes = address.GetAddressBytes();
            Array.Clear(bytes, 8, 8);
            return new IPAddress(bytes) + "/64";
        }

        // The limits are read when a visitor's counter is first made, not when the app starts, so
        // a test host can set its own.
        private static RateLimitPartition<string> ForForms(HttpContext httpContext)
        {
            return FormPostsWindow(httpContext, FormsPolicy);
        }

        // A Razor Page takes a limit on its whole model, not on one handler, so this is asked
        // about every request to the login and reset pages. Only what is sent counts: opening
        // the login page is not a guess at a password.
        private static RateLimitPartition<string> ForAccount(HttpContext httpContext)
        {
            return HttpMethods.IsPost(httpContext.Request.Method)
                ? FormPostsWindow(httpContext, AccountPolicy)
                : RateLimitPartition.GetNoLimiter(AccountPolicy + ":not-sent");
        }

        private static RateLimitPartition<string> FormPostsWindow(HttpContext httpContext, string policy)
        {
            IConfiguration configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
            var permits = Read(configuration, FormPostsKey, DefaultFormPosts);
            var minutes = Read(configuration, FormWindowMinutesKey, DefaultFormWindowMinutes);

            return RateLimitPartition.GetFixedWindowLimiter(
                policy + ":" + ClientKey(httpContext.Connection.RemoteIpAddress),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permits,
                    Window = TimeSpan.FromMinutes(minutes),
                    QueueLimit = 0,
                });
        }

        private static RateLimitPartition<string> ForSlotLookups(HttpContext httpContext)
        {
            IConfiguration configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
            var permits = Read(configuration, SlotLookupsKey, DefaultSlotLookupsPerMinute);

            return RateLimitPartition.GetFixedWindowLimiter(
                SlotLookupsPolicy + ":" + ClientKey(httpContext.Connection.RemoteIpAddress),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permits,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
        }

        // The answer is a bare 429. A browser sending a form is then shown the site's own "too
        // many attempts" page (the status pages in Program.cs); the booking page's script, which
        // asked for slots, reads the status and says so in its own words.
        private static ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken cancellationToken)
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }

            // The path, not the address: the log says what was hit without holding who by.
            context.HttpContext.RequestServices
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(RateLimits))
                .LogWarning("Rate limit reached on {Method} {Path}", context.HttpContext.Request.Method, context.HttpContext.Request.Path);

            return ValueTask.CompletedTask;
        }

        private static int Read(IConfiguration configuration, string key, int fallback)
        {
            return int.TryParse(configuration[key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
                ? value
                : fallback;
        }
    }
}
