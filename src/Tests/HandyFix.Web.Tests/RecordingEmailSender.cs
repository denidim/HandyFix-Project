namespace HandyFix.Web.Tests
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Services.Messaging;

    // Stands in for the real email sender in a full-stack test and keeps what it was asked to send.
    public class RecordingEmailSender : IEmailSender
    {
        public ConcurrentQueue<Email> Sent { get; } = new ConcurrentQueue<Email>();

        public Task SendEmailAsync(
            string from,
            string fromName,
            string to,
            string subject,
            string htmlContent,
            IEnumerable<EmailAttachment> attachments = null,
            string replyTo = null)
        {
            this.Sent.Enqueue(new Email { From = from, To = to, Subject = subject, Body = htmlContent, ReplyTo = replyTo });
            return Task.CompletedTask;
        }

        public class Email
        {
            public string From { get; set; }

            public string To { get; set; }

            public string Subject { get; set; }

            public string Body { get; set; }

            public string ReplyTo { get; set; }
        }
    }
}
