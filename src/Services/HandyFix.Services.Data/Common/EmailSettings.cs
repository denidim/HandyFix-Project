namespace HandyFix.Services.Data.Common
{
    using Microsoft.Extensions.Configuration;

    // The addresses the site's emails go from and to, each with the real domain's address as its
    // default and a configuration key to override it. Brevo, like any real email provider,
    // refuses a sender it has not verified, so staging and local testing point these at an
    // address that is verified in the Brevo account they use.
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

        private static string Read(IConfiguration configuration, string key, string fallback)
        {
            var value = configuration[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
