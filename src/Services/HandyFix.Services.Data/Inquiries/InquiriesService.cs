namespace HandyFix.Services.Data.Inquiries
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services;
    using HandyFix.Services.Mapping;
    using HandyFix.Web.ViewModels.Administration.Enquiries;
    using HandyFix.Web.ViewModels.Home;

    using Microsoft.EntityFrameworkCore;

    public class InquiriesService : IInquiriesService
    {
        private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(10);

        private readonly IDeletableEntityRepository<Inquiry> inquiryRepository;
        private readonly IDeletableEntityRepository<InquiryImage> imageRepository;
        private readonly IImageService imageService;

        public InquiriesService(
            IDeletableEntityRepository<Inquiry> inquiryRepository,
            IDeletableEntityRepository<InquiryImage> imageRepository,
            IImageService imageService)
        {
            this.inquiryRepository = inquiryRepository;
            this.imageRepository = imageRepository;
            this.imageService = imageService;
        }

        // The same person sending the same words again within a few minutes is one enquiry sent
        // twice: a double click, a Back and resend, a page reloaded on the "send again?" prompt.
        // It is thanked like the first and not saved again. A second enquiry that says anything
        // different is a new enquiry, however soon it comes.
        public async Task<bool> IsRecentDuplicateAsync(ContactInputModel model)
        {
            DateTime since = DateTime.UtcNow - DuplicateWindow;
            var email = (model.Email ?? string.Empty).Trim().ToLower();
            var message = BuildMessage(model);

            return await this.inquiryRepository.All()
                .AnyAsync(x => x.CreatedOn >= since && x.Email.ToLower() == email && x.Message == message);
        }

        public async Task CreateInquiryAsync(ContactInputModel model, IReadOnlyList<string> imageUrls)
        {
            var inquiry = new Inquiry
            {
                Name = model.Name,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                Message = BuildMessage(model),
            };

            await this.inquiryRepository.AddAsync(inquiry);
            await this.inquiryRepository.SaveChangesAsync();

            if (imageUrls != null && imageUrls.Count > 0)
            {
                foreach (var url in imageUrls)
                {
                    var img = new InquiryImage
                    {
                        InquiryId = inquiry.Id,
                        ImageUrl = url,
                    };
                    await this.imageRepository.AddAsync(img);
                }

                await this.imageRepository.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<T>> GetAllAsync<T>(
            InquirySortField sortField = InquirySortField.CreatedOn,
            bool descending = true)
        {
            IQueryable<Inquiry> query = this.inquiryRepository.All();

            query = sortField switch
            {
                InquirySortField.Name => descending
                    ? query.OrderByDescending(x => x.Name)
                    : query.OrderBy(x => x.Name),
                _ => descending
                    ? query.OrderByDescending(x => x.CreatedOn)
                    : query.OrderBy(x => x.CreatedOn),
            };

            return await query.To<T>().ToListAsync();
        }

        public async Task<T> GetByIdAsync<T>(Guid id)
        {
            return await this.inquiryRepository.All()
                .Where(x => x.Id == id)
                .To<T>()
                .FirstOrDefaultAsync();
        }

        // A real delete, not the soft one the repository does by default: an enquiry is a person's
        // name, email, phone number and message, and "Delete Permanent" has to mean it for a
        // request to erase them to be met from the admin panel (PROJECT_STATE Section 3cb).
        public async Task DeleteAsync(Guid id)
        {
            Inquiry inquiry = await this.inquiryRepository.All()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (inquiry == null)
            {
                return;
            }

            List<InquiryImage> images = await this.imageRepository.AllWithDeleted()
                .Where(x => x.InquiryId == id)
                .ToListAsync();

            // The photos leave storage first. If that fails, it throws and the rows stay, so the
            // admin can try again; the other order could leave photos that nothing points at.
            await this.imageService.DeleteImagesAsync(images.Select(x => x.ImageUrl));

            foreach (InquiryImage image in images)
            {
                this.imageRepository.HardDelete(image);
            }

            this.inquiryRepository.HardDelete(inquiry);

            // Both repositories share one context, so this one save removes the photo rows and
            // the enquiry together.
            await this.inquiryRepository.SaveChangesAsync();
        }

        public async Task<int> GetTotalCountAsync()
        {
            return await this.inquiryRepository.All().CountAsync();
        }

        // The Contact form sends its category as a separate field, validated separately from the
        // visitor's own text; it's stored as a prefix so the admin Enquiries list reads the same
        // as it always has. Join Our Team sends no category.
        private static string BuildMessage(ContactInputModel model)
        {
            return string.IsNullOrWhiteSpace(model.Category)
                ? model.Message
                : $"[Category: {model.Category.Trim()}] {model.Message}";
        }
    }
}
