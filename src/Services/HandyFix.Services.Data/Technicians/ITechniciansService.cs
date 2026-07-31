namespace HandyFix.Services.Data.Technicians
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Web.ViewModels.Administration.Technicians;

    public interface ITechniciansService
    {
        Task<IEnumerable<T>> GetAllAsync<T>(bool activeOnly = false);

        /// <summary>
        /// The technicians a booking may be assigned to: every active one, plus the technician
        /// already assigned to this booking even if they have since been deactivated - otherwise
        /// opening the form would silently drop an existing assignment on the next save.
        /// </summary>
        Task<IEnumerable<T>> GetAssignableAsync<T>(Guid? includeTechnicianId = null);

        Task<T> GetByIdAsync<T>(Guid id);

        Task<Guid> CreateAsync(TechnicianAdminInputModel model);

        Task UpdateAsync(Guid id, TechnicianAdminInputModel model);

        /// <summary>
        /// Soft-deletes a technician who has no bookings. Returns false without deleting when
        /// bookings reference them - retiring such a technician is what <c>IsActive</c> is for.
        /// </summary>
        Task<bool> DeleteAsync(Guid id);
    }
}
