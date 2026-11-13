namespace HandyFix.Services.Data.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Web.ViewModels.Administration.Services;

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

        /// <summary>
        /// Saves the page's own blocks and questions from the admin form (PROJECT_STATE.md Section
        /// 3ep). Lines are trimmed and empty lines dropped; a block left empty is stored as null.
        /// The questions are replaced in the order the form sent them.
        /// </summary>
        Task SavePageAsync(Guid id, ServiceAdminInputModel model);

        /// <summary>
        /// Drops question rows left entirely blank (changing model.Faqs), then checks the rest
        /// against ServiceFaq's limits. Each error is the form field's key and its message.
        /// </summary>
        IEnumerable<KeyValuePair<string, string>> PruneAndValidateFaqs(ServiceAdminInputModel model);

        Task<bool> NameIsTakenAsync(string name, Guid? exceptId = null);

        /// <summary>
        /// Deletes a service no job has. Returns false, and deletes nothing, for a service that a
        /// job has: that one is switched off instead (PROJECT_STATE.md Section 3ef).
        /// </summary>
        Task<bool> DeleteAsync(Guid id);

        Task SetServiceImageAsync(Guid serviceId, IFormFile imageFile);
    }
}
