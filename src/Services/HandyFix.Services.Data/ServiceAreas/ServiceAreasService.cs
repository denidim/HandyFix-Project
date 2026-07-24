namespace HandyFix.Services.Data.ServiceAreas
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    using Microsoft.EntityFrameworkCore;

    public class ServiceAreasService : IServiceAreasService
    {
        private readonly IDeletableEntityRepository<ServiceArea> areasRepository;

        public ServiceAreasService(IDeletableEntityRepository<ServiceArea> areasRepository)
        {
            this.areasRepository = areasRepository;
        }

        public async Task<IEnumerable<T>> GetAllAsync<T>(bool featuredFirst = true)
        {
            var query = this.areasRepository.All();

            query = featuredFirst
                ? query.OrderByDescending(x => x.IsFeatured).ThenBy(x => x.DisplayOrder)
                : query.OrderBy(x => x.DisplayOrder);

            return await query.To<T>().ToListAsync();
        }

        public async Task<T> GetBySlugAsync<T>(string slug)
        {
            return await this.areasRepository.All()
                .Where(x => x.Slug == slug.ToLower())
                .To<T>()
                .FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<T>> GetNearestAsync<T>(Guid excludeAreaId, int take = 4)
        {
            var currentDriveTime = await this.areasRepository.All()
                .Where(x => x.Id == excludeAreaId)
                .Select(x => (int?)x.DriveTimeMinutes)
                .FirstOrDefaultAsync();

            if (currentDriveTime == null)
            {
                return Enumerable.Empty<T>();
            }

            return await this.areasRepository.All()
                .Where(x => x.Id != excludeAreaId)
                .OrderBy(x => Math.Abs(x.DriveTimeMinutes - currentDriveTime.Value))
                .Take(take)
                .To<T>()
                .ToListAsync();
        }
    }
}
