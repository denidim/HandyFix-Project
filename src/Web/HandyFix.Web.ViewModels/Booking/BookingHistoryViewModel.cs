namespace HandyFix.Web.ViewModels.Booking
{
    using System;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    /// <summary>
    /// One line of a job's history: when, and what happened.
    /// </summary>
    public class BookingHistoryViewModel : IMapFrom<BookingHistoryEntry>
    {
        public DateTime CreatedOn { get; set; }

        public string Text { get; set; }
    }
}
