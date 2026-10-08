namespace HandyFix.Data
{
    using System;

    using HandyFix.Common;

    using Microsoft.AspNetCore.Identity;

    public static class IdentityOptionsProvider
    {
        // After this many wrong passwords in a row an account's login pauses, for this long. The
        // login page tells the visitor both numbers (PROJECT_STATE.md Section 3cc).
        public const int WrongPasswordsBeforePause = 5;

        public const int PauseMinutes = 5;

        public static void GetIdentityOptions(IdentityOptions options)
        {
            // Length is what makes a password hard to guess, so that is the one rule: a long
            // phrase passes, where "Aa1!aa" met every character rule and fell to a word list.
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = GlobalConstants.PasswordMinimumLength;

            options.Lockout.MaxFailedAccessAttempts = WrongPasswordsBeforePause;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(PauseMinutes);
            options.Lockout.AllowedForNewUsers = true;
        }
    }
}
