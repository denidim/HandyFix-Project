namespace HandyFix.Services.Data.Bookings
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;

    public interface IBookingsService
    {
        Task<Booking> CreateBookingAsync(
            string firstName,
            string lastName,
            string email,
            string phone,
            string address,
            string problemDescription,
            Guid slotId,
            IEnumerable<Guid> serviceIds,
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
