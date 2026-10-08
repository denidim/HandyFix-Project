namespace HandyFix.Web.Areas.Identity.Pages.Account
{
    using System.ComponentModel.DataAnnotations;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Web.Services.Accounts;
    using HandyFix.Web.Services.Forms;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.RazorPages;
    using Microsoft.AspNetCore.RateLimiting;

    // Where the link in the reset email leads: a new password, typed twice. The link carries the
    // account and a one-time code, so this page asks for nothing else (PROJECT_STATE.md Section 3cc).
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.AccountPolicy)]
    public class ResetPasswordModel : PageModel
    {
        public const string ChangedMessage = "Your password has been changed. Sign in with the new one.";

        private readonly IAdminAccountService accounts;

        public ResetPasswordModel(IAdminAccountService accounts)
        {
            this.accounts = accounts;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        // True when the link was turned down: older than its time limit, used already, or not
        // one of ours. The page then offers a new one in place of the form.
        public bool LinkRefused { get; private set; }

        public IActionResult OnGet(string userId = null, string code = null)
        {
            // Opened without a link there is nothing here to do.
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(code))
            {
                return this.NotFound();
            }

            this.Input = new InputModel { UserId = userId, Code = code };
            return this.Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!this.ModelState.IsValid)
            {
                return this.Page();
            }

            IdentityResult result = await this.accounts.ResetPasswordAsync(this.Input.UserId, this.Input.Code, this.Input.NewPassword);
            if (result.Succeeded)
            {
                this.TempData[LoginModel.StatusKey] = ChangedMessage;
                return this.RedirectToPage("./Login");
            }

            foreach (IdentityError error in result.Errors)
            {
                if (error.Code == nameof(IdentityErrorDescriber.InvalidToken))
                {
                    this.LinkRefused = true;
                }
                else
                {
                    this.ModelState.AddModelError("Input.NewPassword", error.Description);
                }
            }

            return this.Page();
        }

        public class InputModel
        {
            [Required]
            public string UserId { get; set; }

            [Required]
            public string Code { get; set; }

            [Required(ErrorMessage = "Enter a new password.")]
            [StringLength(100, MinimumLength = GlobalConstants.PasswordMinimumLength, ErrorMessage = "The password must be at least {2} characters long.")]
            [DataType(DataType.Password)]
            public string NewPassword { get; set; }

            [Required(ErrorMessage = "Type the new password again.")]
            [DataType(DataType.Password)]
            [Compare(nameof(NewPassword), ErrorMessage = "The two passwords are not the same.")]
            public string ConfirmPassword { get; set; }
        }
    }
}
