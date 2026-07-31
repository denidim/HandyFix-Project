namespace HandyFix.Services.Data.Availability
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    using Microsoft.EntityFrameworkCore;

    public class AvailabilityService : IAvailabilityService
    {
        private readonly IDeletableEntityRepository<AvailabilitySlot> slotRepository;

        public AvailabilityService(IDeletableEntityRepository<AvailabilitySlot> slotRepository)
        {
            this.slotRepository = slotRepository;
        }

        public async Task<IEnumerable<DateTime>> GetAvailableDatesAsync(int daysAhead = 30)
        {
            var now = DateTime.Now;
            var today = DateTime.Today;
            var endDate = today.AddDays(daysAhead);

            var dates = await this.slotRepository.All()
                .Where(x => x.StartTime > now && x.StartTime <= endDate && !x.IsBooked && !x.IsBlocked)
                .Select(x => x.StartTime.Date)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            return dates;
        }

        public async Task<IEnumerable<T>> GetAvailableSlotsForDateAsync<T>(DateTime date)
        {
            var targetDate = date.Date;
            var nextDay = targetDate.AddDays(1);
            var now = DateTime.Now;

            // Query using an index-friendly range comparison instead of .Date.
            // StartTime > now hides past hours when targetDate is today.
            return await this.slotRepository.All()
                .Where(x => x.StartTime >= targetDate && x.StartTime < nextDay && x.StartTime > now && !x.IsBooked && !x.IsBlocked)
                .OrderBy(x => x.StartTime)
                .To<T>()
                .ToListAsync();
        }

        public async Task<IEnumerable<T>> GetAllSlotsForDateAsync<T>(DateTime date)
        {
            var targetDate = date.Date;
            var nextDay = targetDate.AddDays(1);
            var now = DateTime.Now;

            return await this.slotRepository.All()
                .Where(x => x.StartTime >= targetDate && x.StartTime < nextDay && x.StartTime > now)
                .OrderBy(x => x.StartTime)
                .To<T>()
                .ToListAsync();
        }

        public async Task<bool> BookSlotAsync(Guid slotId, Guid bookingId)
        {
            var slot = await this.slotRepository.All().FirstOrDefaultAsync(x => x.Id == slotId);
            if (slot == null || slot.IsBooked || slot.IsBlocked)
            {
                return false;
            }

            slot.IsBooked = true;
            slot.BookingId = bookingId;

            try
            {
                await this.slotRepository.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another request booked/blocked this slot between our read and write.
                return false;
            }
        }

        public async Task<bool> BlockSlotAsync(Guid slotId)
        {
            var slot = await this.slotRepository.All().FirstOrDefaultAsync(x => x.Id == slotId);
            if (slot == null)
            {
                return false;
            }

            slot.IsBlocked = true;

            await this.slotRepository.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ReleaseSlotAsync(Guid slotId)
        {
            var slot = await this.slotRepository.All().FirstOrDefaultAsync(x => x.Id == slotId);
            if (slot == null)
            {
                return false;
            }

            slot.IsBooked = false;
            slot.BookingId = null;

            try
            {
                await this.slotRepository.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Another request already changed this slot between our read and write.
                return false;
            }
        }

        public async Task BlockDateAsync(DateTime date)
        {
            var targetDate = date.Date;
            var slots = await this.slotRepository.All()
                .Where(x => x.StartTime.Date == targetDate)
                .ToListAsync();

            foreach (var slot in slots)
            {
                slot.IsBlocked = true;
            }

            await this.slotRepository.SaveChangesAsync();
        }

        /// <summary>
        /// Creates business capacity for a date range. Only ever called deliberately - by an admin
        /// through the Calendar, or by the non-production capacity seeder. Read paths never
        /// generate: browsing the public booking page or the admin calendar must not silently
        /// create capacity nobody decided to offer.
        /// </summary>
        public async Task GenerateSlotsForRangeAsync(DateTime startDate, DateTime endDate)
        {
            var start = startDate.Date;
            var end = endDate.Date;

            bool modificationsMade = false;

            for (var date = start; date <= end; date = date.AddDays(1))
            {
                if (date.DayOfWeek == DayOfWeek.Sunday)
                {
                    continue;
                }

                // Index-friendly verification boundary
                var nextDay = date.AddDays(1);
                var hasSlots = await this.slotRepository.All()
                    .AnyAsync(x => x.StartTime >= date && x.StartTime < nextDay);

                if (!hasSlots)
                {
                    modificationsMade = true;
                    for (int hour = 9; hour < 17; hour++)
                    {
                        var slotStart = date.AddHours(hour);
                        var slotEnd = slotStart.AddHours(1);

                        var newSlot = new AvailabilitySlot
                        {
                            StartTime = slotStart,
                            EndTime = slotEnd,
                            IsBooked = false,
                            IsBlocked = false,
                        };

                        await this.slotRepository.AddAsync(newSlot);
                    }
                }
            }

            // Only hit the database save if new records were actually staged
            if (modificationsMade)
            {
                await this.slotRepository.SaveChangesAsync();
            }
        }
    }
}
