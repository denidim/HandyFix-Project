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
        // How much of a name, a number or an address a "Details changed" line keeps.
        public const int ValueRoom = 80;

        // What a line can hold: BookingHistoryEntry.Text is 700 characters in the database.
        public const int LineRoom = 700;

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

        // One sentence of a "Details changed" line: what a box held before it was put right
        // (PROJECT_STATE.md Section 3cf). The page shows what the box holds now, so the line
        // keeps what it held: a correction that was itself wrong can be undone from the history.
        public static string Was(string label, string oldValue, string whenEmpty, int room = ValueRoom)
        {
            return string.IsNullOrWhiteSpace(oldValue)
                ? $"{label} was {whenEmpty}."
                : $"{label} was {Shorten(oldValue, room)}.";
        }

        // Text on one line and no longer than there is room for. A history line holds 700
        // characters (BookingHistoryEntry), and a job's description alone may be 3000.
        public static string Shorten(string text, int room)
        {
            var oneLine = string.Join(" ", (text ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries));

            return oneLine.Length <= room
                ? oneLine
                : oneLine.Substring(0, Math.Max(0, room - 1)).TrimEnd() + "…";
        }
    }
}
