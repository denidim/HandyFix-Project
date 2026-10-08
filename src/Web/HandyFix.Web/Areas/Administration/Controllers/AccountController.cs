namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System.Threading.Tasks;

    using HandyFix.Web.Services.Accounts;
    using HandyFix.Web.ViewModels.Administration.Account;

    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;

    // The signed-in admin's own account: its password and its login email. Before this page
    // either could only be changed by a developer (PROJECT_STATE.md Section 3cc).
    public class AccountController : AdministrationController
    {
        private const string EmailForm = nameof(AdminAccountViewModel.Email);

        private const string PasswordForm = nameof(AdminAccountViewModel.Password);

        private readonly IAdminAccountService accounts;

        public AccountController(IAdminAccountService accounts)
        {
            this.accounts = accounts;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            return this.View(await this.PageAsync());
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword([Bind(Prefix = PasswordForm)] ChangePasswordInputModel input)
        {
            if (this.ModelState.IsValid)
            {
                IdentityResult result = await this.accounts.ChangePasswordAsync(this.User, input.CurrentPassword, input.NewPassword);
                if (result.Succeeded)
                {
                    this.TempData["SuccessMessage"] = "Your password has been changed. Use the new one the next time you sign in.";
                    return this.RedirectToAction(nameof(this.Index));
                }

                this.AddErrors(result, PasswordForm, nameof(ChangePasswordInputModel.NewPassword));
            }

            return this.View(nameof(this.Index), await this.PageAsync());
        }

        [HttpPost]
        public async Task<IActionResult> ChangeEmail([Bind(Prefix = EmailForm)] ChangeLoginEmailInputModel input)
        {
            if (this.ModelState.IsValid)
            {
                IdentityResult result = await this.accounts.ChangeLoginEmailAsync(this.User, input.NewEmail, input.CurrentPassword);
                if (result.Succeeded)
                {
                    this.TempData["SuccessMessage"] = $"Your login email is now {input.NewEmail}. Sign in with it from now on; a forgotten password is reset through it too.";
                    return this.RedirectToAction(nameof(this.Index));
                }

                this.AddErrors(result, EmailForm, nameof(ChangeLoginEmailInputModel.NewEmail));
            }

            // The address typed is shown again; a password box never is.
            AdminAccountViewModel page = await this.PageAsync();
            page.Email.NewEmail = input.NewEmail;
            return this.View(nameof(this.Index), page);
        }

        private async Task<AdminAccountViewModel> PageAsync()
        {
            return new AdminAccountViewModel { LoginEmail = await this.accounts.GetLoginEmailAsync(this.User) };
        }

        // Each of Identity's refusals goes under the box it is about, in the form it came from.
        // A wrong current password is said in the site's own words; the rest are about the new
        // value and Identity's own sentence says which rule it broke.
        private void AddErrors(IdentityResult result, string form, string newValueField)
        {
            foreach (IdentityError error in result.Errors)
            {
                if (error.Code == nameof(IdentityErrorDescriber.PasswordMismatch))
                {
                    this.ModelState.AddModelError($"{form}.{nameof(ChangePasswordInputModel.CurrentPassword)}", "That is not your current password.");
                }
                else if (error.Code == nameof(IdentityErrorDescriber.DuplicateEmail) || error.Code == nameof(IdentityErrorDescriber.DuplicateUserName))
                {
                    this.ModelState.AddModelError($"{form}.{newValueField}", "That email address is already in use on this site.");
                }
                else
                {
                    this.ModelState.AddModelError($"{form}.{newValueField}", error.Description);
                }
            }
        }
    }
}
