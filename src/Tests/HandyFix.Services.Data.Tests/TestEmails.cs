namespace HandyFix.Services.Data.Tests
{
    using HandyFix.Services.Data.Emails;
    using HandyFix.Services.Messaging;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;

    // The real email classes over the email sender a test hands in, so a test of a service still
    // sees every email that service has sent, word for word.
    internal static class TestEmails
    {
        public static BookingEmails Booking(IEmailSender emailSender, IConfiguration configuration = null)
        {
            return new BookingEmails(
                emailSender,
                configuration ?? new ConfigurationBuilder().Build(),
                NullLogger<BookingEmails>.Instance);
        }
    }
}
