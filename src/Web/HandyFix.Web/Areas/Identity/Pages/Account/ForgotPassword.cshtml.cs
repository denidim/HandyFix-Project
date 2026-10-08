namespace HandyFix.Web.Areas.Identity.Pages.Account
{
    using System.ComponentModel.DataAnnotations;
    using System.Threading.Tasks;

    using HandyFix.Web.Services.Accounts;
    using HandyFix.Web.Services.Forms;
    using HandyFix.Web.ViewModels.Validation;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.AspNetCore.RateLimiting;

    // "Forgot password?" on the login page. Until 2026-10-08 that link opened the Identity UI
    // package's own page, whose email went through a sender that sends nothing, so the owner
    // was told to check an inbox no email was ever going to reach (PROJECT_STATE.md Section 3cc).
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.AccountPolicy)]
    public class ForgotPasswordModel : PageModel
    {
        private readonly IAdminAccountService accounts;
        private readonly IFormGuard formGuard;

        public ForgotPasswordModel(IAdminAccountService accounts, IFormGuard formGuard)
        {
            this.accounts = accounts;
            this.formGuard = formGuard;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        // True once a request has been taken: the page then says what happens next in place of
        // the form. It is in the address, so it says nothing about whether an email was sent.
        [BindProperty(SupportsGet = true)]
        public bool Sent { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!this.ModelState.IsValid)
            {
                return this.Page();
            }

            // The same guard as the three customer forms: this one is as public as they are, and
            // each send is an email to the owner.
            FormGuardResult guard = await this.formGuard.CheckAsync(this.HttpContext, FormNames.ForgotPassword);
            if (guard == FormGuardResult.ChallengeFailed)
            {
                this.ModelState.AddModelError(string.Empty, FormNames.ChallengeFailedMessage);
                return this.Page();
            }

            // A submission that carries a program's mark gets the answer a person gets, and no email.
            if (guard == FormGuardResult.Passed)
            {
                await this.accounts.SendPasswordResetLinkAsync(this.Input.Email, this.ResetLink);
            }

            // The same answer whether or not the address is a login.
            return this.RedirectToPage(new { sent = true });
        }

        private string ResetLink(string userId, string code)
        {
            return this.Url.Page(
                "./ResetPassword",
                pageHandler: null,
                values: new { area = IdentityPages.Area, userId, code },
                protocol: this.Request.Scheme);
        }

        public class InputModel
        {
            [Required(ErrorMessage = "Enter the email you sign in with.")]
            [StrictEmail]
            public string Email { get; set; }
        }
    }
}
