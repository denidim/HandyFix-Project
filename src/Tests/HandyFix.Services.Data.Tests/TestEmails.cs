namespace HandyFix.Services.Data.Tests
{
    using HandyFix.Services.Data.Emails;
    using HandyFix.Services.Messaging;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;

    // The real email classes over the email sender a test hands in, so a test of a service still
    // sees every email that service has sent, word for word.
    internal static class TestEmails
    {
        public static BookingEmails Booking(IEmailSender emailSender, IConfiguration configuration = null)
        {
            return new BookingEmails(emailSender, Settings(configuration), NullLogger<BookingEmails>.Instance);
        }

        public static EnquiryEmails Enquiry(IEmailSender emailSender, IConfiguration configuration = null)
        {
            return new EnquiryEmails(emailSender, Settings(configuration), NullLogger<EnquiryEmails>.Instance);
        }

        // The email settings read from the keys a test gives, the way the site reads its own, so
        // a test that sets a key proves the key's name too. No keys, the defaults.
        private static IOptions<EmailOptions> Settings(IConfiguration configuration)
        {
            var settings = new EmailOptions();
            settings.ReadFrom(configuration ?? new ConfigurationBuilder().Build());
            return Options.Create(settings);
        }
    }
}
