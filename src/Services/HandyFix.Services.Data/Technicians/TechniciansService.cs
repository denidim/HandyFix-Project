namespace HandyFix.Services.Data.Technicians
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;
    using HandyFix.Web.ViewModels.Administration.Technicians;

    using Microsoft.EntityFrameworkCore;

    public class TechniciansService : ITechniciansService
    {
        private readonly IDeletableEntityRepository<Technician> techniciansRepository;
        private readonly IDeletableEntityRepository<Booking> bookingsRepository;

        public TechniciansService(
            IDeletableEntityRepository<Technician> techniciansRepository,
            IDeletableEntityRepository<Booking> bookingsRepository)
        {
            this.techniciansRepository = techniciansRepository;
            this.bookingsRepository = bookingsRepository;
        }

        public async Task<IEnumerable<T>> GetAllAsync<T>(bool activeOnly = false)
        {
            var query = this.techniciansRepository.All();

            if (activeOnly)
            {
                query = query.Where(x => x.IsActive);
            }

            return await query
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.FirstName)
                .ThenBy(x => x.LastName)
                .To<T>()
                .ToListAsync();
        }

        public async Task<IEnumerable<T>> GetAssignableAsync<T>(Guid? includeTechnicianId = null)
        {
            return await this.techniciansRepository.All()
                .Where(x => x.IsActive || (includeTechnicianId != null && x.Id == includeTechnicianId))
                .OrderByDescending(x => x.IsActive)
                .ThenBy(x => x.FirstName)
                .ThenBy(x => x.LastName)
                .To<T>()
                .ToListAsync();
        }

        public async Task<T> GetByIdAsync<T>(Guid id)
        {
            return await this.techniciansRepository.All()
                .Where(x => x.Id == id)
                .To<T>()
                .FirstOrDefaultAsync();
        }

        public async Task<Guid> CreateAsync(TechnicianAdminInputModel model)
        {
            var technician = new Technician
            {
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim(),
                IsActive = model.IsActive,
            };

            await this.techniciansRepository.AddAsync(technician);
            await this.techniciansRepository.SaveChangesAsync();

            return technician.Id;
        }

        public async Task UpdateAsync(Guid id, TechnicianAdminInputModel model)
        {
            var technician = await this.techniciansRepository.All().FirstOrDefaultAsync(x => x.Id == id);
            if (technician == null)
            {
                return;
            }

            technician.FirstName = model.FirstName.Trim();
            technician.LastName = model.LastName.Trim();
            technician.PhoneNumber = model.PhoneNumber?.Trim();
            technician.IsActive = model.IsActive;

            await this.techniciansRepository.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var technician = await this.techniciansRepository.All().FirstOrDefaultAsync(x => x.Id == id);
            if (technician == null)
            {
                return false;
            }

            // Soft delete, unlike ServiceAreasService's hard delete - and for the opposite reason.
            // A technician squats on no unique index, but Booking.TechnicianId is a live FK
            // pointing at real job history. Deleting one who has worked jobs would leave those
            // bookings pointing at a row the global query filter hides, so the assignment would
            // silently read as "Not Assigned" on every past booking. Deactivation is the intended
            // way to retire someone; deletion is only for a row created in error.
            var hasBookings = await this.bookingsRepository.AllWithDeleted()
                .AnyAsync(x => x.TechnicianId == id);
            if (hasBookings)
            {
                return false;
            }

            this.techniciansRepository.Delete(technician);
            await this.techniciansRepository.SaveChangesAsync();

            return true;
        }
    }
}
