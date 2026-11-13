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
    using HandyFix.Web.ViewModels.Administration.Services;

    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;

    public class ServicesService : IServicesService
    {
        private readonly IDeletableEntityRepository<Service> servicesRepository;
        private readonly IDeletableEntityRepository<ServiceImage> serviceImageRepository;
        private readonly IDeletableEntityRepository<ServiceFaq> faqsRepository;
        private readonly IImageStorageService imageStorageService;

        public ServicesService(
            IDeletableEntityRepository<Service> servicesRepository,
            IDeletableEntityRepository<ServiceImage> serviceImageRepository,
            IDeletableEntityRepository<ServiceFaq> faqsRepository,
            IImageStorageService imageStorageService)
        {
            this.servicesRepository = servicesRepository;
            this.serviceImageRepository = serviceImageRepository;
            this.faqsRepository = faqsRepository;
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

        public async Task<decimal?> GetEmergencyRateAsync()
        {
            return await this.servicesRepository.All()
                .Where(x => x.IsActive && x.IsEmergency)
                .OrderBy(x => x.DisplayOrder)
                .Select(x => (decimal?)x.BasePrice)
                .FirstOrDefaultAsync();
        }

        public async Task<T> GetBySlugAsync<T>(string slug)
        {
            return await this.servicesRepository.All()
                .Where(x => x.Slug == slug.ToLower())
                .To<T>()
                .FirstOrDefaultAsync();
        }

        public async Task<Guid> CreateAsync(string name, string description, decimal basePrice, int estimatedDurationMinutes, Guid categoryId, int displayOrder, bool isPopular, bool isQuoteOnly = false, bool isEmergency = false)
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
                IsQuoteOnly = isQuoteOnly,
                IsEmergency = isEmergency,
                Slug = SlugGenerator.Slugify(name),
            };

            await this.servicesRepository.AddAsync(service);
            await this.servicesRepository.SaveChangesAsync();

            return service.Id;
        }

        public async Task UpdateAsync(Guid id, string name, string description, decimal basePrice, int estimatedDurationMinutes, bool isActive, Guid categoryId, int displayOrder, bool isPopular, bool isQuoteOnly = false, bool isEmergency = false)
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
                service.IsQuoteOnly = isQuoteOnly;
                service.IsEmergency = isEmergency;

                // A new name gives the page a new address, and the picture stays where it is: its
                // address is in the database and nothing renames it. A built-in picture keeps the
                // file name it came with (PROJECT_STATE.md Section 3cq).
                service.Slug = SlugGenerator.Slugify(name);

                await this.servicesRepository.SaveChangesAsync();
            }
        }

        public async Task SavePageAsync(Guid id, ServiceAdminInputModel model)
        {
            Service service = await this.servicesRepository.All()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (service == null)
            {
                return;
            }

            service.Summary = OneLine(model.Summary);
            service.CommonFindings = Lines(model.CommonFindings);
            service.VisitSteps = Lines(model.VisitSteps);
            service.BeforeWeArrive = Lines(model.BeforeWeArrive);
            service.Included = Lines(model.Included);
            service.NotIncluded = Lines(model.NotIncluded);

            // Replaced, not matched up row by row, as a town's questions are: they are only text
            // in an order, and hard-deleting the old rows stops deleted ones piling up.
            List<ServiceFaq> existing = await this.faqsRepository.AllWithDeleted()
                .Where(x => x.ServiceId == id)
                .ToListAsync();

            foreach (ServiceFaq faq in existing)
            {
                this.faqsRepository.HardDelete(faq);
            }

            var displayOrder = 1;
            foreach (ServiceFaqInputModel faq in model.Faqs ?? Enumerable.Empty<ServiceFaqInputModel>())
            {
                if (string.IsNullOrWhiteSpace(faq?.Question) || string.IsNullOrWhiteSpace(faq.Answer))
                {
                    continue;
                }

                await this.faqsRepository.AddAsync(new ServiceFaq
                {
                    ServiceId = id,
                    Question = faq.Question.Trim(),
                    Answer = faq.Answer.Trim(),
                    DisplayOrder = displayOrder++,
                });
            }

            await this.servicesRepository.SaveChangesAsync();
        }

        public IEnumerable<KeyValuePair<string, string>> PruneAndValidateFaqs(ServiceAdminInputModel model)
        {
            model.Faqs = (model.Faqs ?? new List<ServiceFaqInputModel>())
                .Where(f => !string.IsNullOrWhiteSpace(f?.Question) || !string.IsNullOrWhiteSpace(f?.Answer))
                .ToList();

            var errors = new List<KeyValuePair<string, string>>();

            // The limits are ServiceFaq's, so a row that passes here cannot fail at SaveChanges.
            for (var i = 0; i < model.Faqs.Count; i++)
            {
                var question = model.Faqs[i].Question?.Trim();
                var answer = model.Faqs[i].Answer?.Trim();

                if (string.IsNullOrWhiteSpace(question))
                {
                    errors.Add(new($"Faqs[{i}].Question", "Question is required."));
                }
                else if (question.Length < 5 || question.Length > 300)
                {
                    errors.Add(new($"Faqs[{i}].Question", "Question must be between 5 and 300 characters."));
                }

                if (string.IsNullOrWhiteSpace(answer))
                {
                    errors.Add(new($"Faqs[{i}].Answer", "Answer is required."));
                }
                else if (answer.Length < 5 || answer.Length > 1000)
                {
                    errors.Add(new($"Faqs[{i}].Answer", "Answer must be between 5 and 1000 characters."));
                }
            }

            return errors;
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

        public async Task<bool> DeleteAsync(Guid id)
        {
            Service service = await this.servicesRepository.All()
                .Include(x => x.Images)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (service == null)
            {
                return false;
            }

            // The delete is a soft one, and every page leaves a deleted row out: a job that has
            // this service would read "None picked" from then on, on its page, in the job list
            // and in the month's file for the accountant. So a service a job has is not deleted;
            // it is switched off (the Active tick), which keeps its name on its jobs. The same
            // rule as a technician with jobs (TechniciansService.DeleteAsync). A line that was
            // taken off a job, when its service was changed, does not count: that job no longer
            // has this service.
            var hasJobs = await this.servicesRepository.All()
                .Where(x => x.Id == id)
                .SelectMany(x => x.BookingServices)
                .AnyAsync(x => !x.IsDeleted);
            if (hasJobs)
            {
                return false;
            }

            List<string> pictures = service.Images.Select(x => x.ImageUrl).ToList();

            this.servicesRepository.Delete(service);
            await this.servicesRepository.SaveChangesAsync();

            // After the service is gone, so a picture is never missing from a service still
            // shown. An upload leaves the bucket; a built-in picture stays in the site's files,
            // where it used to be deleted until the next deploy put it back.
            foreach (var picture in pictures)
            {
                await this.imageStorageService.DeleteServiceImageAsync(picture);
            }

            return true;
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

        // The picture the admin uploads, on a new service or an existing one. In this order: the new
        // picture goes up, the service points at it, and only then does the admin's previous upload
        // leave the bucket, so a failure at any step leaves the service with a picture.
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

            if (string.IsNullOrEmpty(imageUrl))
            {
                return;
            }

            string previous = await this.serviceImageRepository.All()
                .Where(x => x.ServiceId == serviceId)
                .Select(x => x.ImageUrl)
                .FirstOrDefaultAsync();

            await this.AddOrUpdateServiceImageAsync(serviceId, imageUrl);

            if (previous != null && previous != imageUrl)
            {
                await this.imageStorageService.DeleteServiceImageAsync(previous);
            }
        }

        // A block as the page reads it: one point per line, each trimmed, no empty lines, and
        // null when nothing is left, so an empty block is left off the page.
        private static string Lines(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var lines = text.Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToList();

            return lines.Count > 0 ? string.Join("\n", lines) : null;
        }

        private static string OneLine(string text) =>
            string.IsNullOrWhiteSpace(text) ? null : string.Join(" ", text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
    }
}
