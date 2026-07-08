namespace HandyFix.Services.Data.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IServicesService
    {
        Task<IEnumerable<T>> GetAllAsync<T>(bool activeOnly = true);

        Task<IEnumerable<T>> GetByCategoryAsync<T>(string categoryName, bool activeOnly = true);

        Task<T> GetByIdAsync<T>(Guid id);

        Task<T> GetBySlugAsync<T>(string slug);

        Task<Guid> CreateAsync(string name, string description, decimal basePrice, int estimatedDurationMinutes, Guid categoryId);

        Task UpdateAsync(Guid id, string name, string description, decimal basePrice, int estimatedDurationMinutes, bool isActive, Guid categoryId);

        Task DeleteAsync(Guid id);
    }
}
