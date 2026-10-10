namespace HandyFix.Web.Services.Accounts
{
    using System;
    using System.Security.Claims;
    using System.Text;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data.Models;
    using HandyFix.Services.Data.Common;
    using HandyFix.Services.Messaging;

    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.WebUtilities;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class AdminAccountService : IAdminAccountService
    {
        // How long a reset link works. Program.cs gives the same figure to Identity, which is
        // what enforces it; here it is for the words in the email.
        public const int ResetLinkHours = 2;

        private readonly UserManager<ApplicationUser> userManager;
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly IEmailSender emailSender;
        private readonly IConfiguration configuration;
        private readonly ILogger<AdminAccountService> logger;

        public AdminAccountService(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailSender emailSender,
            IConfiguration configuration,
            ILogger<AdminAccountService> logger)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.emailSender = emailSender;
            this.configuration = configuration;
            this.logger = logger;
        }

        public async Task<string> GetLoginEmailAsync(ClaimsPrincipal admin)
        {
            ApplicationUser user = await this.userManager.GetUserAsync(admin);
            return user?.Email;
        }

        public async Task<IdentityResult> ChangePasswordAsync(ClaimsPrincipal admin, string currentPassword, string newPassword)
        {
            ApplicationUser user = await this.userManager.GetUserAsync(admin);
            if (user == null)
            {
                return IdentityResult.Failed(this.userManager.ErrorDescriber.DefaultError());
            }

            IdentityResult result = await this.userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (result.Succeeded)
            {
                // A new password makes every sign-in made with the old one invalid, this one
                // included. Signing this browser in again keeps the admin where they are.
                await this.signInManager.RefreshSignInAsync(user);
                this.logger.LogInformation("The admin password was changed in the admin panel.");
            }

            return result;
        }

        public async Task<IdentityResult> ChangeLoginEmailAsync(ClaimsPrincipal admin, string newEmail, string currentPassword)
        {
            ApplicationUser user = await this.userManager.GetUserAsync(admin);
            if (user == null)
            {
                return IdentityResult.Failed(this.userManager.ErrorDescriber.DefaultError());
            }

            if (!await this.userManager.CheckPasswordAsync(user, currentPassword))
            {
                return IdentityResult.Failed(this.userManager.ErrorDescriber.PasswordMismatch());
            }

            newEmail = newEmail.Trim();

            // Asked before anything is changed, so a refusal leaves the account exactly as it was.
            ApplicationUser holder = await this.userManager.FindByNameAsync(newEmail) ?? await this.userManager.FindByEmailAsync(newEmail);
            if (holder != null && holder.Id != user.Id)
            {
                return IdentityResult.Failed(this.userManager.ErrorDescriber.DuplicateEmail(newEmail));
            }

            // The login page signs in by user name, so the two are kept the same. One save, which
            // also makes every other sign-in to the account invalid.
            user.UserName = newEmail;
            user.Email = newEmail;
            user.EmailConfirmed = true;

            IdentityResult result = await this.userManager.UpdateSecurityStampAsync(user);
            if (result.Succeeded)
            {
                await this.signInManager.RefreshSignInAsync(user);
                this.logger.LogInformation("The admin login email was changed in the admin panel.");
            }

            return result;
        }

        public async Task SendPasswordResetLinkAsync(string email, Func<string, string, string> linkFor)
        {
            ApplicationUser user = string.IsNullOrWhiteSpace(email) ? null : await this.userManager.FindByEmailAsync(email.Trim());
            if (user == null || !await this.userManager.IsInRoleAsync(user, GlobalConstants.AdministratorRoleName))
            {
                // Without the address: the log says it happened, not who was asked about.
                this.logger.LogInformation("A password reset was asked for an address that is not an administrator's login. Nothing was sent.");
                return;
            }

            var token = await this.userManager.GeneratePasswordResetTokenAsync(user);
            var link = linkFor(user.Id, WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token)));

            var address = EmailText.Encode(link);
            var body = EmailLayout.ForAccountSecurity(
                EmailLayout.Badge(EmailColour.Red, "&#128274; Security Notification")
                + EmailLayout.Heading("Reset your admin password")
                + EmailLayout.Lead($"A request was received to reset the password for the <strong>{GlobalConstants.SystemName}</strong> admin panel account.")
                + EmailLayout.Button("Choose a New Password &rarr;", address)
                + EmailLayout.Note("Important security note:", $"&bull; This password reset link works for <strong>{ResetLinkHours} hours</strong> and can only be used once.<br />&bull; If you did not request this reset, you can safely ignore this email. Your current password remains unchanged and secure.")
                + EmailLayout.SmallPrint($"If the button above does not work, copy and paste this link into your browser:<br />{EmailLayout.LinkInFull(address)}"));

            await this.emailSender.TrySendEmailAsync(
                this.logger,
                "password reset link, to the admin",
                EmailSettings.SystemFromAddress(this.configuration),
                EmailSettings.WebsiteFromName,
                user.Email,
                $"Reset your {GlobalConstants.SystemName} admin password",
                body);
        }

        public async Task<IdentityResult> ResetPasswordAsync(string userId, string code, string newPassword)
        {
            ApplicationUser user = string.IsNullOrWhiteSpace(userId) ? null : await this.userManager.FindByIdAsync(userId);
            var token = DecodeToken(code);
            if (user == null || token == null)
            {
                return IdentityResult.Failed(this.userManager.ErrorDescriber.InvalidToken());
            }

            IdentityResult result = await this.userManager.ResetPasswordAsync(user, token, newPassword);
            if (result.Succeeded)
            {
                // The link proved who is asking, so a login paused by wrong passwords opens again:
                // "forgot password" has to be the way out of that pause, not a second wait.
                await this.userManager.SetLockoutEndDateAsync(user, null);
                await this.userManager.ResetAccessFailedCountAsync(user);
                this.logger.LogInformation("The admin password was reset through an emailed link.");
            }

            return result;
        }

        // Null when the code is not one of ours: cut short in a mail program, or made up.
        private static string DecodeToken(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return null;
            }

            try
            {
                return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
