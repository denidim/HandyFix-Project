namespace HandyFix.Services.Data.Bookings
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data.Models;
    using HandyFix.Web.ViewModels.Booking;

    public interface IBookingsService
    {
        Task<Booking> CreateBookingAsync(
            BookingInputModel model,
            IReadOnlyList<string> imageUrls,
            string userId = null);

        /// <summary>
        /// A job the admin writes in by hand: booked from the start, with no deposit, no slot
        /// claimed and no email. Returns the new job's id.
        /// </summary>
        Task<Guid> CreateWrittenInJobAsync(JobInputModel model);

        Task<T> GetByIdAsync<T>(Guid id);

        Task<IEnumerable<T>> GetAllBookingsAsync<T>(
            BookingSortField sortField = BookingSortField.CreatedOn,
            bool descending = true,
            string statusFilter = null);

        Task<IEnumerable<T>> GetUserBookingsAsync<T>(string userId);

        /// <summary>
        /// Marks a job done and writes its final price on it. False, with nothing changed, when
        /// the job is missing, the price is not one, or <see cref="BookingRules.CanComplete"/>
        /// says no.
        /// </summary>
        Task<bool> CompleteBookingAsync(Guid bookingId, decimal finalPrice);

        /// <summary>
        /// Puts right the final price of a job that is done.
        /// </summary>
        Task<bool> ChangeFinalPriceAsync(Guid bookingId, decimal finalPrice);

        /// <summary>
        /// Sets or clears a booking's technician and, when a new one is set, emails the customer
        /// that technician's name and number. The result says which of those happened.
        /// </summary>
        Task<TechnicianAssignmentResult> AssignTechnicianAsync(Guid bookingId, Guid? technicianId);

        /// <summary>
        /// Cancels a job, keeps the reason and the day it was for, and frees its slot. False,
        /// with nothing changed, when the job is missing, no reason is given or
        /// <see cref="BookingRules.CanCancel"/> says no.
        /// </summary>
        Task<bool> CancelBookingAsync(Guid bookingId, string reason);

        /// <summary>
        /// Moves a job to another day or hour. A website booking gives its hour in the calendar
        /// back and takes the new one where it is free; the result says what happened to both.
        /// </summary>
        Task<JobMoveResult> MoveBookingAsync(Guid bookingId, DateTime newStart);

        /// <summary>
        /// Puts a job's details right: the name, the phone number, the email, the address, the
        /// service and what the job is, and for a written-in job where it came from. Not its
        /// day and time, which <see cref="MoveBookingAsync"/> changes. Nothing is emailed. The
        /// result says what changed, or why nothing did; <see cref="BookingRules.CanEditDetails"/>
        /// decides whether the job may be edited at all.
        /// </summary>
        Task<JobEditResult> EditDetailsAsync(JobEditInputModel model);

        /// <summary>
        /// Saves the notes only the admin sees. False when there is no such job.
        /// </summary>
        Task<bool> SaveNotesAsync(Guid bookingId, string notes);

        /// <summary>
        /// A job's history lines, oldest first.
        /// </summary>
        Task<IEnumerable<T>> GetHistoryAsync<T>(Guid bookingId);

        /// <summary>
        /// The jobs that are on or done on one day, for the admin calendar.
        /// </summary>
        Task<IEnumerable<T>> GetJobsForDayAsync<T>(DateTime day);

        Task RescheduleBookingAsync(Guid bookingId, Guid newSlotId);

        Task AddBookingImageAsync(Guid bookingId, string imageUrl);

        Task<int> ReleaseAbandonedBookingsAsync(TimeSpan olderThan);

        Task<int> GetTotalCountAsync();

        Task<int> GetPendingCountAsync();

        BookingSummaryStats GetSummaryStats(IEnumerable<BookingDetailsViewModel> bookings);

        Task<IEnumerable<string>> GetStatusOptionsAsync();
    }
}
