namespace HandyFix.Services.Data.Common
{
    using System;
    using System.Globalization;

    using HandyFix.Data.Models;

    // The lines at the foot of a job's page: booked, paid, technician picked, moved, cancelled,
    // done (PROJECT_STATE.md Section 3ce). Whatever changes a job adds one to the job it has
    // already loaded, so the line is saved by the same save as the change it reports and neither
    // can land without the other.
    public static class JobHistory
    {
        // When it happened is filled in as it is saved.
        public static BookingHistoryEntry Line(string text)
        {
            return new BookingHistoryEntry { Text = text };
        }

        // Written the same on every machine: a server set to another language would otherwise
        // put a comma where the pence start, or its own words for the days.
        public static string Pounds(decimal amount)
        {
            return "£" + amount.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static string DayAndHour(DateTime time)
        {
            return time.ToString("ddd d MMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
