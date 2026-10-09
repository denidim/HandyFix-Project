namespace HandyFix.Common
{
    using System;

    public static class UkTime
    {
        private static readonly TimeZoneInfo Zone = FindZone();

        // A moment the site saved (when a booking or an enquiry came in), as a clock in the UK
        // shows it. Those moments are saved in UTC. Printed as they were saved they read an hour
        // early all summer, and a booking made just after midnight showed the day before; the
        // one page that converted them used the server's own clock, which is UTC on the servers
        // and UK time only on a developer's machine (PROJECT_STATE.md Section 3ce).
        public static DateTime FromUtc(DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
        }

        // The zone goes by one name on Linux and another on Windows. A machine that knows
        // neither shows the time as it was saved, which is what every page did before, and not
        // an error page.
        private static TimeZoneInfo FindZone()
        {
            foreach (var id in new[] { "Europe/London", "GMT Standard Time" })
            {
                if (TimeZoneInfo.TryFindSystemTimeZoneById(id, out TimeZoneInfo zone))
                {
                    return zone;
                }
            }

            return TimeZoneInfo.Utc;
        }
    }
}
