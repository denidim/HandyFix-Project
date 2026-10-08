namespace HandyFix.Services.Messaging
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    // Puts a mark in front of the subject of every email it passes on. Staging sends from the
    // same address as the live site, so without a mark a test email reads exactly like a real
    // one (PROJECT_STATE.md Section 3cc).
    public class SubjectPrefixEmailSender : IEmailSender
    {
        private readonly IEmailSender inner;

        private readonly string prefix;

        public SubjectPrefixEmailSender(IEmailSender inner, string prefix)
        {
            this.inner = inner;
            this.prefix = prefix.Trim();
        }

        public Task SendEmailAsync(
            string from,
            string fromName,
            string to,
            string subject,
            string htmlContent,
            IEnumerable<EmailAttachment> attachments = null,
            string replyTo = null)
        {
            return this.inner.SendEmailAsync(from, fromName, to, $"{this.prefix} {subject}", htmlContent, attachments, replyTo);
        }
    }
}
