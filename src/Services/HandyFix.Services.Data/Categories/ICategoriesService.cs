namespace HandyFix.Services.Data.Categories
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface ICategoriesService
    {
        Task<IEnumerable<T>> GetAllAsync<T>();

        Task<T> GetBySlugAsync<T>(string slug);

        Task CreateAsync(string name, string description);
    }
}
