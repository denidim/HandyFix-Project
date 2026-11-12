namespace HandyFix.Services.Data.Emails
{
    using HandyFix.Common;

    using Microsoft.Extensions.Configuration;

    // The addresses the site's emails go from and to, each with the real domain's address as its
    // default and a configuration key to override it. Brevo, like any real email provider,
    // refuses a sender it has not verified. Staging keeps the defaults, so the real sender is
    // tried there before the live site depends on it; only its notices go somewhere else.
    // The site fills one from its configuration with ReadFrom (Program.cs); a test can make one
    // with the values it needs.
    public class EmailOptions
    {
        // The name on emails to a customer, and on notices to the company about a customer.
        public const string CustomerFromName = "Plumbing Handyman Surrey";

        public const string WebsiteFromName = "Plumbing Handyman Surrey Website";

        private const string DefaultFromAddress = "bookings@plumbing-handyman-surrey.co.uk";

        private const string DefaultAdminNotificationAddress = "info@plumbing-handyman-surrey.co.uk";

        // The sender of every email to a customer. Replies to it reach the company's inbox.
        public string BookingsFromAddress { get; set; } = DefaultFromAddress;

        // The sender of the site's own notices: a booking was received, the company was told.
        public string SystemFromAddress { get; set; } = DefaultFromAddress;

        // The one inbox every notice to the company goes to: paid deposits, enquiries, job
        // applications (decided 2026-09-21, PROJECT_STATE Section 3bb).
        public string AdminNotificationAddress { get; set; } = DefaultAdminNotificationAddress;

        // A mark put in front of every subject: "[STAGING]" on staging, nothing on the live site.
        // Null when there is none to add.
        public string SubjectPrefix { get; set; }

        // The address of the site this copy of the code runs as, with no slash at the end. For a
        // link in an email that has to open this site and not another: the admin panel behind a
        // notice to the company. Staging names itself in its compose file; told nothing, it is
        // the live site's address.
        public string SiteUrl { get; set; } = GlobalConstants.SiteUrl;

        // Google's form for leaving a review of the business, the same link the Reviews page's
        // button opens. The paid-in-full invoice email carries it as a button (PROJECT_STATE.md
        // Section 3ei); with no link set, the email has no button. Null when there is none.
        public string GoogleLeaveReviewUrl { get; set; }

        // Each value from its own key. The keys sit in four sections, so they are named one by
        // one rather than bound as a section. A key that is missing, empty or only spaces keeps
        // the default: a compose file passes an empty string for a value its .env leaves out.
        public void ReadFrom(IConfiguration configuration)
        {
            this.BookingsFromAddress = Read(configuration, "Email:BookingsFromAddress", DefaultFromAddress);
            this.SystemFromAddress = Read(configuration, "Email:SystemFromAddress", DefaultFromAddress);
            this.AdminNotificationAddress = Read(configuration, "Admin:NotificationEmail", DefaultAdminNotificationAddress);
            this.SubjectPrefix = Read(configuration, "Email:SubjectPrefix", null);
            this.SiteUrl = Read(configuration, "Site:PublicUrl", GlobalConstants.SiteUrl).TrimEnd('/');
            this.GoogleLeaveReviewUrl = Read(configuration, "Business:GoogleLeaveReviewUrl", null);
        }

        private static string Read(IConfiguration configuration, string key, string fallback)
        {
            var value = configuration[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
