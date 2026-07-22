namespace HandyFix.Services.Data.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    using Mapster;

    using Microsoft.EntityFrameworkCore;

    public class ServicesService : IServicesService
    {
        private readonly IDeletableEntityRepository<Service> servicesRepository;
        private readonly IDeletableEntityRepository<ServiceImage> serviceImageRepository;

        public ServicesService(
            IDeletableEntityRepository<Service> servicesRepository,
            IDeletableEntityRepository<ServiceImage> serviceImageRepository)
        {
            this.servicesRepository = servicesRepository;
            this.serviceImageRepository = serviceImageRepository;
        }

        public async Task<IEnumerable<T>> GetAllAsync<T>(bool activeOnly = true)
        {
            var query = this.servicesRepository.All();

            if (activeOnly)
            {
                query = query.Where(x => x.IsActive);
            }

            return await query
                .OrderBy(x => x.Name)
                .To<T>()
                .ToListAsync();
        }

        public async Task<IEnumerable<T>> GetByCategoryAsync<T>(string categoryName, bool activeOnly = true)
        {
            var query = this.servicesRepository.All()
                .Where(x => x.Category.Name.ToLower() == categoryName.ToLower());

            if (activeOnly)
            {
                query = query.Where(x => x.IsActive);
            }

            return await query
                .OrderBy(x => x.Name)
                .To<T>()
                .ToListAsync();
        }

        public async Task<T> GetByIdAsync<T>(Guid id)
        {
            return await this.servicesRepository.All()
                .Where(x => x.Id == id)
                .To<T>()
                .FirstOrDefaultAsync();
        }

        public async Task<T> GetBySlugAsync<T>(string slug)
        {
            return await this.servicesRepository.All()
                .Where(x => x.Slug == slug.ToLower())
                .Include(x => x.Category)
                .ProjectToType<T>()
                .FirstOrDefaultAsync();
        }

        public async Task<Guid> CreateAsync(string name, string description, decimal basePrice, int estimatedDurationMinutes, Guid categoryId)
        {
            var service = new Service
            {
                Name = name,
                Description = description,
                BasePrice = basePrice,
                EstimatedDurationMinutes = estimatedDurationMinutes,
                CategoryId = categoryId,
                IsActive = true,
                Slug = Slugify(name),
            };

            await this.servicesRepository.AddAsync(service);
            await this.servicesRepository.SaveChangesAsync();

            return service.Id;
        }

        public async Task UpdateAsync(Guid id, string name, string description, decimal basePrice, int estimatedDurationMinutes, bool isActive, Guid categoryId)
        {
            var service = await this.servicesRepository.All()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (service != null)
            {
                service.Name = name;
                service.Description = description;
                service.BasePrice = basePrice;
                service.EstimatedDurationMinutes = estimatedDurationMinutes;
                service.IsActive = isActive;
                service.CategoryId = categoryId;
                service.Slug = Slugify(name);

                await this.servicesRepository.SaveChangesAsync();
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            var service = await this.servicesRepository.All()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (service != null)
            {
                this.servicesRepository.Delete(service);
                await this.servicesRepository.SaveChangesAsync();
            }
        }

        public async Task AddOrUpdateServiceImageAsync(Guid serviceId, string imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                return;
            }

            var existingImage = await this.serviceImageRepository.All()
                .FirstOrDefaultAsync(x => x.ServiceId == serviceId);

            if (existingImage != null)
            {
                existingImage.ImageUrl = imageUrl;
            }
            else
            {
                await this.serviceImageRepository.AddAsync(new ServiceImage
                {
                    ServiceId = serviceId,
                    ImageUrl = imageUrl,
                });
            }

            await this.serviceImageRepository.SaveChangesAsync();
        }

        private static string Slugify(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            return name
                .Replace(" ", "-")
                .Replace("/", "-")
                .Replace("&", "-")
                .Replace("--", "-")
                .Trim('-')
                .ToLower();
        }
    }
}
