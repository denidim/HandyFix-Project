namespace HandyFix.Services.Data.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services;
    using HandyFix.Services.Data.Common;
    using HandyFix.Services.Mapping;

    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;

    public class ServicesService : IServicesService
    {
        private readonly IDeletableEntityRepository<Service> servicesRepository;
        private readonly IDeletableEntityRepository<ServiceImage> serviceImageRepository;
        private readonly IImageStorageService imageStorageService;

        public ServicesService(
            IDeletableEntityRepository<Service> servicesRepository,
            IDeletableEntityRepository<ServiceImage> serviceImageRepository,
            IImageStorageService imageStorageService)
        {
            this.servicesRepository = servicesRepository;
            this.serviceImageRepository = serviceImageRepository;
            this.imageStorageService = imageStorageService;
        }

        public async Task<IEnumerable<T>> GetAllAsync<T>(bool activeOnly = true)
        {
            IQueryable<Service> query = this.servicesRepository.All();

            if (activeOnly)
            {
                query = query.Where(x => x.IsActive);
            }

            return await query
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .To<T>()
                .ToListAsync();
        }

        public async Task<IEnumerable<T>> GetByCategoryAsync<T>(string categoryName, bool activeOnly = true)
        {
            IQueryable<Service> query = this.servicesRepository.All()
                .Where(x => x.Category.Name.ToLower() == categoryName.ToLower());

            if (activeOnly)
            {
                query = query.Where(x => x.IsActive);
            }

            return await query
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
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
                .To<T>()
                .FirstOrDefaultAsync();
        }

        public async Task<Guid> CreateAsync(string name, string description, decimal basePrice, int estimatedDurationMinutes, Guid categoryId, int displayOrder, bool isPopular)
        {
            var service = new Service
            {
                Name = name,
                Description = description,
                BasePrice = basePrice,
                EstimatedDurationMinutes = estimatedDurationMinutes,
                CategoryId = categoryId,
                IsActive = true,
                DisplayOrder = displayOrder,
                IsPopular = isPopular,
                Slug = SlugGenerator.Slugify(name),
            };

            await this.servicesRepository.AddAsync(service);
            await this.servicesRepository.SaveChangesAsync();

            return service.Id;
        }

        public async Task UpdateAsync(Guid id, string name, string description, decimal basePrice, int estimatedDurationMinutes, bool isActive, Guid categoryId, int displayOrder, bool isPopular)
        {
            Service service = await this.servicesRepository.All()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (service != null)
            {
                service.Name = name;
                service.Description = description;
                service.BasePrice = basePrice;
                service.EstimatedDurationMinutes = estimatedDurationMinutes;
                service.IsActive = isActive;
                service.CategoryId = categoryId;
                service.DisplayOrder = displayOrder;
                service.IsPopular = isPopular;
                service.Slug = SlugGenerator.Slugify(name);

                await this.servicesRepository.SaveChangesAsync();
            }
        }

        // A service's slug is made from its name, and the unique index on Slug covers deleted rows
        // too. A name whose slug is already in the table, on any row but the one being edited,
        // cannot be saved: the admin form asks before it tries (PROJECT_STATE Section 3ca).
        public async Task<bool> NameIsTakenAsync(string name, Guid? exceptId = null)
        {
            var slug = SlugGenerator.Slugify(name);

            return await this.servicesRepository.AllWithDeleted()
                .AnyAsync(x => x.Slug == slug && x.Id != exceptId);
        }

        public async Task DeleteAsync(Guid id)
        {
            Service service = await this.servicesRepository.All()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (service != null)
            {
                this.imageStorageService.DeleteServiceImage(service.Slug);
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

            ServiceImage existingImage = await this.serviceImageRepository.All()
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

        public async Task SetServiceImageAsync(Guid serviceId, IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return;
            }

            Service service = await this.servicesRepository.All()
                .FirstOrDefaultAsync(x => x.Id == serviceId);

            if (service == null)
            {
                return;
            }

            string imageUrl;
            using (Stream stream = imageFile.OpenReadStream())
            {
                imageUrl = await this.imageStorageService.SaveServiceImageAsync(stream, imageFile.FileName, imageFile.ContentType, service.Slug);
            }

            if (!string.IsNullOrEmpty(imageUrl))
            {
                await this.AddOrUpdateServiceImageAsync(serviceId, imageUrl);
            }
        }

        public async Task UpdateServiceImageAsync(Guid serviceId, string oldSlug, string newSlug, IFormFile imageFile)
        {
            if (imageFile != null && imageFile.Length > 0)
            {
                this.imageStorageService.DeleteServiceImage(oldSlug);
                if (oldSlug != newSlug)
                {
                    this.imageStorageService.DeleteServiceImage(newSlug);
                }

                string imageUrl;
                using (Stream stream = imageFile.OpenReadStream())
                {
                    imageUrl = await this.imageStorageService.SaveServiceImageAsync(stream, imageFile.FileName, imageFile.ContentType, newSlug);
                }

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    await this.AddOrUpdateServiceImageAsync(serviceId, imageUrl);
                }
            }
            else if (oldSlug != newSlug)
            {
                this.imageStorageService.RenameServiceImage(oldSlug, newSlug);
                string newImageUrl = this.imageStorageService.GetServiceImagePublicUrl(newSlug);
                await this.AddOrUpdateServiceImageAsync(serviceId, newImageUrl);
            }
        }
    }
}
