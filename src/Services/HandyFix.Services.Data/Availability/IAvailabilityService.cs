namespace HandyFix.Services.Data.Availability
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;

    public interface IAvailabilityService
    {
        Task<IEnumerable<DateTime>> GetAvailableDatesAsync(int daysAhead = 30);

        Task<IEnumerable<T>> GetAvailableSlotsForDateAsync<T>(DateTime date);

        Task<IEnumerable<T>> GetAllSlotsForDateAsync<T>(DateTime date);

        Task<IEnumerable<AvailabilitySlot>> GetAllSlotsForDayAsync(DateTime date);

        /// <summary>
        /// Whether that day has any slot at all: free, booked or blocked. The calendar's arrows
        /// ask it about the day before and the day after.
        /// </summary>
        Task<bool> HasSlotsOnDayAsync(DateTime date);

        /// <summary>
        /// The day a slot is on, or null if there is no such slot. The booking page opens again
        /// on that day when a customer's form comes back to them.
        /// </summary>
        Task<DateTime?> GetSlotDateAsync(Guid slotId);

        Task<bool> BookSlotAsync(Guid slotId, Guid bookingId);

        Task<bool> BlockSlotAsync(Guid slotId);

        Task<bool> ReleaseSlotAsync(Guid slotId);

        /// <summary>
        /// Opens a blocked slot again. False when there is no such slot.
        /// </summary>
        Task<bool> UnblockSlotAsync(Guid slotId);

        Task BlockDateAsync(DateTime date);

        Task GenerateSlotsForRangeAsync(DateTime startDate, DateTime endDate);
    }
}
