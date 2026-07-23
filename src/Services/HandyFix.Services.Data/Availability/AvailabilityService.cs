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
        private readonly IDeletableEntityRepository<Technician> technicianRepository;

        public AvailabilityService(
            IDeletableEntityRepository<AvailabilitySlot> slotRepository,
            IDeletableEntityRepository<Technician> technicianRepository)
        {
            this.slotRepository = slotRepository;
            this.technicianRepository = technicianRepository;
        }

        public async Task<IEnumerable<DateTime>> GetAvailableDatesAsync(int daysAhead = 30)
        {
            var today = DateTime.Today;
            var endDate = today.AddDays(daysAhead);

            // Auto-generate default slots for the date range
            await this.GenerateSlotsForRangeAsync(today, endDate);

            var dates = await this.slotRepository.All()
                .Where(x => x.StartTime >= today && x.StartTime <= endDate && !x.IsBooked && !x.IsBlocked)
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

            // 1. Ensure slots are fully generated and saved to DB before querying
            await this.GenerateSlotsForRangeAsync(targetDate, targetDate);

            // 2. Query using an index-friendly range comparison instead of .Date
            return await this.slotRepository.All()
                .Where(x => x.StartTime >= targetDate && x.StartTime < nextDay && !x.IsBooked && !x.IsBlocked)
                .OrderBy(x => x.StartTime)
                .To<T>()
                .ToListAsync();
        }

        public async Task<IEnumerable<T>> GetAllSlotsForDateAsync<T>(DateTime date)
        {
            var targetDate = date.Date;
            var nextDay = targetDate.AddDays(1);

            await this.GenerateSlotsForRangeAsync(targetDate, targetDate);

            return await this.slotRepository.All()
                .Where(x => x.StartTime >= targetDate && x.StartTime < nextDay)
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

            await this.slotRepository.SaveChangesAsync();
            return true;
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

            await this.slotRepository.SaveChangesAsync();
            return true;
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

        public async Task GenerateSlotsForRangeAsync(DateTime startDate, DateTime endDate)
        {
            var start = startDate.Date;
            var end = endDate.Date;

            var defaultTechnician = await this.technicianRepository.All().FirstOrDefaultAsync(x => x.IsActive);
            if (defaultTechnician == null)
            {
                return; // Exit safely if no technician exists
            }

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
                            TechnicianId = defaultTechnician.Id,
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
