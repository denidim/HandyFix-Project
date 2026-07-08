namespace HandyFix.Services.Data.Inquiries
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Common.Repositories;
    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

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

        public async Task CreateInquiryAsync(string name, string email, string phoneNumber, string message, IEnumerable<string> imageUrls = null)
        {
            var inquiry = new Inquiry
            {
                Name = name,
                Email = email,
                PhoneNumber = phoneNumber,
                Message = message,
            };

            await this.inquiryRepository.AddAsync(inquiry);
            await this.inquiryRepository.SaveChangesAsync();

            if (imageUrls != null)
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

        public async Task<IEnumerable<T>> GetAllAsync<T>()
        {
            return await this.inquiryRepository.All()
                .OrderByDescending(x => x.CreatedOn)
                .To<T>()
                .ToListAsync();
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
