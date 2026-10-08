namespace HandyFix.Services.Data.Common
{
    using Microsoft.Extensions.Configuration;

    // The addresses the site's emails go from and to, each with the real domain's address as its
    // default and a configuration key to override it. Brevo, like any real email provider,
    // refuses a sender it has not verified. Staging keeps the defaults, so the real sender is
    // tried there before the live site depends on it; only its notices go somewhere else.
    public static class EmailSettings
    {
        // The name on emails to a customer, and on notices to the company about a customer.
        public const string CustomerFromName = "Plumbing Handyman Surrey";

        public const string WebsiteFromName = "Plumbing Handyman Surrey Website";

        private const string DefaultFromAddress = "bookings@plumbing-handyman-surrey.co.uk";

        private const string DefaultAdminNotificationAddress = "info@plumbing-handyman-surrey.co.uk";

        // The sender of every email to a customer. Replies to it reach the company's inbox.
        public static string BookingsFromAddress(IConfiguration configuration)
        {
            return Read(configuration, "Email:BookingsFromAddress", DefaultFromAddress);
        }

        // The sender of the site's own notices: a booking was received, the company was told.
        public static string SystemFromAddress(IConfiguration configuration)
        {
            return Read(configuration, "Email:SystemFromAddress", DefaultFromAddress);
        }

        // The one inbox every notice to the company goes to: paid deposits, enquiries, job
        // applications (decided 2026-09-21, PROJECT_STATE Section 3bb).
        public static string AdminNotificationAddress(IConfiguration configuration)
        {
            return Read(configuration, "Admin:NotificationEmail", DefaultAdminNotificationAddress);
        }

        // A mark put in front of every subject: "[STAGING]" on staging, nothing on the live site.
        // Null when there is none to add.
        public static string SubjectPrefix(IConfiguration configuration)
        {
            return Read(configuration, "Email:SubjectPrefix", null);
        }

        private static string Read(IConfiguration configuration, string key, string fallback)
        {
            var value = configuration[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
