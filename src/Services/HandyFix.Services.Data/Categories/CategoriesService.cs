namespace HandyFix.Services.Data.Categories
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    using Microsoft.EntityFrameworkCore;

    public class CategoriesService : ICategoriesService
    {
        private readonly IDeletableEntityRepository<ServiceCategory> categoriesRepository;

        public CategoriesService(IDeletableEntityRepository<ServiceCategory> categoriesRepository)
        {
            this.categoriesRepository = categoriesRepository;
        }

        public async Task<IEnumerable<T>> GetAllAsync<T>()
        {
            return await this.categoriesRepository.All()
                .OrderBy(x => x.Name)
                .To<T>()
                .ToListAsync();
        }

        public async Task<T> GetBySlugAsync<T>(string slug)
        {
            return await this.categoriesRepository.All()
                .Where(x => x.Slug == slug.ToLower())
                .To<T>()
                .FirstOrDefaultAsync();
        }
    }
}
