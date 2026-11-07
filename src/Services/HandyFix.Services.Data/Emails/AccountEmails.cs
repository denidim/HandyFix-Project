namespace HandyFix.Services.Data.Emails
{
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Services.Messaging;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    // Every word of the emails about the admin's own login (PROJECT_STATE.md Section 3di).
    // AdminAccountService decides who may be sent one.
    public class AccountEmails : IAccountEmails
    {
        private readonly IEmailSender emailSender;
        private readonly EmailOptions settings;
        private readonly ILogger<AccountEmails> logger;

        public AccountEmails(
            IEmailSender emailSender,
            IOptions<EmailOptions> settings,
            ILogger<AccountEmails> logger)
        {
            this.emailSender = emailSender;
            this.settings = settings.Value;
            this.logger = logger;
        }

        public async Task<bool> PasswordResetLinkAsync(string to, string link, int linkHours)
        {
            var address = EmailText.Encode(link);
            var body = EmailLayout.ForAccountSecurity(
                EmailLayout.Badge(EmailColour.Red, "&#128274; Security Notification")
                + EmailLayout.Heading("Reset your admin password")
                + EmailLayout.Lead($"A request was received to reset the password for the <strong>{GlobalConstants.SystemName}</strong> admin panel account.")
                + EmailLayout.Button("Choose a New Password &rarr;", address)
                + EmailLayout.Note("Important security note:", $"&bull; This password reset link works for <strong>{linkHours} hours</strong> and can only be used once.<br />&bull; If you did not request this reset, you can safely ignore this email. Your current password remains unchanged and secure.")
                + EmailLayout.SmallPrint($"If the button above does not work, copy and paste this link into your browser:<br />{EmailLayout.LinkInFull(address)}"));

            return await this.emailSender.TrySendEmailAsync(
                this.logger,
                "password reset link, to the admin",
                this.settings.SystemFromAddress,
                EmailOptions.WebsiteFromName,
                to,
                $"Reset your {GlobalConstants.SystemName} admin password",
                body);
        }
    }
}
