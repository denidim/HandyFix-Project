using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using HandyFix.Data;
using HandyFix.Data.Models;
using HandyFix.Web.Services.Forms;

namespace HandyFix.Web.Areas.Identity.Pages.Account
{
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

        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(SignInManager<ApplicationUser> signInManager, ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        [TempData(Key = StatusKey)]
        public string StatusMessage { get; set; }

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

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                // A wrong password counts towards the pause (IdentityOptionsProvider). Until
                // 2026-10-08 it did not, and the password could be guessed at without limit.
                Microsoft.AspNetCore.Identity.SignInResult result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);
                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");
                    return LocalRedirect(returnUrl);
                }

                if (result.IsLockedOut)
                {
                    _logger.LogWarning("A sign-in was refused: the account's login is paused after too many wrong passwords.");
                }

                ModelState.AddModelError(string.Empty, FailedMessage);
                return Page();
            }

            // If we got this far, something failed, redisplay form
            return Page();
        }
    }
}
