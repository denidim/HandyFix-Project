namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services;
    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Web.ViewModels.Administration.Enquiries;
    using HandyFix.Web.ViewModels.Home;

    using Microsoft.Data.Sqlite;
    using Microsoft.EntityFrameworkCore;

    using Moq;

    using Xunit;

    public class InquiriesServiceTests
    {
        [Fact]
        public async Task CreateInquiryAsyncShouldSaveToDatabaseWithImages()
        {
            using ApplicationDbContext dbContext = InMemoryContext();
            InquiriesService service = BuildService(dbContext);

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

            Assert.Single(inquiry.Images);
            Assert.Equal("/uploads/inquiries/some_image_file.jpg", inquiry.Images.First().ImageUrl);
        }

        // Guards PROJECT_STATE.md Section 3ax: the Contact page used to write the category into the
        // message in the browser before validation ran, so an empty message passed. The category is
        // now its own field and the prefix is added here, after the visitor's own text is validated.
        [Fact]
        public async Task CreateInquiryAsyncShouldPrefixMessageWithCategoryWhenProvided()
        {
            using ApplicationDbContext dbContext = InMemoryContext();
            InquiriesService service = BuildService(dbContext);

            await service.CreateInquiryAsync(
                new ContactInputModel
                {
                    Name = "Jane Doe",
                    Email = "jane@example.com",
                    PhoneNumber = "07123456789",
                    Message = "Kitchen tap is dripping.",
                    Category = "Plumbing",
                },
                new List<string>());

            Assert.Equal("[Category: Plumbing] Kitchen tap is dripping.", dbContext.Inquiries.Single().Message);
        }

        [Fact]
        public async Task GetAllAsyncShouldDefaultToCreatedOnDescending()
        {
            using ApplicationDbContext dbContext = InMemoryContext();

            var older = new Inquiry { Name = "Alice Older", Email = "alice@example.com", PhoneNumber = "07000000001", Message = "First message here." };
            var newer = new Inquiry { Name = "Bob Newer", Email = "bob@example.com", PhoneNumber = "07000000002", Message = "Second message here." };
            dbContext.Inquiries.Add(older);
            dbContext.Inquiries.Add(newer);
            await dbContext.SaveChangesAsync();

            // Back-date CreatedOn explicitly rather than relying on incidental
            // real-time gaps between saves.
            older.CreatedOn = DateTime.UtcNow.AddDays(-1);
            await dbContext.SaveChangesAsync();

            InquiriesService service = BuildService(dbContext);
            var results = (await service.GetAllAsync<EnquiryViewModel>()).ToList();

            Assert.Equal(newer.Id, results.First().Id);
            Assert.Equal(older.Id, results.Last().Id);
        }

        [Fact]
        public async Task GetAllAsyncShouldSortByNameAscendingWhenRequested()
        {
            using ApplicationDbContext dbContext = InMemoryContext();

            dbContext.Inquiries.Add(new Inquiry { Name = "Zack", Email = "zack@example.com", PhoneNumber = "07000000003", Message = "Needs a plumber urgently." });
            dbContext.Inquiries.Add(new Inquiry { Name = "Amy", Email = "amy@example.com", PhoneNumber = "07000000004", Message = "Needs a handyman urgently." });
            await dbContext.SaveChangesAsync();

            InquiriesService service = BuildService(dbContext);
            var results = (await service.GetAllAsync<EnquiryViewModel>(InquirySortField.Name, descending: false)).ToList();

            Assert.Equal("Amy", results.First().Name);
            Assert.Equal("Zack", results.Last().Name);
        }

        // "Delete Permanent" used to set IsDeleted and keep the row: the name, email, phone number
        // and message stayed in the database, hidden from every page, and the photos stayed in
        // storage (PROJECT_STATE.md Section 3cb). On Sqlite, because the photo rows point at the
        // enquiry with a real foreign key and InMemory would not notice them left behind.
        [Fact]
        public async Task DeleteAsyncShouldRemoveTheEnquiryItsPhotoRowsAndItsPhotosForGood()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            using ApplicationDbContext dbContext = SqliteContext(connection);
            Inquiry inquiry = await AddInquiryWithPhotosAsync(dbContext);
            Inquiry other = await AddInquiryWithPhotosAsync(dbContext, "https://photos.example/inquiries/other.jpg");

            var imageService = new Mock<IImageService>();
            IEnumerable<string> removedFromStorage = null;
            imageService
                .Setup(x => x.DeleteImagesAsync(It.IsAny<IEnumerable<string>>()))
                .Callback<IEnumerable<string>>(urls => removedFromStorage = urls.ToList())
                .Returns(Task.CompletedTask);

            await BuildService(dbContext, imageService).DeleteAsync(inquiry.Id);

            // IgnoreQueryFilters, because a soft delete would also pass a filtered check.
            Assert.Empty(dbContext.Inquiries.IgnoreQueryFilters().Where(x => x.Id == inquiry.Id));
            Assert.Empty(dbContext.InquiryImages.IgnoreQueryFilters().Where(x => x.InquiryId == inquiry.Id));
            Assert.Equal(
                new[] { "https://photos.example/inquiries/a.jpg", "https://photos.example/inquiries/b.jpg" },
                removedFromStorage.OrderBy(url => url));

            // Another enquiry and its photo are not touched.
            Assert.Single(dbContext.Inquiries.Where(x => x.Id == other.Id));
            Assert.Single(dbContext.InquiryImages.Where(x => x.InquiryId == other.Id));
        }

        [Fact]
        public async Task DeleteAsyncShouldKeepEverythingWhenThePhotosCannotBeRemovedFromStorage()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            using ApplicationDbContext dbContext = SqliteContext(connection);
            Inquiry inquiry = await AddInquiryWithPhotosAsync(dbContext);

            var imageService = new Mock<IImageService>();
            imageService
                .Setup(x => x.DeleteImagesAsync(It.IsAny<IEnumerable<string>>()))
                .ThrowsAsync(new InvalidOperationException("Storage is unreachable."));

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => BuildService(dbContext, imageService).DeleteAsync(inquiry.Id));

            // The rows are what the admin's second try works from.
            Assert.Single(dbContext.Inquiries.Where(x => x.Id == inquiry.Id));
            Assert.Equal(2, dbContext.InquiryImages.Count(x => x.InquiryId == inquiry.Id));
        }

        [Fact]
        public async Task DeleteAsyncShouldDoNothingForAnUnknownEnquiry()
        {
            using var connection = new SqliteConnection("DataSource=:memory:");
            using ApplicationDbContext dbContext = SqliteContext(connection);
            await AddInquiryWithPhotosAsync(dbContext);

            var imageService = new Mock<IImageService>();

            await BuildService(dbContext, imageService).DeleteAsync(Guid.NewGuid());

            Assert.Single(dbContext.Inquiries);
            imageService.Verify(x => x.DeleteImagesAsync(It.IsAny<IEnumerable<string>>()), Times.Never);
        }

        private static InquiriesService BuildService(ApplicationDbContext dbContext, Mock<IImageService> imageService = null)
        {
            return new InquiriesService(
                new EfDeletableEntityRepository<Inquiry>(dbContext),
                new EfDeletableEntityRepository<InquiryImage>(dbContext),
                (imageService ?? new Mock<IImageService>()).Object);
        }

        private static ApplicationDbContext InMemoryContext()
        {
            return new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options);
        }

        private static ApplicationDbContext SqliteContext(SqliteConnection connection)
        {
            connection.Open();

            var dbContext = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            dbContext.Database.EnsureCreated();
            return dbContext;
        }

        private static async Task<Inquiry> AddInquiryWithPhotosAsync(ApplicationDbContext dbContext, params string[] photoUrls)
        {
            if (photoUrls.Length == 0)
            {
                photoUrls = new[] { "https://photos.example/inquiries/a.jpg", "https://photos.example/inquiries/b.jpg" };
            }

            var inquiry = new Inquiry
            {
                Name = "Jane Doe",
                Email = "jane@example.com",
                PhoneNumber = "07123456789",
                Message = "Kitchen tap is dripping.",
            };

            foreach (var url in photoUrls)
            {
                inquiry.Images.Add(new InquiryImage { ImageUrl = url });
            }

            dbContext.Inquiries.Add(inquiry);
            await dbContext.SaveChangesAsync();
            return inquiry;
        }
    }
}
