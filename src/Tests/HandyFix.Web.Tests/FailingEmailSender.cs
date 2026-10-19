namespace HandyFix.Web.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Services.Messaging;

    // Stands in for the real email sender in a full-stack test and fails every send, as Brevo
    // does when it cannot be reached.
    public class FailingEmailSender : IEmailSender
    {
        public Task SendEmailAsync(
            string from,
            string fromName,
            string to,
            string subject,
            string htmlContent,
            IEnumerable<EmailAttachment> attachments = null,
            string replyTo = null)
        {
            throw new InvalidOperationException("Brevo email send failed (500): try later");
        }
    }
}
