namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Globalization;

    using HandyFix.Common;

    using Xunit;

    // The moment a booking or an enquiry came in is saved in UTC and shown to the admin as a
    // clock in the UK shows it (PROJECT_STATE.md Section 3ce). The admin pages printed it as
    // saved: an hour early from late March to late October.
    public class UkTimeTests
    {
        [Theory]
        [InlineData("2026-07-01T12:00:00", "2026-07-01T13:00:00")] // summer: an hour ahead of UTC
        [InlineData("2026-01-15T12:00:00", "2026-01-15T12:00:00")] // winter: the same as UTC
        [InlineData("2026-10-08T23:13:00", "2026-10-09T00:13:00")] // just after midnight: it showed the day before
        [InlineData("2026-10-25T00:30:00", "2026-10-25T01:30:00")] // the last half hour of summer time
        [InlineData("2026-10-25T01:30:00", "2026-10-25T01:30:00")] // after the clocks go back
        public void FromUtcShouldGiveTheTimeOnAClockInTheUk(string savedAs, string shownAs)
        {
            // As it comes back from the database: a time with no zone attached.
            DateTime saved = DateTime.Parse(savedAs, CultureInfo.InvariantCulture);

            Assert.Equal(DateTime.Parse(shownAs, CultureInfo.InvariantCulture), UkTime.FromUtc(saved));
        }
    }
}
