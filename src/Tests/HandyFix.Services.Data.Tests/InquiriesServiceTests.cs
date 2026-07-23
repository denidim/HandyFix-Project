namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Web.ViewModels.Home;

    using Microsoft.EntityFrameworkCore;

    using Xunit;

    public class InquiriesServiceTests
    {
        [Fact]
        public async Task CreateInquiryAsyncShouldSaveToDatabaseWithImages()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
            
            using var dbContext = new ApplicationDbContext(options);
            using var inquiryRepository = new EfDeletableEntityRepository<Inquiry>(dbContext);
            using var imageRepository = new EfDeletableEntityRepository<InquiryImage>(dbContext);

            var service = new InquiriesService(inquiryRepository, imageRepository);
            
            var imageUrls = new List<string> { "/uploads/inquiries/some_image_file.jpg" };
            var model = new ContactInputModel
            {
                Name = "Jane Doe",
                Email = "jane@example.com",
                PhoneNumber = "07123456789",
                Message = "Needs leak repairs urgently.",
            };

            await service.CreateInquiryAsync(model, imageUrls);

            Assert.Equal(1, dbContext.Inquiries.Count());
            var inquiry = dbContext.Inquiries.Include(x => x.Images).First();
            Assert.Equal("Jane Doe", inquiry.Name);
            Assert.Equal("jane@example.com", inquiry.Email);
            Assert.Equal("07123456789", inquiry.PhoneNumber);
            Assert.Equal("Needs leak repairs urgently.", inquiry.Message);
            
            Assert.Equal(1, inquiry.Images.Count);
            Assert.Equal("/uploads/inquiries/some_image_file.jpg", inquiry.Images.First().ImageUrl);
        }
    }
}
