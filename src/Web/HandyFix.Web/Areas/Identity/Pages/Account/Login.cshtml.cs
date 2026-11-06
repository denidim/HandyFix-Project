namespace HandyFix.Web.Areas.Identity.Pages.Account
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Web.Services.Forms;

    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.AspNetCore.RateLimiting;
    using Microsoft.Extensions.Logging;

    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.AccountPolicy)]
    public class LoginModel : PageModel
    {
        // Where the reset page leaves its "your password has been changed" for this page.
        public const string StatusKey = "LoginStatus";

        // Said after every sign-in that fails, whatever the reason: a wrong password, an address
        // that is not a login, a login paused after too many wrong tries. One message for all
        // three tells a stranger nothing about which addresses are logins, and tells the owner
        // the rule he may just have run into (PROJECT_STATE.md Section 3cc).
        public static readonly string FailedMessage =
            $"That email and password did not match. After {IdentityOptionsProvider.WrongPasswordsBeforePause} wrong tries in a row the login pauses for {IdentityOptionsProvider.PauseMinutes} minutes.";

        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly ILogger<LoginModel> logger;

        public LoginModel(SignInManager<ApplicationUser> signInManager, ILogger<LoginModel> logger)
        {
            this.signInManager = signInManager;
            this.logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        [TempData(Key = StatusKey)]
        public string StatusMessage { get; set; }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(this.ErrorMessage))
            {
                this.ModelState.AddModelError(string.Empty, this.ErrorMessage);
            }

            returnUrl ??= this.Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await this.HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            this.ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= this.Url.Content("~/");

            if (this.ModelState.IsValid)
            {
                // A wrong password counts towards the pause (IdentityOptionsProvider). Until
                // 2026-10-08 it did not, and the password could be guessed at without limit.
                Microsoft.AspNetCore.Identity.SignInResult result = await this.signInManager.PasswordSignInAsync(this.Input.Email, this.Input.Password, this.Input.RememberMe, lockoutOnFailure: true);
                if (result.Succeeded)
                {
                    this.logger.LogInformation("User logged in.");
                    return this.LocalRedirect(returnUrl);
                }

                if (result.IsLockedOut)
                {
                    this.logger.LogWarning("A sign-in was refused: the account's login is paused after too many wrong passwords.");
                }

                this.ModelState.AddModelError(string.Empty, FailedMessage);
                return this.Page();
            }

            // If we got this far, something failed, redisplay form
            return this.Page();
        }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }
    }
}
