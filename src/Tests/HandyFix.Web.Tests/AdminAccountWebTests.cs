namespace HandyFix.Web.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Seeding;
    using HandyFix.Services.Messaging;
    using HandyFix.Web.Areas.Identity.Pages.Account;
    using HandyFix.Web.Services.Forms;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.Routing;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    using Xunit;

    // The admin account through the whole stack: signing in, the pause after wrong passwords,
    // the Account page in the admin panel, and getting back in after forgetting the password
    // (PROJECT_STATE.md Section 3cc). Each test has a site and a database of its own, because
    // each one changes the one admin account.
    public class AdminAccountWebTests
    {
        private const string LoginPage = "/Identity/Account/Login";

        private const string ForgotPasswordPage = "/Identity/Account/ForgotPassword";

        private const string ResetPasswordPage = "/Identity/Account/ResetPassword";

        private const string AccountPage = "/Administration/Account";

        private const string AdminEmail = SqliteWebApplicationFactory.AdminEmail;

        private const string AdminPassword = SqliteWebApplicationFactory.AdminPassword;

        private const string NewPassword = "nine green bottles on a wall";

        // The Identity UI package brings about thirty pages of its own. This asks the running
        // site what it will answer under /Identity, so a page a later version of the package
        // adds fails here until someone decides it should be open.
        [Fact]
        public void TheIdentityAreaServesTheSixPagesTheSiteUsesAndNoOthers()
        {
            using var site = new Site();
            site.Browser();

            string[] routes = site.Services.GetRequiredService<EndpointDataSource>().Endpoints
                .OfType<RouteEndpoint>()
                .Select(endpoint => endpoint.RoutePattern.RawText)
                .Where(route => route != null && route.StartsWith("Identity/", StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(route => route, StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(
                new[]
                {
                    "Identity/Account/AccessDenied",
                    "Identity/Account/ForgotPassword",
                    "Identity/Account/Login",
                    "Identity/Account/Logout",
                    "Identity/Account/Register",
                    "Identity/Account/ResetPassword",
                },
                routes);
        }

        // Until 2026-10-08 a signed-in admin could open all of these, "delete my account" among
        // them. Asked for now, even by the admin, each is the site's own "not found" page.
        [Theory]
        [InlineData("/Identity/Account/Manage")]
        [InlineData("/Identity/Account/Manage/ChangePassword")]
        [InlineData("/Identity/Account/Manage/Email")]
        [InlineData("/Identity/Account/Manage/DeletePersonalData")]
        [InlineData("/Identity/Account/Manage/TwoFactorAuthentication")]
        [InlineData("/Identity/Account/ForgotPasswordConfirmation")]
        [InlineData("/Identity/Account/ResendEmailConfirmation")]
        [InlineData("/Identity/Account/Lockout")]
        public async Task ThePackagesOwnAccountPagesAreNotThereEvenForTheAdmin(string address)
        {
            using var site = new Site();
            HttpClient browser = site.Browser();
            await SignInAsync(browser, AdminEmail, AdminPassword);
            Assert.True(await IsSignedInAsync(browser));

            HttpResponseMessage response = await browser.GetAsync(address);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Contains("We can't find that page.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        }

        [Fact]
        public async Task TheAccountPageIsForTheSignedInAdminAndTheSidebarLeadsToIt()
        {
            using var site = new Site();
            HttpClient browser = site.Browser();

            // Not signed in: the login page, not the account page.
            HttpResponseMessage refused = await browser.GetAsync(AccountPage);
            Assert.Equal(LoginPage, refused.RequestMessage.RequestUri.AbsolutePath);

            await SignInAsync(browser, AdminEmail, AdminPassword);

            var dashboard = await (await browser.GetAsync("/Administration/Dashboard")).Content.ReadAsStringAsync();
            Assert.Contains("href=\"" + AccountPage + "\"", dashboard);

            var account = await (await browser.GetAsync(AccountPage)).Content.ReadAsStringAsync();
            Assert.Matches("id=\"current-login-email\"[^>]*>" + Regex.Escape(AdminEmail) + "<", account);
        }

        // One message for a wrong password and for an address that is not a login at all, so the
        // login page tells a stranger nothing about which addresses are worth guessing at.
        [Theory]
        [InlineData(AdminEmail, "not the password")]
        [InlineData("nobody@example.com", AdminPassword)]
        public async Task AFailedSignInSaysTheSameWhateverWasWrong(string email, string password)
        {
            using var site = new Site();
            HttpClient browser = site.Browser();

            HttpResponseMessage response = await SignInAsync(browser, email, password);

            Assert.Equal(LoginPage, response.RequestMessage.RequestUri.AbsolutePath);
            Assert.Contains(LoginModel.FailedMessage, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
            Assert.False(await IsSignedInAsync(browser));
        }

        // Until 2026-10-08 a wrong password counted for nothing and the password could be guessed
        // at without end. Five wrong ones in a row now pause the login, for the right one too.
        [Fact]
        public async Task FiveWrongPasswordsPauseTheLoginAndItOpensAgainAfterwards()
        {
            using var site = new Site();
            HttpClient browser = site.Browser();

            for (var attempt = 1; attempt <= IdentityOptionsProvider.WrongPasswordsBeforePause; attempt++)
            {
                await SignInAsync(browser, AdminEmail, "wrong guess number " + attempt);
            }

            HttpResponseMessage whilePaused = await SignInAsync(browser, AdminEmail, AdminPassword);

            Assert.Contains(LoginModel.FailedMessage, WebUtility.HtmlDecode(await whilePaused.Content.ReadAsStringAsync()));
            Assert.False(await IsSignedInAsync(browser));

            // The pause is the five minutes the message promises, and it does end.
            await site.WithAdminAsync(async (users, admin) =>
            {
                Assert.NotNull(admin.LockoutEnd);
                Assert.InRange(
                    admin.LockoutEnd.Value - DateTimeOffset.UtcNow,
                    TimeSpan.FromMinutes(IdentityOptionsProvider.PauseMinutes - 1),
                    TimeSpan.FromMinutes(IdentityOptionsProvider.PauseMinutes));

                await users.SetLockoutEndDateAsync(admin, DateTimeOffset.UtcNow.AddSeconds(-1));
            });

            await SignInAsync(browser, AdminEmail, AdminPassword);
            Assert.True(await IsSignedInAsync(browser));
        }

        // Signing in and resetting have an allowance of their own, counted per address like the
        // forms'. Opening the login page is not a guess and does not count. This site allows three.
        [Fact]
        public async Task SigningInTooOftenIsAnsweredWithTheSitesOwnTooManyAttemptsPage()
        {
            using var site = new Site(formPosts: 3);
            HttpClient browser = site.Browser();

            for (var visit = 0; visit < 6; visit++)
            {
                Assert.Equal(HttpStatusCode.OK, (await browser.GetAsync(LoginPage)).StatusCode);
            }

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                Assert.Equal(HttpStatusCode.OK, (await SignInAsync(browser, "nobody@example.com", "guess " + attempt)).StatusCode);
            }

            HttpResponseMessage fourth = await SignInAsync(browser, "nobody@example.com", "guess 4");

            Assert.Equal(HttpStatusCode.TooManyRequests, fourth.StatusCode);
            Assert.Contains("That's a lot of tries in a short while.", WebUtility.HtmlDecode(await fourth.Content.ReadAsStringAsync()));

            // Asking for a reset link draws on the same allowance; a customer's enquiry does not.
            HttpResponseMessage reset = await FormsWebTests.PostFormAsync(browser, ForgotPasswordPage, new Dictionary<string, string> { ["Input.Email"] = AdminEmail });
            Assert.Equal(HttpStatusCode.TooManyRequests, reset.StatusCode);
            Assert.Empty(site.Emails.Sent);

            HttpResponseMessage enquiry = await FormsWebTests.PostFormAsync(browser, "/Contact", new Dictionary<string, string>
            {
                ["Name"] = "Jane Doe",
                ["Email"] = "jane.doe@example.com",
                ["PhoneNumber"] = "07700 900123",
                ["Category"] = "Plumbing",
                ["Message"] = "The login allowance is used up and this still goes through.",
            });
            Assert.Equal(HttpStatusCode.OK, enquiry.StatusCode);
        }

        [Fact]
        public async Task TheAdminChangesTheirPasswordAndStaysSignedIn()
        {
            using var site = new Site();
            HttpClient browser = site.Browser();
            await SignInAsync(browser, AdminEmail, AdminPassword);

            HttpResponseMessage changed = await PostFromAsync(browser, AccountPage, AccountPage + "/ChangePassword", new Dictionary<string, string>
            {
                ["Password.CurrentPassword"] = AdminPassword,
                ["Password.NewPassword"] = NewPassword,
                ["Password.ConfirmPassword"] = NewPassword,
            });

            // Back on the Account page, told so, and not thrown out by the change.
            Assert.Equal(AccountPage, changed.RequestMessage.RequestUri.AbsolutePath);
            Assert.Contains("Your password has been changed.", await changed.Content.ReadAsStringAsync());
            Assert.True(await IsSignedInAsync(browser));

            await SignOutAsync(browser);
            Assert.False(await IsSignedInAsync(browser));

            await SignInAsync(browser, AdminEmail, AdminPassword);
            Assert.False(await IsSignedInAsync(browser));

            await SignInAsync(browser, AdminEmail, NewPassword);
            Assert.True(await IsSignedInAsync(browser));
        }

        [Theory]
        [InlineData("not the password", NewPassword, NewPassword, "That is not your current password.")]
        [InlineData(AdminPassword, "too short", "too short", "The password must be at least 10 characters long.")]
        [InlineData(AdminPassword, NewPassword, "nine green bottles on a hill", "The two passwords are not the same.")]
        public async Task APasswordChangeThatIsRefusedSaysWhyAndChangesNothing(string current, string newPassword, string again, string reason)
        {
            using var site = new Site();
            HttpClient browser = site.Browser();
            await SignInAsync(browser, AdminEmail, AdminPassword);

            HttpResponseMessage refused = await PostFromAsync(browser, AccountPage, AccountPage + "/ChangePassword", new Dictionary<string, string>
            {
                ["Password.CurrentPassword"] = current,
                ["Password.NewPassword"] = newPassword,
                ["Password.ConfirmPassword"] = again,
            });

            // Shown as a message under a box. The page also carries each rule's words in an
            // attribute for the browser's own checks, so finding the words alone would prove nothing.
            var content = WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync());
            Assert.Matches("class=\"[^\"]*field-validation-error[^\"]*\"[^>]*>" + Regex.Escape(reason), content);

            // The form again, with the login email still shown above it.
            Assert.Contains("id=\"change-password-form\"", content);
            Assert.Matches("id=\"current-login-email\"[^>]*>" + Regex.Escape(AdminEmail) + "<", content);

            await site.WithAdminAsync(async (users, admin) => Assert.True(await users.CheckPasswordAsync(admin, AdminPassword)));
        }

        // The seeder used to look the admin up by one address written in the code, so a login
        // moved to another address would have been joined by a second admin at the next start.
        [Fact]
        public async Task TheAdminChangesTheirLoginEmailAndTheNextStartMakesNoSecondAdmin()
        {
            using var site = new Site();
            HttpClient browser = site.Browser();
            await SignInAsync(browser, AdminEmail, AdminPassword);

            HttpResponseMessage refused = await PostFromAsync(browser, AccountPage, AccountPage + "/ChangeEmail", new Dictionary<string, string>
            {
                ["Email.NewEmail"] = "new.owner@example.com",
                ["Email.CurrentPassword"] = "not the password",
            });
            var refusedContent = WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync());
            Assert.Contains("That is not your current password.", refusedContent);
            Assert.Matches("name=\"Email.NewEmail\"[^>]*value=\"new.owner@example.com\"", refusedContent);

            HttpResponseMessage changed = await PostFromAsync(browser, AccountPage, AccountPage + "/ChangeEmail", new Dictionary<string, string>
            {
                ["Email.NewEmail"] = "new.owner@example.com",
                ["Email.CurrentPassword"] = AdminPassword,
            });
            var changedContent = await changed.Content.ReadAsStringAsync();
            Assert.Contains("Your login email is now new.owner@example.com.", changedContent);
            Assert.Matches("id=\"current-login-email\"[^>]*>new\\.owner@example\\.com<", changedContent);
            Assert.True(await IsSignedInAsync(browser));

            await SignOutAsync(browser);
            await SignInAsync(browser, AdminEmail, AdminPassword);
            Assert.False(await IsSignedInAsync(browser));
            await SignInAsync(browser, "new.owner@example.com", AdminPassword);
            Assert.True(await IsSignedInAsync(browser));

            // What the application does at its next start, with the settings still naming the
            // first address.
            using (IServiceScope scope = site.Services.CreateScope())
            {
                await new ApplicationDbContextSeeder().SeedAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), scope.ServiceProvider);

                UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                ApplicationUser admin = Assert.Single(await users.GetUsersInRoleAsync(GlobalConstants.AdministratorRoleName));
                Assert.Equal("new.owner@example.com", admin.Email);
                Assert.Single(users.Users.ToList());
            }

            // A forgotten password is now reset through the new address, and not the old one.
            HttpClient stranger = site.Browser();
            await FormsWebTests.PostFormAsync(stranger, ForgotPasswordPage, new Dictionary<string, string> { ["Input.Email"] = AdminEmail });
            Assert.Empty(site.Emails.Sent);
            await FormsWebTests.PostFormAsync(stranger, ForgotPasswordPage, new Dictionary<string, string> { ["Input.Email"] = "new.owner@example.com" });
            Assert.Equal("new.owner@example.com", Assert.Single(site.Emails.Sent).To);
        }

        // "Forgot password?" used to open the Identity UI package's page, which told the owner
        // to check an inbox and sent nothing. This is the whole way back in: the request, the
        // email, the link, the new password, and a paused login opened by it.
        [Fact]
        public async Task AForgottenPasswordIsResetThroughAnEmailedLinkWhichAlsoOpensAPausedLogin()
        {
            using var site = new Site();
            HttpClient browser = site.Browser();

            for (var attempt = 1; attempt <= IdentityOptionsProvider.WrongPasswordsBeforePause; attempt++)
            {
                await SignInAsync(browser, AdminEmail, "wrong guess number " + attempt);
            }

            HttpResponseMessage asked = await FormsWebTests.PostFormAsync(browser, ForgotPasswordPage, new Dictionary<string, string> { ["Input.Email"] = AdminEmail });
            Assert.Contains("id=\"reset-link-sent\"", await asked.Content.ReadAsStringAsync());
            Assert.Equal(new[] { FormNames.ForgotPassword }, site.Turnstile.AskedAbout);

            RecordingEmailSender.Email email = Assert.Single(site.Emails.Sent);
            Assert.Equal(AdminEmail, email.To);
            Assert.Equal("Reset your Plumbing Handyman Surrey admin password", email.Subject);
            Assert.Contains("works for 2 hours", email.Body);

            var link = new Uri(WebUtility.HtmlDecode(Regex.Match(email.Body, "href=\"([^\"]+)\"").Groups[1].Value));
            Assert.Equal(ResetPasswordPage, link.AbsolutePath);

            HttpResponseMessage reset = await PostFromAsync(browser, link.PathAndQuery, ResetPasswordPage, new Dictionary<string, string>
            {
                ["Input.NewPassword"] = NewPassword,
                ["Input.ConfirmPassword"] = NewPassword,
            });

            // On the login page, told the password has been changed.
            Assert.Equal(LoginPage, reset.RequestMessage.RequestUri.AbsolutePath);
            Assert.Contains(ResetPasswordModel.ChangedMessage, await reset.Content.ReadAsStringAsync());

            await SignInAsync(browser, AdminEmail, NewPassword);
            Assert.True(await IsSignedInAsync(browser));

            // The link works once. Used again, it offers a new one and changes nothing.
            HttpClient second = site.Browser();
            HttpResponseMessage again = await PostFromAsync(second, link.PathAndQuery, ResetPasswordPage, new Dictionary<string, string>
            {
                ["Input.NewPassword"] = "somebody else's choice",
                ["Input.ConfirmPassword"] = "somebody else's choice",
            });
            var againContent = await again.Content.ReadAsStringAsync();
            Assert.Contains("id=\"reset-link-refused\"", againContent);
            Assert.Contains("href=\"" + ForgotPasswordPage + "\"", againContent);
            await site.WithAdminAsync(async (users, admin) => Assert.True(await users.CheckPasswordAsync(admin, NewPassword)));
        }

        // The answer is the same as for the admin's own address, and nothing is sent: the page
        // must not tell a stranger which addresses are logins.
        [Fact]
        public async Task AskingToResetAnAddressThatIsNotTheLoginSaysTheSameAndSendsNothing()
        {
            using var site = new Site();
            HttpClient browser = site.Browser();

            HttpResponseMessage asked = await FormsWebTests.PostFormAsync(browser, ForgotPasswordPage, new Dictionary<string, string> { ["Input.Email"] = "nobody@example.com" });

            Assert.Contains("id=\"reset-link-sent\"", await asked.Content.ReadAsStringAsync());
            Assert.Empty(site.Emails.Sent);
        }

        // The forgot-password form is as public as the customer forms and carries the same guard.
        [Fact]
        public async Task AResetRequestWithAProgramsMarkIsAnsweredTheSameAndSendsNothing()
        {
            using var site = new Site();
            HttpClient browser = site.Browser();

            var page = await (await browser.GetAsync(ForgotPasswordPage)).Content.ReadAsStringAsync();
            Assert.Contains("name=\"" + FormGuard.HoneypotFieldName + "\"", page);
            Assert.Contains("data-action=\"" + FormNames.ForgotPassword + "\"", page);

            HttpResponseMessage asked = await FormsWebTests.PostFormAsync(browser, ForgotPasswordPage, new Dictionary<string, string>
            {
                ["Input.Email"] = AdminEmail,
                [FormGuard.HoneypotFieldName] = "filled in by a program",
            });

            Assert.Contains("id=\"reset-link-sent\"", await asked.Content.ReadAsStringAsync());
            Assert.Empty(site.Emails.Sent);
        }

        [Fact]
        public async Task AResetRequestThatFailsThePersonCheckIsToldSoAndSendsNothing()
        {
            using var site = new Site();
            site.Turnstile.Passes = false;
            HttpClient browser = site.Browser();

            HttpResponseMessage asked = await FormsWebTests.PostFormAsync(browser, ForgotPasswordPage, new Dictionary<string, string> { ["Input.Email"] = AdminEmail });

            var content = WebUtility.HtmlDecode(await asked.Content.ReadAsStringAsync());
            Assert.Contains(FormNames.ChallengeFailedMessage, content);
            Assert.Contains("id=\"forgot-password-form\"", content);
            Assert.Empty(site.Emails.Sent);
        }

        // Opened without a link, or with one that was cut short in a mail program.
        [Fact]
        public async Task TheResetPageDoesNothingWithoutAGoodLink()
        {
            using var site = new Site();
            HttpClient browser = site.Browser();

            Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync(ResetPasswordPage)).StatusCode);

            string adminId = null;
            await site.WithAdminAsync((users, admin) =>
            {
                adminId = admin.Id;
                return Task.CompletedTask;
            });

            HttpResponseMessage madeUp = await PostFromAsync(browser, ResetPasswordPage + "?userId=" + adminId + "&code=bm90LWEtcmVhbC1jb2Rl", ResetPasswordPage, new Dictionary<string, string>
            {
                ["Input.NewPassword"] = NewPassword,
                ["Input.ConfirmPassword"] = NewPassword,
            });

            Assert.Contains("id=\"reset-link-refused\"", await madeUp.Content.ReadAsStringAsync());
            await site.WithAdminAsync(async (users, admin) => Assert.True(await users.CheckPasswordAsync(admin, AdminPassword)));
        }

        private static Task<HttpResponseMessage> SignInAsync(HttpClient browser, string email, string password)
        {
            return FormsWebTests.PostFormAsync(browser, LoginPage, new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = password,
            });
        }

        private static Task<HttpResponseMessage> SignOutAsync(HttpClient browser)
        {
            return PostFromAsync(browser, AccountPage, "/Identity/Account/Logout?returnUrl=%2F", new Dictionary<string, string>());
        }

        // The admin's Account page opens for a signed-in admin; anyone else is sent to the login page.
        private static async Task<bool> IsSignedInAsync(HttpClient browser)
        {
            HttpResponseMessage response = await browser.GetAsync(AccountPage);
            return response.StatusCode == HttpStatusCode.OK && response.RequestMessage.RequestUri.AbsolutePath == AccountPage;
        }

        // Sends one of the forms on a page to its own address, with the page's hidden fields
        // (the antiforgery token among them) as a browser would send them back.
        private static async Task<HttpResponseMessage> PostFromAsync(HttpClient browser, string page, string action, Dictionary<string, string> fields)
        {
            var html = await (await browser.GetAsync(page)).Content.ReadAsStringAsync();
            var form = new Dictionary<string, string>(FormsWebTests.HiddenFieldsOf(html));
            Assert.Contains("__RequestVerificationToken", form.Keys);

            foreach (KeyValuePair<string, string> field in fields)
            {
                form[field.Key] = field.Value;
            }

            return await browser.PostAsync(action, new FormUrlEncodedContent(form));
        }

        // A site of its own for one test: its own database and admin account, an email sender
        // that keeps what it is given, and a person check that passes or fails as the test says.
        private sealed class Site : IDisposable
        {
            private readonly SqliteWebApplicationFactory root = new SqliteWebApplicationFactory();
            private readonly WebApplicationFactory<Program> host;

            public Site(int? formPosts = null)
            {
                this.host = this.root.WithWebHostBuilder(builder =>
                {
                    if (formPosts != null)
                    {
                        builder.ConfigureAppConfiguration((context, configuration) => configuration.AddInMemoryCollection(
                            new Dictionary<string, string> { [RateLimits.FormPostsKey] = formPosts.Value.ToString(CultureInfo.InvariantCulture) }));
                    }

                    builder.ConfigureServices(services =>
                    {
                        services.AddSingleton<IEmailSender>(this.Emails);
                        services.AddSingleton<ITurnstileVerifier>(this.Turnstile);
                    });
                });
            }

            public RecordingEmailSender Emails { get; } = new RecordingEmailSender();

            public FakeTurnstile Turnstile { get; } = new FakeTurnstile();

            public IServiceProvider Services => this.host.Services;

            // A browser of its own: it keeps its cookies, follows redirects and asks for pages.
            public HttpClient Browser()
            {
                HttpClient client = this.host.CreateClient();
                client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
                return client;
            }

            public async Task WithAdminAsync(Func<UserManager<ApplicationUser>, ApplicationUser, Task> look)
            {
                using IServiceScope scope = this.Services.CreateScope();
                UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                ApplicationUser admin = Assert.Single(await users.GetUsersInRoleAsync(GlobalConstants.AdministratorRoleName));
                await look(users, admin);
            }

            public void Dispose()
            {
                this.host.Dispose();
                this.root.Dispose();
            }
        }
    }
}
