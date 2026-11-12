namespace HandyFix.Services.Data.Bookings
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Web.ViewModels.Administration.Dashboard;
    using HandyFix.Web.ViewModels.Booking;

    public interface IBookingsService
    {
        /// <summary>
        /// Makes a website booking, waiting for its deposit, with the photos already stored, and
        /// takes its slot. The result says whether it was made or why not (the slot is not in
        /// the calendar, or was taken first); when it was not, nothing is saved.
        /// </summary>
        Task<BookingCreateResult> CreateBookingAsync(
            BookingInputModel model,
            IReadOnlyList<string> imageUrls);

        Task<T> GetByIdAsync<T>(Guid id);

        Task<IEnumerable<T>> GetAllBookingsAsync<T>(
            BookingSortField sortField = BookingSortField.CreatedOn,
            bool descending = true,
            string statusFilter = null);

        /// <summary>
        /// The jobs that are on or done on one day, for the admin calendar.
        /// </summary>
        Task<IEnumerable<T>> GetJobsForDayAsync<T>(DateTime day);

        /// <summary>
        /// The nearest day after this one that <see cref="GetJobsForDayAsync"/> lists anything
        /// for, however far away, or null when there is none. The calendar's arrow leads there.
        /// </summary>
        Task<DateTime?> GetNextDayWithJobsAsync(DateTime day);

        /// <summary>
        /// The nearest day before this one that <see cref="GetJobsForDayAsync"/> lists anything
        /// for, or null when there is none.
        /// </summary>
        Task<DateTime?> GetPreviousDayWithJobsAsync(DateTime day);

        Task<int> ReleaseAbandonedBookingsAsync(TimeSpan olderThan);

        Task<int> GetTotalCountAsync();

        // How many jobs made since then gave each answer to "How did you hear about us?",
        // cancelled ones too (PROJECT_STATE.md Section 3ej).
        Task<IReadOnlyList<HeardAboutCountViewModel>> GetHeardAboutCountsAsync(DateTime since);

        Task<int> GetPendingCountAsync();

        BookingSummaryStats GetSummaryStats(IEnumerable<BookingDetailsViewModel> bookings);

        Task<IEnumerable<string>> GetStatusOptionsAsync();
    }
}
