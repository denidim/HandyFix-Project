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
    using HandyFix.Web.ViewModels.Administration.Enquiries;
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

        [Fact]
        public async Task GetAllAsyncShouldDefaultToCreatedOnDescending()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var inquiryRepository = new EfDeletableEntityRepository<Inquiry>(dbContext);
            using var imageRepository = new EfDeletableEntityRepository<InquiryImage>(dbContext);

            var older = new Inquiry { Name = "Alice Older", Email = "alice@example.com", PhoneNumber = "07000000001", Message = "First message here." };
            var newer = new Inquiry { Name = "Bob Newer", Email = "bob@example.com", PhoneNumber = "07000000002", Message = "Second message here." };
            dbContext.Inquiries.Add(older);
            dbContext.Inquiries.Add(newer);
            await dbContext.SaveChangesAsync();

            // Back-date CreatedOn explicitly rather than relying on incidental
            // real-time gaps between saves.
            older.CreatedOn = DateTime.UtcNow.AddDays(-1);
            await dbContext.SaveChangesAsync();

            var service = new InquiriesService(inquiryRepository, imageRepository);
            var results = (await service.GetAllAsync<EnquiryViewModel>()).ToList();

            Assert.Equal(newer.Id, results.First().Id);
            Assert.Equal(older.Id, results.Last().Id);
        }

        [Fact]
        public async Task GetAllAsyncShouldSortByNameAscendingWhenRequested()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var inquiryRepository = new EfDeletableEntityRepository<Inquiry>(dbContext);
            using var imageRepository = new EfDeletableEntityRepository<InquiryImage>(dbContext);

            dbContext.Inquiries.Add(new Inquiry { Name = "Zack", Email = "zack@example.com", PhoneNumber = "07000000003", Message = "Needs a plumber urgently." });
            dbContext.Inquiries.Add(new Inquiry { Name = "Amy", Email = "amy@example.com", PhoneNumber = "07000000004", Message = "Needs a handyman urgently." });
            await dbContext.SaveChangesAsync();

            var service = new InquiriesService(inquiryRepository, imageRepository);
            var results = (await service.GetAllAsync<EnquiryViewModel>(InquirySortField.Name, descending: false)).ToList();

            Assert.Equal("Amy", results.First().Name);
            Assert.Equal("Zack", results.Last().Name);
        }
    }
}
