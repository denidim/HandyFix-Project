namespace HandyFix.Services.Data.ServiceAreas
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;
    using HandyFix.Web.ViewModels.ServiceAreas;

    using Microsoft.EntityFrameworkCore;

    public class ServiceAreasService : IServiceAreasService
    {
        private readonly IDeletableEntityRepository<ServiceArea> areasRepository;
        private readonly IDeletableEntityRepository<ServiceAreaFaq> faqsRepository;

        public ServiceAreasService(
            IDeletableEntityRepository<ServiceArea> areasRepository,
            IDeletableEntityRepository<ServiceAreaFaq> faqsRepository)
        {
            this.areasRepository = areasRepository;
            this.faqsRepository = faqsRepository;
        }

        public async Task<IEnumerable<T>> GetAllAsync<T>(bool featuredFirst = true)
        {
            IQueryable<ServiceArea> query = this.areasRepository.All();

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

        public async Task<T> GetByIdAsync<T>(Guid id)
        {
            return await this.areasRepository.All()
                .Where(x => x.Id == id)
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

        public async Task<Guid> CreateAsync(ServiceAreaAdminInputModel model)
        {
            var area = new ServiceArea
            {
                Slug = NormalizeSlug(model.Slug),
                Name = model.Name,
                Region = model.Region,
                DriveTimeMinutes = model.DriveTimeMinutes,
                DisplayOrder = model.DisplayOrder,
                IsFeatured = model.IsFeatured,
                IntroCopy = model.IntroCopy,
                LocalNeighbourhoodsCopy = model.LocalNeighbourhoodsCopy,
            };

            await this.areasRepository.AddAsync(area);
            await this.areasRepository.SaveChangesAsync();

            await this.ReplaceFaqsAsync(area.Id, model.Faqs);

            return area.Id;
        }

        public async Task UpdateAsync(Guid id, ServiceAreaAdminInputModel model)
        {
            ServiceArea area = await this.areasRepository.All().FirstOrDefaultAsync(x => x.Id == id);
            if (area == null)
            {
                return;
            }

            area.Slug = NormalizeSlug(model.Slug);
            area.Name = model.Name;
            area.Region = model.Region;
            area.DriveTimeMinutes = model.DriveTimeMinutes;
            area.DisplayOrder = model.DisplayOrder;
            area.IsFeatured = model.IsFeatured;
            area.IntroCopy = model.IntroCopy;
            area.LocalNeighbourhoodsCopy = model.LocalNeighbourhoodsCopy;

            await this.areasRepository.SaveChangesAsync();

            await this.ReplaceFaqsAsync(id, model.Faqs);
        }

        public async Task DeleteAsync(Guid id)
        {
            ServiceArea area = await this.areasRepository.AllWithDeleted().FirstOrDefaultAsync(x => x.Id == id);
            if (area == null)
            {
                return;
            }

            // The FK from ServiceAreaFaq is ReferentialAction.Restrict, so the children have to go
            // first or SaveChanges throws a constraint violation.
            List<ServiceAreaFaq> faqs = await this.faqsRepository.AllWithDeleted()
                .Where(x => x.ServiceAreaId == id)
                .ToListAsync();

            foreach (ServiceAreaFaq faq in faqs)
            {
                this.faqsRepository.HardDelete(faq);
            }

            await this.faqsRepository.SaveChangesAsync();

            this.areasRepository.HardDelete(area);
            await this.areasRepository.SaveChangesAsync();
        }

        public async Task<bool> SlugExistsAsync(string slug, Guid? excludeAreaId = null)
        {
            var normalized = NormalizeSlug(slug);

            // AllWithDeleted, not All: IX_ServiceAreas_Slug has no IsDeleted filter, so a
            // soft-deleted row still reserves its slug at the database level. Checking only live
            // rows would let the form accept a duplicate and then fail with a raw 500 on save.
            IQueryable<ServiceArea> query = this.areasRepository.AllWithDeleted().Where(x => x.Slug == normalized);

            if (excludeAreaId.HasValue)
            {
                query = query.Where(x => x.Id != excludeAreaId.Value);
            }

            return await query.AnyAsync();
        }

        private static string NormalizeSlug(string slug)
        {
            return string.IsNullOrWhiteSpace(slug) ? string.Empty : slug.Trim().ToLowerInvariant();
        }

        public IEnumerable<ServiceAreaFaqValidationError> PruneAndValidateFaqs(ServiceAreaAdminInputModel model)
        {
            model.Faqs ??= new List<ServiceAreaFaqInputModel>();

            model.Faqs = model.Faqs
                .Where(f => !string.IsNullOrWhiteSpace(f?.Question) || !string.IsNullOrWhiteSpace(f?.Answer))
                .ToList();

            var errors = new List<ServiceAreaFaqValidationError>();

            for (var i = 0; i < model.Faqs.Count; i++)
            {
                var question = model.Faqs[i].Question?.Trim();
                var answer = model.Faqs[i].Answer?.Trim();

                // Limits mirror the ServiceAreaFaq entity, so a row that passes here cannot fail
                // at SaveChanges.
                if (string.IsNullOrWhiteSpace(question))
                {
                    errors.Add(new ServiceAreaFaqValidationError { Key = $"Faqs[{i}].Question", Message = "Question is required." });
                }
                else if (question.Length < 5 || question.Length > 300)
                {
                    errors.Add(new ServiceAreaFaqValidationError { Key = $"Faqs[{i}].Question", Message = "Question must be between 5 and 300 characters." });
                }

                if (string.IsNullOrWhiteSpace(answer))
                {
                    errors.Add(new ServiceAreaFaqValidationError { Key = $"Faqs[{i}].Answer", Message = "Answer is required." });
                }
                else if (answer.Length < 5 || answer.Length > 1000)
                {
                    errors.Add(new ServiceAreaFaqValidationError { Key = $"Faqs[{i}].Answer", Message = "Answer must be between 5 and 1000 characters." });
                }
            }

            return errors;
        }

        /// <summary>
        /// FAQs are replaced wholesale rather than diffed. They carry no external references and
        /// are ordered purely by DisplayOrder, so recreating them is simpler than reconciling by id
        /// - and hard-deleting the old rows stops soft-deleted orphans accumulating on every save.
        /// </summary>
        private async Task ReplaceFaqsAsync(Guid areaId, IEnumerable<ServiceAreaFaqInputModel> faqs)
        {
            List<ServiceAreaFaq> existing = await this.faqsRepository.AllWithDeleted()
                .Where(x => x.ServiceAreaId == areaId)
                .ToListAsync();

            foreach (ServiceAreaFaq faq in existing)
            {
                this.faqsRepository.HardDelete(faq);
            }

            var displayOrder = 1;
            foreach (ServiceAreaFaqInputModel faq in faqs ?? Enumerable.Empty<ServiceAreaFaqInputModel>())
            {
                if (string.IsNullOrWhiteSpace(faq?.Question) || string.IsNullOrWhiteSpace(faq.Answer))
                {
                    continue;
                }

                await this.faqsRepository.AddAsync(new ServiceAreaFaq
                {
                    ServiceAreaId = areaId,
                    Question = faq.Question.Trim(),
                    Answer = faq.Answer.Trim(),
                    DisplayOrder = displayOrder++,
                });
            }

            await this.faqsRepository.SaveChangesAsync();
        }
    }
}
