namespace HandyFix.Web.Tests.Controllers
{
    using System.Security.Claims;
    using System.Threading.Tasks;

    using HandyFix.Web.Areas.Administration.Controllers;
    using HandyFix.Web.Services.Accounts;
    using HandyFix.Web.ViewModels.Administration.Account;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;

    using Moq;

    using Xunit;

    // Controller-level wiring only: which box each refusal is shown under, and what the admin is
    // sent back to. The account changes themselves run through the whole stack in
    // AdminAccountWebTests (PROJECT_STATE.md Section 3cc).
    public class AdminAccountControllerTests
    {
        private static readonly IdentityErrorDescriber Describe = new IdentityErrorDescriber();

        [Fact]
        public async Task IndexShouldShowTheLoginEmailAndTwoEmptyForms()
        {
            AccountController controller = BuildController(out _);

            var result = Assert.IsType<ViewResult>(await controller.Index());

            var page = Assert.IsType<AdminAccountViewModel>(result.Model);
            Assert.Equal("owner@example.com", page.LoginEmail);
            Assert.Null(page.Email.NewEmail);
            Assert.Null(page.Password.NewPassword);
        }

        [Fact]
        public async Task ChangePasswordShouldNotReachTheAccountWhenTheFormIsInvalid()
        {
            AccountController controller = BuildController(out var accounts);
            controller.ModelState.AddModelError("Password.NewPassword", "The password must be at least 10 characters long.");

            var result = Assert.IsType<ViewResult>(await controller.ChangePassword(new ChangePasswordInputModel()));

            Assert.Equal("Index", result.ViewName);
            accounts.Verify(x => x.ChangePasswordAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ChangePasswordShouldPutAWrongCurrentPasswordUnderItsOwnBox()
        {
            AccountController controller = BuildController(out var accounts);
            accounts
                .Setup(x => x.ChangePasswordAsync(It.IsAny<ClaimsPrincipal>(), "not the password", "nine green bottles"))
                .ReturnsAsync(IdentityResult.Failed(Describe.PasswordMismatch()));

            var result = Assert.IsType<ViewResult>(await controller.ChangePassword(
                new ChangePasswordInputModel { CurrentPassword = "not the password", NewPassword = "nine green bottles", ConfirmPassword = "nine green bottles" }));

            Assert.Equal("Index", result.ViewName);
            Assert.Equal("That is not your current password.", Assert.Single(controller.ModelState["Password.CurrentPassword"].Errors).ErrorMessage);
            Assert.False(controller.ModelState.ContainsKey("Password.NewPassword"));
            Assert.False(controller.TempData.ContainsKey("SuccessMessage"));

            // The page it goes back to still shows who is signed in.
            Assert.Equal("owner@example.com", Assert.IsType<AdminAccountViewModel>(result.Model).LoginEmail);
        }

        [Fact]
        public async Task ChangePasswordShouldPutAnyOtherRefusalUnderTheNewPassword()
        {
            AccountController controller = BuildController(out var accounts);
            accounts
                .Setup(x => x.ChangePasswordAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(Describe.PasswordTooShort(10)));

            await controller.ChangePassword(new ChangePasswordInputModel { CurrentPassword = "correct horse", NewPassword = "short", ConfirmPassword = "short" });

            Assert.Equal(Describe.PasswordTooShort(10).Description, Assert.Single(controller.ModelState["Password.NewPassword"].Errors).ErrorMessage);
        }

        [Fact]
        public async Task ChangePasswordShouldGoBackToTheAccountPageAndSaySoWhenItWorked()
        {
            AccountController controller = BuildController(out var accounts);
            accounts
                .Setup(x => x.ChangePasswordAsync(It.IsAny<ClaimsPrincipal>(), "correct horse", "nine green bottles"))
                .ReturnsAsync(IdentityResult.Success);

            var result = Assert.IsType<RedirectToActionResult>(await controller.ChangePassword(
                new ChangePasswordInputModel { CurrentPassword = "correct horse", NewPassword = "nine green bottles", ConfirmPassword = "nine green bottles" }));

            Assert.Equal("Index", result.ActionName);
            Assert.StartsWith("Your password has been changed.", (string)controller.TempData["SuccessMessage"]);
        }

        [Fact]
        public async Task ChangeEmailShouldPutAWrongPasswordUnderTheEmailFormsOwnPasswordBox()
        {
            AccountController controller = BuildController(out var accounts);
            accounts
                .Setup(x => x.ChangeLoginEmailAsync(It.IsAny<ClaimsPrincipal>(), "new.owner@example.com", "not the password"))
                .ReturnsAsync(IdentityResult.Failed(Describe.PasswordMismatch()));

            var result = Assert.IsType<ViewResult>(await controller.ChangeEmail(
                new ChangeLoginEmailInputModel { NewEmail = "new.owner@example.com", CurrentPassword = "not the password" }));

            // Under the email form's password box, not the password form's.
            Assert.Equal("That is not your current password.", Assert.Single(controller.ModelState["Email.CurrentPassword"].Errors).ErrorMessage);
            Assert.False(controller.ModelState.ContainsKey("Password.CurrentPassword"));

            // What was typed is still in the box; the login email shown is still the old one.
            var page = Assert.IsType<AdminAccountViewModel>(result.Model);
            Assert.Equal("new.owner@example.com", page.Email.NewEmail);
            Assert.Equal("owner@example.com", page.LoginEmail);
        }

        [Fact]
        public async Task ChangeEmailShouldSayAnAddressAlreadyInUseIsInUse()
        {
            AccountController controller = BuildController(out var accounts);
            accounts
                .Setup(x => x.ChangeLoginEmailAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(Describe.DuplicateEmail("taken@example.com")));

            await controller.ChangeEmail(new ChangeLoginEmailInputModel { NewEmail = "taken@example.com", CurrentPassword = "correct horse" });

            Assert.Equal("That email address is already in use on this site.", Assert.Single(controller.ModelState["Email.NewEmail"].Errors).ErrorMessage);
        }

        [Fact]
        public async Task ChangeEmailShouldGoBackToTheAccountPageAndNameTheNewAddressWhenItWorked()
        {
            AccountController controller = BuildController(out var accounts);
            accounts
                .Setup(x => x.ChangeLoginEmailAsync(It.IsAny<ClaimsPrincipal>(), "new.owner@example.com", "correct horse"))
                .ReturnsAsync(IdentityResult.Success);

            var result = Assert.IsType<RedirectToActionResult>(await controller.ChangeEmail(
                new ChangeLoginEmailInputModel { NewEmail = "new.owner@example.com", CurrentPassword = "correct horse" }));

            Assert.Equal("Index", result.ActionName);
            Assert.StartsWith("Your login email is now new.owner@example.com.", (string)controller.TempData["SuccessMessage"]);
        }

        private static AccountController BuildController(out Mock<IAdminAccountService> accounts)
        {
            accounts = new Mock<IAdminAccountService>();
            accounts.Setup(x => x.GetLoginEmailAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync("owner@example.com");

            return new AccountController(accounts.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
            };
        }
    }
}
