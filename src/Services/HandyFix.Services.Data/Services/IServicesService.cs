namespace HandyFix.Services.Data.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Microsoft.AspNetCore.Http;

    public interface IServicesService
    {
        Task<IEnumerable<T>> GetAllAsync<T>(bool activeOnly = true);

        Task<IEnumerable<T>> GetByCategoryAsync<T>(string categoryName, bool activeOnly = true);

        Task<T> GetByIdAsync<T>(Guid id);

        Task<T> GetBySlugAsync<T>(string slug);

        /// <summary>
        /// The hourly price of the emergency service, which the FAQ and the Terms quote, so the
        /// rate is set in one place, the admin panel. Null when no active service is marked as an
        /// emergency.
        /// </summary>
        Task<decimal?> GetEmergencyRateAsync();

        Task<Guid> CreateAsync(string name, string description, decimal basePrice, int estimatedDurationMinutes, Guid categoryId, int displayOrder, bool isPopular, bool isQuoteOnly = false, bool isEmergency = false);

        Task UpdateAsync(Guid id, string name, string description, decimal basePrice, int estimatedDurationMinutes, bool isActive, Guid categoryId, int displayOrder, bool isPopular, bool isQuoteOnly = false, bool isEmergency = false);

        Task<bool> NameIsTakenAsync(string name, Guid? exceptId = null);

        /// <summary>
        /// Deletes a service no job has. Returns false, and deletes nothing, for a service that a
        /// job has: that one is switched off instead (PROJECT_STATE.md Section 3ef).
        /// </summary>
        Task<bool> DeleteAsync(Guid id);

        Task SetServiceImageAsync(Guid serviceId, IFormFile imageFile);
    }
}
