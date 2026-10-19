namespace HandyFix.Web
{
    using Microsoft.Extensions.Hosting;

    // What tells a copy of the site from the live one at a glance: a strip across the top of every
    // page, the admin panel's too, and a mark in front of the tab's title. Staging looks exactly like
    // the live site and asks for a password too, and a test booking meant for one was made on the
    // other (PROJECT_STATE.md Section 3cq). The live site, Production, shows neither.
    public static class EnvironmentMark
    {
        // The words are the developer's own, for staging; a developer's machine names itself.
        public static string Strip(IHostEnvironment environment)
        {
            return environment.IsProduction()
                ? null
                : $"TEST SITE ({environment.EnvironmentName.ToLowerInvariant()}). Bookings, payments and emails here are not real.";
        }

        public static string TitlePrefix(IHostEnvironment environment)
        {
            return environment.IsProduction()
                ? string.Empty
                : $"[{environment.EnvironmentName.ToUpperInvariant()}] ";
        }
    }
}
