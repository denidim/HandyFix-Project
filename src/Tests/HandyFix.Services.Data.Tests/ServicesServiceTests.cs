namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;

    using Moq;

    using Xunit;

    public class ServicesServiceTests
    {
        // An address the storage service gives an upload in the photo bucket (PROJECT_STATE.md
        // Section 3cq), and one it gives the next.
        private const string UploadedPicture = "https://photos.example/services/0123456789abcdef0123456789abcdef_tap-repairs.webp";
        private const string NextUploadedPicture = "https://photos.example/services/fedcba9876543210fedcba9876543210_tap-repairs.webp";

        [Fact]
        public async Task CreateAsyncShouldAddServiceToDatabase()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
            
            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);
            
            // Seed a category
            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);
            await dbContext.SaveChangesAsync();

            var service = new ServicesService(repository, null, new Mock<IImageStorageService>().Object);
            await service.CreateAsync("Leaky Pipe Repair", "Repairing leaky pipes quickly", 60.00m, 45, category.Id, 7, true);

            Assert.Equal(1, dbContext.Services.Count());
            var created = dbContext.Services.First();
            Assert.Equal("Leaky Pipe Repair", created.Name);
            Assert.Equal(60.00m, created.BasePrice);
            Assert.Equal(45, created.EstimatedDurationMinutes);
            Assert.Equal(category.Id, created.CategoryId);
            Assert.Equal(7, created.DisplayOrder);
            Assert.True(created.IsPopular);
        }

        [Fact]
        public async Task DeleteAsyncShouldSoftDeleteService()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;
            
            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);

            var serviceEntity = new Service 
            { 
                Id = Guid.NewGuid(), 
                Name = "Handyman fixing squeaks", 
                Description = "some description",
                Slug = "handyman-fixing-squeaks",
                BasePrice = 50,
                CategoryId = category.Id
            };
            dbContext.Services.Add(serviceEntity);
            await dbContext.SaveChangesAsync();

            var service = new ServicesService(repository, null, new Mock<IImageStorageService>().Object);
            await service.DeleteAsync(serviceEntity.Id);

            var inDb = dbContext.Services.IgnoreQueryFilters().FirstOrDefault(x => x.Id == serviceEntity.Id);
            Assert.NotNull(inDb);
            Assert.True(inDb.IsDeleted);
        }

        // The service goes first and its picture after, so a picture is never missing from a service
        // still shown. Until PROJECT_STATE.md Section 3cq it was the other way round, because the
        // picture's file was named after the slug; its address is in the database now. The picture
        // is handed over whatever it is: the storage service leaves a built-in one alone.
        [Fact]
        public async Task DeleteAsyncShouldRemoveTheServiceAndThenHandOverItsPicture()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);

            var serviceEntity = new Service
            {
                Id = Guid.NewGuid(),
                Name = "Tap Repairs",
                Description = "some description",
                Slug = "tap-repairs",
                BasePrice = 50,
                CategoryId = category.Id,
            };
            dbContext.Services.Add(serviceEntity);
            dbContext.ServiceImages.Add(new ServiceImage { ServiceId = serviceEntity.Id, ImageUrl = UploadedPicture });
            await dbContext.SaveChangesAsync();

            var serviceWasGoneFirst = false;
            var imageStorageServiceMock = new Mock<IImageStorageService>();
            imageStorageServiceMock
                .Setup(x => x.DeleteServiceImageAsync(UploadedPicture))
                .Callback(() => serviceWasGoneFirst =
                    dbContext.Services.IgnoreQueryFilters().Any(x => x.Id == serviceEntity.Id && x.IsDeleted))
                .Returns(Task.CompletedTask);

            var service = new ServicesService(repository, null, imageStorageServiceMock.Object);
            await service.DeleteAsync(serviceEntity.Id);

            imageStorageServiceMock.Verify(x => x.DeleteServiceImageAsync(UploadedPicture), Times.Once);
            Assert.True(serviceWasGoneFirst);

            var inDb = dbContext.Services.IgnoreQueryFilters().First(x => x.Id == serviceEntity.Id);
            Assert.True(inDb.IsDeleted);
        }

        [Fact]
        public async Task DeleteAsyncOnAnUnknownServiceShouldNotAttemptAnImageDelete()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);

            var imageStorageServiceMock = new Mock<IImageStorageService>();
            var service = new ServicesService(repository, null, imageStorageServiceMock.Object);

            await service.DeleteAsync(Guid.NewGuid());

            imageStorageServiceMock.Verify(x => x.DeleteServiceImageAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateAsyncShouldCollapseMultipleHyphensInSlug()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);
            await dbContext.SaveChangesAsync();

            var service = new ServicesService(repository, null, new Mock<IImageStorageService>().Object);
            await service.CreateAsync("Walton-on-Thames & Weybridge Repairs", "A test service", 60.00m, 45, category.Id, Service.DefaultDisplayOrder, false);

            var created = dbContext.Services.First();
            Assert.Equal("walton-on-thames-weybridge-repairs", created.Slug);
            Assert.DoesNotContain("--", created.Slug);
        }

        [Fact]
        public async Task GetAllAsyncShouldOnlyReturnActiveServicesByDefault()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);
            dbContext.Services.Add(new Service { Name = "Active Service", Description = "d", Slug = "active-service", BasePrice = 50, CategoryId = category.Id, IsActive = true });
            dbContext.Services.Add(new Service { Name = "Inactive Service", Description = "d", Slug = "inactive-service", BasePrice = 50, CategoryId = category.Id, IsActive = false });
            await dbContext.SaveChangesAsync();

            var service = new ServicesService(repository, null, new Mock<IImageStorageService>().Object);
            var results = (await service.GetAllAsync<ServiceViewModel>()).ToList();

            Assert.Single(results);
            Assert.Equal("Active Service", results.First().Name);
        }

        [Fact]
        public async Task GetAllAsyncShouldReturnAllServicesWhenActiveOnlyIsFalse()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);
            dbContext.Services.Add(new Service { Name = "Active Service", Description = "d", Slug = "active-service", BasePrice = 50, CategoryId = category.Id, IsActive = true });
            dbContext.Services.Add(new Service { Name = "Inactive Service", Description = "d", Slug = "inactive-service", BasePrice = 50, CategoryId = category.Id, IsActive = false });
            await dbContext.SaveChangesAsync();

            var service = new ServicesService(repository, null, new Mock<IImageStorageService>().Object);
            var results = (await service.GetAllAsync<ServiceViewModel>(activeOnly: false)).ToList();

            Assert.Equal(2, results.Count);
        }

        [Fact]
        public async Task GetByCategoryAsyncShouldMatchCategoryNameCaseInsensitively()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);
            dbContext.Services.Add(new Service { Name = "Tap Repair", Description = "d", Slug = "tap-repair", BasePrice = 50, CategoryId = category.Id, IsActive = true });
            await dbContext.SaveChangesAsync();

            var service = new ServicesService(repository, null, new Mock<IImageStorageService>().Object);
            var results = (await service.GetByCategoryAsync<ServiceViewModel>("PLUMBING")).ToList();

            Assert.Single(results);
            Assert.Equal("Tap Repair", results.First().Name);
        }

        [Fact]
        public async Task GetByCategoryAsyncShouldRespectActiveOnlyFilter()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);
            dbContext.Services.Add(new Service { Name = "Active Service", Description = "d", Slug = "active-service", BasePrice = 50, CategoryId = category.Id, IsActive = true });
            dbContext.Services.Add(new Service { Name = "Inactive Service", Description = "d", Slug = "inactive-service", BasePrice = 50, CategoryId = category.Id, IsActive = false });
            await dbContext.SaveChangesAsync();

            var service = new ServicesService(repository, null, new Mock<IImageStorageService>().Object);
            var results = (await service.GetByCategoryAsync<ServiceViewModel>("Plumbing")).ToList();

            Assert.Single(results);
            Assert.Equal("Active Service", results.First().Name);
        }

        [Fact]
        public async Task ListsShouldOrderByDisplayOrderThenName()
        {
            // Lists were alphabetical only, so a category's general call-out sat wherever its name
            // put it and "popular" was the first four in the alphabet (PROJECT_STATE Section 3ca).
            // A lower display order comes first; services sharing a number fall back to the name.
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Handyman", Description = "general repairs", Slug = "handyman" };
            dbContext.ServiceCategories.Add(category);
            dbContext.Services.Add(new Service { Name = "Bravo", Description = "d", Slug = "bravo", BasePrice = 50, CategoryId = category.Id });
            dbContext.Services.Add(new Service { Name = "Zulu Call-Out", Description = "d", Slug = "zulu-call-out", BasePrice = 50, CategoryId = category.Id, DisplayOrder = 0 });
            dbContext.Services.Add(new Service { Name = "Alpha", Description = "d", Slug = "alpha", BasePrice = 50, CategoryId = category.Id, IsPopular = true });
            await dbContext.SaveChangesAsync();

            var service = new ServicesService(repository, null, new Mock<IImageStorageService>().Object);
            var all = (await service.GetAllAsync<ServiceViewModel>()).ToList();
            var byCategory = (await service.GetByCategoryAsync<ServiceViewModel>("Handyman")).ToList();

            Assert.Equal(new[] { "Zulu Call-Out", "Alpha", "Bravo" }, all.Select(s => s.Name));
            Assert.Equal(new[] { "Zulu Call-Out", "Alpha", "Bravo" }, byCategory.Select(s => s.Name));

            // The view model carries the fields the home page and the admin list read.
            Assert.Equal(new[] { 0, Service.DefaultDisplayOrder, Service.DefaultDisplayOrder }, all.Select(s => s.DisplayOrder));
            Assert.Equal(new[] { false, true, false }, all.Select(s => s.IsPopular));
            Assert.All(all, s => Assert.True(s.IsActive));
        }

        [Fact]
        public async Task NameIsTakenAsyncShouldCountDeletedServicesButNotTheOneBeingEdited()
        {
            // The slug comes from the name and its unique index covers deleted rows. Saving a name
            // already in use, or once used by a deleted service, failed with an error page
            // (PROJECT_STATE Section 3ca); the admin form now asks here first.
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Handyman", Description = "general repairs", Slug = "handyman" };
            dbContext.ServiceCategories.Add(category);
            var live = new Service { Name = "Gutter Clearing", Description = "d", Slug = "gutter-clearing", BasePrice = 60, CategoryId = category.Id };
            var deleted = new Service { Name = "Fence Repairs", Description = "d", Slug = "fence-repairs", BasePrice = 60, CategoryId = category.Id, IsDeleted = true };
            dbContext.Services.AddRange(live, deleted);
            await dbContext.SaveChangesAsync();

            var service = new ServicesService(repository, null, new Mock<IImageStorageService>().Object);

            Assert.True(await service.NameIsTakenAsync("Gutter Clearing"));
            Assert.True(await service.NameIsTakenAsync("gutter clearing"));
            Assert.True(await service.NameIsTakenAsync("Fence Repairs"));
            Assert.False(await service.NameIsTakenAsync("Gutter Clearing", live.Id));
            Assert.True(await service.NameIsTakenAsync("Fence Repairs", live.Id));
            Assert.False(await service.NameIsTakenAsync("Gutter Guards"));
        }

        [Fact]
        public async Task GetByIdAsyncShouldReturnNullWhenServiceDoesNotExist()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);

            var service = new ServicesService(repository, null, new Mock<IImageStorageService>().Object);
            var result = await service.GetByIdAsync<ServiceViewModel>(Guid.NewGuid());

            Assert.Null(result);
        }

        [Fact]
        public async Task GetBySlugAsyncShouldReturnNullWhenServiceDoesNotExist()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);

            var service = new ServicesService(repository, null, new Mock<IImageStorageService>().Object);
            var result = await service.GetBySlugAsync<ServiceViewModel>("does-not-exist");

            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateAsyncShouldModifyExistingServiceAndRegenerateSlug()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);

            var originalCategory = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            var newCategory = new ServiceCategory { Id = Guid.NewGuid(), Name = "Handyman", Description = "general repairs", Slug = "handyman" };
            dbContext.ServiceCategories.Add(originalCategory);
            dbContext.ServiceCategories.Add(newCategory);

            var serviceEntity = new Service { Id = Guid.NewGuid(), Name = "Tap Repair", Description = "old description", Slug = "tap-repair", BasePrice = 50, EstimatedDurationMinutes = 30, IsActive = true, CategoryId = originalCategory.Id };
            dbContext.Services.Add(serviceEntity);
            await dbContext.SaveChangesAsync();

            var service = new ServicesService(repository, null, new Mock<IImageStorageService>().Object);
            await service.UpdateAsync(serviceEntity.Id, "Tap Replacement", "new description", 75m, 60, false, newCategory.Id, 5, true);

            var updated = dbContext.Services.First(x => x.Id == serviceEntity.Id);
            Assert.Equal("Tap Replacement", updated.Name);
            Assert.Equal("new description", updated.Description);
            Assert.Equal(75m, updated.BasePrice);
            Assert.Equal(60, updated.EstimatedDurationMinutes);
            Assert.False(updated.IsActive);
            Assert.Equal(newCategory.Id, updated.CategoryId);
            Assert.Equal("tap-replacement", updated.Slug);
            Assert.Equal(5, updated.DisplayOrder);
            Assert.True(updated.IsPopular);
        }

        [Fact]
        public async Task UpdateAsyncShouldDoNothingWhenServiceDoesNotExist()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);

            var service = new ServicesService(repository, null, new Mock<IImageStorageService>().Object);
            await service.UpdateAsync(Guid.NewGuid(), "Name", "Description", 10m, 10, true, Guid.NewGuid(), Service.DefaultDisplayOrder, false);

            Assert.Equal(0, dbContext.Services.Count());
        }

        [Fact]
        public async Task AddOrUpdateServiceImageAsyncShouldCreateNewImageWhenNoneExists()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);
            using var imageRepository = new EfDeletableEntityRepository<ServiceImage>(dbContext);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);
            var serviceEntity = new Service { Id = Guid.NewGuid(), Name = "Tap Repair", Description = "d", Slug = "tap-repair", BasePrice = 50, CategoryId = category.Id };
            dbContext.Services.Add(serviceEntity);
            await dbContext.SaveChangesAsync();

            var service = new ServicesService(repository, imageRepository, new Mock<IImageStorageService>().Object);
            await service.AddOrUpdateServiceImageAsync(serviceEntity.Id, "/images/services/tap-repair-hero.webp");

            Assert.Equal(1, dbContext.ServiceImages.Count());
            Assert.Equal("/images/services/tap-repair-hero.webp", dbContext.ServiceImages.First().ImageUrl);
        }

        [Fact]
        public async Task AddOrUpdateServiceImageAsyncShouldUpdateExistingImageInPlaceRatherThanDuplicating()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);
            using var imageRepository = new EfDeletableEntityRepository<ServiceImage>(dbContext);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);
            var serviceEntity = new Service { Id = Guid.NewGuid(), Name = "Tap Repair", Description = "d", Slug = "tap-repair", BasePrice = 50, CategoryId = category.Id };
            dbContext.Services.Add(serviceEntity);
            dbContext.ServiceImages.Add(new ServiceImage { ServiceId = serviceEntity.Id, ImageUrl = "/images/services/tap-repair-hero.webp" });
            await dbContext.SaveChangesAsync();

            var service = new ServicesService(repository, imageRepository, new Mock<IImageStorageService>().Object);
            await service.AddOrUpdateServiceImageAsync(serviceEntity.Id, "/images/services/tap-repair-hero-v2.webp");

            Assert.Equal(1, dbContext.ServiceImages.Count());
            Assert.Equal("/images/services/tap-repair-hero-v2.webp", dbContext.ServiceImages.First().ImageUrl);
        }

        [Fact]
        public async Task AddOrUpdateServiceImageAsyncShouldDoNothingWhenUrlIsEmpty()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);
            using var imageRepository = new EfDeletableEntityRepository<ServiceImage>(dbContext);

            var service = new ServicesService(repository, imageRepository, new Mock<IImageStorageService>().Object);
            await service.AddOrUpdateServiceImageAsync(Guid.NewGuid(), string.Empty);

            Assert.Equal(0, dbContext.ServiceImages.Count());
        }

        [Fact]
        public async Task SetServiceImageAsyncShouldDoNothingWhenFileIsNullOrEmpty()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);
            using var imageRepository = new EfDeletableEntityRepository<ServiceImage>(dbContext);

            var imageStorageServiceMock = new Mock<IImageStorageService>();
            var emptyFileMock = new Mock<IFormFile>();
            emptyFileMock.Setup(x => x.Length).Returns(0);

            var service = new ServicesService(repository, imageRepository, imageStorageServiceMock.Object);
            await service.SetServiceImageAsync(Guid.NewGuid(), null);
            await service.SetServiceImageAsync(Guid.NewGuid(), emptyFileMock.Object);

            imageStorageServiceMock.Verify(
                x => x.SaveServiceImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task SetServiceImageAsyncShouldDoNothingWhenServiceDoesNotExist()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);
            using var imageRepository = new EfDeletableEntityRepository<ServiceImage>(dbContext);

            var imageStorageServiceMock = new Mock<IImageStorageService>();
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(x => x.Length).Returns(10);

            var service = new ServicesService(repository, imageRepository, imageStorageServiceMock.Object);
            await service.SetServiceImageAsync(Guid.NewGuid(), fileMock.Object);

            imageStorageServiceMock.Verify(
                x => x.SaveServiceImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task SetServiceImageAsyncShouldSaveAndLinkImageForExistingService()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);
            using var imageRepository = new EfDeletableEntityRepository<ServiceImage>(dbContext);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);
            var serviceEntity = new Service { Id = Guid.NewGuid(), Name = "Tap Repair", Description = "d", Slug = "tap-repair", BasePrice = 50, CategoryId = category.Id };
            dbContext.Services.Add(serviceEntity);
            await dbContext.SaveChangesAsync();

            var imageStorageServiceMock = new Mock<IImageStorageService>();
            imageStorageServiceMock
                .Setup(x => x.SaveServiceImageAsync(It.IsAny<Stream>(), "photo.jpg", "image/jpeg", "tap-repair"))
                .ReturnsAsync("/images/services/tap-repair-hero.webp");

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(x => x.Length).Returns(10);
            fileMock.Setup(x => x.FileName).Returns("photo.jpg");
            fileMock.Setup(x => x.ContentType).Returns("image/jpeg");
            fileMock.Setup(x => x.OpenReadStream()).Returns(new MemoryStream(new byte[10]));

            var service = new ServicesService(repository, imageRepository, imageStorageServiceMock.Object);
            await service.SetServiceImageAsync(serviceEntity.Id, fileMock.Object);

            Assert.Equal(1, dbContext.ServiceImages.Count());
            var image = dbContext.ServiceImages.First();
            Assert.Equal(serviceEntity.Id, image.ServiceId);
            Assert.Equal("/images/services/tap-repair-hero.webp", image.ImageUrl);
        }

        // A new picture goes up, the service points at it, and only then does the admin's previous
        // upload leave the bucket: a failure at any step leaves the service with a picture
        // (PROJECT_STATE.md Section 3cq).
        [Fact]
        public async Task SetServiceImageAsyncShouldHandOverThePreviousPictureOnlyOnceTheNewOneIsLinked()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);
            using var imageRepository = new EfDeletableEntityRepository<ServiceImage>(dbContext);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);
            var serviceEntity = new Service { Id = Guid.NewGuid(), Name = "Tap Repairs", Description = "d", Slug = "tap-repairs", BasePrice = 50, CategoryId = category.Id };
            dbContext.Services.Add(serviceEntity);
            dbContext.ServiceImages.Add(new ServiceImage { ServiceId = serviceEntity.Id, ImageUrl = UploadedPicture });
            await dbContext.SaveChangesAsync();

            string linkedWhenTheOldOneWent = null;
            var imageStorageServiceMock = new Mock<IImageStorageService>();
            imageStorageServiceMock
                .Setup(x => x.SaveServiceImageAsync(It.IsAny<Stream>(), "photo.jpg", "image/jpeg", "tap-repairs"))
                .ReturnsAsync(NextUploadedPicture);
            imageStorageServiceMock
                .Setup(x => x.DeleteServiceImageAsync(UploadedPicture))
                .Callback(() => linkedWhenTheOldOneWent = dbContext.ServiceImages.Single().ImageUrl)
                .Returns(Task.CompletedTask);

            var service = new ServicesService(repository, imageRepository, imageStorageServiceMock.Object);
            await service.SetServiceImageAsync(serviceEntity.Id, Upload());

            Assert.Equal(NextUploadedPicture, dbContext.ServiceImages.Single().ImageUrl);
            imageStorageServiceMock.Verify(x => x.DeleteServiceImageAsync(UploadedPicture), Times.Once);
            Assert.Equal(NextUploadedPicture, linkedWhenTheOldOneWent);
        }

        // A rename used to move the picture's file inside the container, and the next deploy, which
        // starts a container from what git holds, left the service pointing at a file it did not
        // have. The picture is not touched by a rename now, built-in or uploaded.
        [Theory]
        [InlineData("/images/services/tap-repairs-hero.webp")]
        [InlineData(UploadedPicture)]
        public async Task ARenameLeavesThePictureWhereItIs(string picture)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);
            using var imageRepository = new EfDeletableEntityRepository<ServiceImage>(dbContext);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);
            var serviceEntity = new Service { Id = Guid.NewGuid(), Name = "Tap Repairs", Description = "some description", Slug = "tap-repairs", BasePrice = 50, CategoryId = category.Id };
            dbContext.Services.Add(serviceEntity);
            dbContext.ServiceImages.Add(new ServiceImage { ServiceId = serviceEntity.Id, ImageUrl = picture });
            await dbContext.SaveChangesAsync();

            var imageStorageServiceMock = new Mock<IImageStorageService>();
            var service = new ServicesService(repository, imageRepository, imageStorageServiceMock.Object);

            await service.UpdateAsync(serviceEntity.Id, "Tap Fixing", "some description", 50m, 60, true, category.Id, Service.DefaultDisplayOrder, false);
            await service.SetServiceImageAsync(serviceEntity.Id, null);

            Assert.Equal("tap-fixing", dbContext.Services.Single().Slug);
            Assert.Equal(picture, dbContext.ServiceImages.Single().ImageUrl);
            imageStorageServiceMock.VerifyNoOtherCalls();
            Assert.Equal(picture, (await service.GetBySlugAsync<ServiceViewModel>("tap-fixing")).ImageUrl);
        }

        // A file as the admin's form sends it.
        private static IFormFile Upload()
        {
            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(x => x.Length).Returns(10);
            fileMock.Setup(x => x.FileName).Returns("photo.jpg");
            fileMock.Setup(x => x.ContentType).Returns("image/jpeg");
            fileMock.Setup(x => x.OpenReadStream()).Returns(new MemoryStream(new byte[10]));
            return fileMock.Object;
        }
    }
}
