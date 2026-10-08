namespace HandyFix.Services.Messaging
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IEmailSender
    {
        // replyTo is where a reply goes when it should not go back to the sender: a notice to the
        // company about a customer carries the customer's address, so pressing Reply answers them.
        Task SendEmailAsync(
            string from,
            string fromName,
            string to,
            string subject,
            string htmlContent,
            IEnumerable<EmailAttachment> attachments = null,
            string replyTo = null);
    }
}
