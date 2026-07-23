namespace HandyFix.Services.Data.Bookings
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;
    using HandyFix.Web.ViewModels.Booking;

    public interface IBookingsService
    {
        Task<Booking> CreateBookingAsync(
            BookingInputModel model,
            IReadOnlyList<string> imageUrls,
            string userId = null);

        Task<T> GetByIdAsync<T>(Guid id);

        Task<IEnumerable<T>> GetAllBookingsAsync<T>();

        Task<IEnumerable<T>> GetUserBookingsAsync<T>(string userId);

        Task UpdateStatusAsync(Guid bookingId, string statusName);

        Task AssignTechnicianAsync(Guid bookingId, Guid technicianId);

        Task CancelBookingAsync(Guid bookingId);

        Task RescheduleBookingAsync(Guid bookingId, Guid newSlotId);

        Task AddBookingImageAsync(Guid bookingId, string imageUrl);
    }
}
