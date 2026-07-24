namespace HandyFix.Services.Data.Inquiries
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;
    using HandyFix.Web.ViewModels.Administration.Enquiries;
    using HandyFix.Web.ViewModels.Home;

    using Microsoft.EntityFrameworkCore;

    public class InquiriesService : IInquiriesService
    {
        private readonly IDeletableEntityRepository<Inquiry> inquiryRepository;
        private readonly IDeletableEntityRepository<InquiryImage> imageRepository;

        public InquiriesService(
            IDeletableEntityRepository<Inquiry> inquiryRepository,
            IDeletableEntityRepository<InquiryImage> imageRepository)
        {
            this.inquiryRepository = inquiryRepository;
            this.imageRepository = imageRepository;
        }

        public async Task CreateInquiryAsync(ContactInputModel model, IReadOnlyList<string> imageUrls)
        {
            var inquiry = new Inquiry
            {
                Name = model.Name,
                Email = model.Email,
                PhoneNumber = model.PhoneNumber,
                Message = model.Message,
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
            var query = this.inquiryRepository.All();

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

        public async Task DeleteAsync(Guid id)
        {
            var inquiry = await this.inquiryRepository.All()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (inquiry != null)
            {
                this.inquiryRepository.Delete(inquiry);
                await this.inquiryRepository.SaveChangesAsync();
            }
        }
    }
}
