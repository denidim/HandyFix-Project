namespace HandyFix.Services.Data.Common
{
    using System;
    using System.Threading.Tasks;

    using HandyFix.Services.Messaging;

    using Microsoft.Extensions.Logging;

    public static class EmailSenderExtensions
    {
        // Sends an email that follows something already saved (a booking, a payment, an enquiry)
        // and says whether it went. A send that fails is logged and does not throw: the thing it
        // was about is in the database either way, and an error page at that point would tell a
        // customer their booking or payment failed when it had not (PROJECT_STATE Section 3cb).
        //
        // "kind" names the email in the log ("enquiry notice to the company"), so the log says
        // which one failed without holding anybody's address.
        public static async Task<bool> TrySendEmailAsync(
            this IEmailSender emailSender,
            ILogger logger,
            string kind,
            string from,
            string fromName,
            string to,
            string subject,
            string htmlContent,
            string replyTo = null)
        {
            try
            {
                await emailSender.SendEmailAsync(from, fromName, to, subject, htmlContent, null, replyTo);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Email not sent: {EmailKind}", kind);
                return false;
            }
        }
    }
}
