namespace HandyFix.Web.Services.Accounts
{
    using System;
    using System.Collections.Generic;

    using Microsoft.AspNetCore.Mvc.ApplicationModels;

    // Which pages of the Identity area can be opened at all. The Identity UI package brings
    // about thirty of its own, in its stock look: "delete my account", two-step sign-in setup,
    // an email change that waits for a confirmation nobody is sent, and more. The site uses the
    // six below. A list of what is open, not of what is closed, so a page a later version of
    // the package adds starts closed (PROJECT_STATE.md Section 3cc).
    public static class IdentityPages
    {
        public const string Area = "Identity";

        private static readonly HashSet<string> Open = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/Account/Login",
            "/Account/Logout",
            "/Account/AccessDenied",
            "/Account/ForgotPassword",
            "/Account/ResetPassword",

            // Answers 404 by itself while sign-up is closed; see RegisterModel.
            "/Account/Register",
        };

        public static bool IsOpen(string viewEnginePath)
        {
            return Open.Contains(viewEnginePath);
        }

        // A page with no route is not there: asking for it ends in the site's own 404 page.
        public static void CloseAllButOurs(PageRouteModel page)
        {
            if (!IsOpen(page.ViewEnginePath))
            {
                page.Selectors.Clear();
            }
        }
    }
}
