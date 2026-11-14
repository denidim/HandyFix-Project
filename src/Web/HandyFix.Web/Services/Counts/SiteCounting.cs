namespace HandyFix.Web.Services.Counts
{
    using System;
    using System.Linq;

    using HandyFix.Data.Models;

    using Microsoft.AspNetCore.Http;

    // The rules of the website's count of what its visitors do (PROJECT_STATE.md Section 3fa):
    // which of the page script's words is which count, whose browser is not a visitor's, and
    // the mark that keeps one of the business's own browsers out of the figures.
    public static class SiteCounting
    {
        // The mark is a cookie the admin panel sets, in the business's own browsers only. A
        // visitor's browser never gets it, and the page script neither writes nor reads anything
        // in a visitor's browser.
        public const string BrowserCookie = "HandyFix.CountThisBrowser";

        public const string Counted = "yes";

        public const string NotCounted = "no";

        // The longest a browser keeps a cookie. The admin panel sets the mark again when it is
        // gone.
        private static readonly TimeSpan MarkLifetime = TimeSpan.FromDays(400);

        // What a program that reads pages calls itself. Search engines, link previews, speed
        // tests and browsers driven by a script (this project's own checks among them) all name
        // themselves in one of these ways.
        private static readonly string[] RobotMarks =
        {
            "bot", "crawl", "spider", "slurp", "headless", "lighthouse", "pagespeed", "facebookexternalhit", "preview",
        };

        // The words the page script sends (wwwroot/js/site.js). A visit is kept as one of two
        // counts, by the kind of device it came from; anything else is not a count.
        public static SiteCountKind? KindOf(string what, string userAgent)
        {
            return what switch
            {
                "visit" => IsPhone(userAgent) ? SiteCountKind.VisitOnPhone : SiteCountKind.VisitOnComputer,
                "booking" => SiteCountKind.BookingOpened,
                "time" => SiteCountKind.TimePicked,
                "call" => SiteCountKind.CallTapped,
                "whatsapp" => SiteCountKind.WhatsAppTapped,
                _ => null,
            };
        }

        // A browser that does not say what it is counts as a robot too: every real one does.
        public static bool IsRobot(string userAgent)
        {
            return string.IsNullOrWhiteSpace(userAgent)
                || RobotMarks.Any(mark => userAgent.Contains(mark, StringComparison.OrdinalIgnoreCase));
        }

        // Phones and Android tablets say "Mobi" or "Android". An iPad says it is a Mac, and is
        // counted as a computer.
        public static bool IsPhone(string userAgent)
        {
            return userAgent != null
                && (userAgent.Contains("Mobi", StringComparison.OrdinalIgnoreCase)
                    || userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase)
                    || userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase));
        }

        // Whose doings are counted: a person's browser, on this site's own pages, that is not
        // one of the business's. Signed in to the admin panel, or marked by it, is the
        // business's. A browser says "same-origin" when the request comes from a page of this
        // site; one too old to say anything is believed.
        public static bool IsAVisitor(HttpContext httpContext)
        {
            HttpRequest request = httpContext.Request;
            string from = request.Headers["Sec-Fetch-Site"].ToString();

            return !IsRobot(request.Headers.UserAgent.ToString())
                && httpContext.User?.Identity?.IsAuthenticated != true
                && !IsMarkedNotCounted(request)
                && (from.Length == 0 || from == "same-origin");
        }

        public static bool IsMarkedNotCounted(HttpRequest request)
        {
            return request.Cookies[BrowserCookie] == NotCounted;
        }

        // Essential, so it is written before the cookie notice is answered: it is the business's
        // own setting, in the business's own browser, as the login cookie is.
        public static void MarkBrowser(HttpContext httpContext, bool counted)
        {
            httpContext.Response.Cookies.Append(
                BrowserCookie,
                counted ? Counted : NotCounted,
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.Add(MarkLifetime),
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = httpContext.Request.IsHttps,
                });
        }
    }
}
