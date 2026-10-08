namespace HandyFix.Web.Services.Accounts
{
    using System;
    using System.Security.Claims;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Identity;

    // What the owner can do to the admin account without a developer: change its password and
    // its login email while signed in, and get back in after forgetting the password
    // (PROJECT_STATE.md Section 3cc).
    public interface IAdminAccountService
    {
        Task<string> GetLoginEmailAsync(ClaimsPrincipal admin);

        Task<IdentityResult> ChangePasswordAsync(ClaimsPrincipal admin, string currentPassword, string newPassword);

        // The current password is asked for again: a panel left open on a shared computer must
        // not be enough to move the login, and with it the password reset, to another mailbox.
        Task<IdentityResult> ChangeLoginEmailAsync(ClaimsPrincipal admin, string newEmail, string currentPassword);

        // Emails a reset link to this address if it is an administrator's login, and says nothing
        // either way, so the caller cannot tell a stranger which addresses are logins. "linkFor"
        // turns the account's id and a one-time code into the address of the reset page.
        Task SendPasswordResetLinkAsync(string email, Func<string, string, string> linkFor);

        Task<IdentityResult> ResetPasswordAsync(string userId, string code, string newPassword);
    }
}
