namespace HandyFix.Services.Data.Availability
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IAvailabilityService
    {
        Task<IEnumerable<DateTime>> GetAvailableDatesAsync(int daysAhead = 30);

        Task<IEnumerable<T>> GetAvailableSlotsForDateAsync<T>(DateTime date);

        Task<IEnumerable<T>> GetAllSlotsForDateAsync<T>(DateTime date);

        Task<bool> BookSlotAsync(Guid slotId, Guid bookingId);

        Task<bool> BlockSlotAsync(Guid slotId);

        Task<bool> ReleaseSlotAsync(Guid slotId);

        Task BlockDateAsync(DateTime date);

        Task GenerateSlotsForRangeAsync(DateTime startDate, DateTime endDate);
    }
}
