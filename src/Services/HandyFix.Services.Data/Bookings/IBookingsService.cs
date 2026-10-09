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

        Task<T> GetByIdAsync<T>(Guid id);

        Task<IEnumerable<T>> GetAllBookingsAsync<T>(
            BookingSortField sortField = BookingSortField.CreatedOn,
            bool descending = true,
            string statusFilter = null);

        Task<IEnumerable<T>> GetUserBookingsAsync<T>(string userId);

        /// <summary>
        /// Marks a booking completed. False, with nothing changed, when the booking is missing or
        /// <see cref="BookingRules.CanComplete"/> says no.
        /// </summary>
        Task<bool> CompleteBookingAsync(Guid bookingId);

        /// <summary>
        /// Sets or clears a booking's technician and, when a new one is set, emails the customer
        /// that technician's name and number. The result says which of those happened.
        /// </summary>
        Task<TechnicianAssignmentResult> AssignTechnicianAsync(Guid bookingId, Guid? technicianId);

        /// <summary>
        /// Cancels a booking and frees its slot. False, with nothing changed, when the booking is
        /// missing or <see cref="BookingRules.CanCancel"/> says no.
        /// </summary>
        Task<bool> CancelBookingAsync(Guid bookingId);

        Task RescheduleBookingAsync(Guid bookingId, Guid newSlotId);

        Task AddBookingImageAsync(Guid bookingId, string imageUrl);

        Task<int> ReleaseAbandonedBookingsAsync(TimeSpan olderThan);

        Task<int> GetTotalCountAsync();

        Task<int> GetPendingCountAsync();

        BookingSummaryStats GetSummaryStats(IEnumerable<BookingDetailsViewModel> bookings);

        Task<IEnumerable<string>> GetStatusOptionsAsync();
    }
}
