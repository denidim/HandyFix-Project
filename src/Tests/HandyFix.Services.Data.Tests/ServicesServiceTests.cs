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
            await service.CreateAsync("Leaky Pipe Repair", "Repairing leaky pipes quickly", 60.00m, 45, category.Id);

            Assert.Equal(1, dbContext.Services.Count());
            var created = dbContext.Services.First();
            Assert.Equal("Leaky Pipe Repair", created.Name);
            Assert.Equal(60.00m, created.BasePrice);
            Assert.Equal(45, created.EstimatedDurationMinutes);
            Assert.Equal(category.Id, created.CategoryId);
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

        [Fact]
        public async Task DeleteAsyncShouldRemoveTheImageBeforeTheRowIsGone()
        {
            // Order is load-bearing: the image path is derived from the service's slug, so once
            // the row is gone the file can no longer be located and would be orphaned in wwwroot
            // forever.
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
            await dbContext.SaveChangesAsync();

            var rowStillExistedWhenImageWasDeleted = false;
            var imageStorageServiceMock = new Mock<IImageStorageService>();
            imageStorageServiceMock
                .Setup(x => x.DeleteServiceImage("tap-repairs"))
                .Callback(() => rowStillExistedWhenImageWasDeleted =
                    dbContext.Services.IgnoreQueryFilters().Any(x => x.Id == serviceEntity.Id && !x.IsDeleted));

            var service = new ServicesService(repository, null, imageStorageServiceMock.Object);
            await service.DeleteAsync(serviceEntity.Id);

            imageStorageServiceMock.Verify(x => x.DeleteServiceImage("tap-repairs"), Times.Once);
            Assert.True(rowStillExistedWhenImageWasDeleted);

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

            imageStorageServiceMock.Verify(x => x.DeleteServiceImage(It.IsAny<string>()), Times.Never);
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
            await service.CreateAsync("Walton-on-Thames & Weybridge Repairs", "A test service", 60.00m, 45, category.Id);

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
            await service.UpdateAsync(serviceEntity.Id, "Tap Replacement", "new description", 75m, 60, false, newCategory.Id);

            var updated = dbContext.Services.First(x => x.Id == serviceEntity.Id);
            Assert.Equal("Tap Replacement", updated.Name);
            Assert.Equal("new description", updated.Description);
            Assert.Equal(75m, updated.BasePrice);
            Assert.Equal(60, updated.EstimatedDurationMinutes);
            Assert.False(updated.IsActive);
            Assert.Equal(newCategory.Id, updated.CategoryId);
            Assert.Equal("tap-replacement", updated.Slug);
        }

        [Fact]
        public async Task UpdateAsyncShouldDoNothingWhenServiceDoesNotExist()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);

            var service = new ServicesService(repository, null, new Mock<IImageStorageService>().Object);
            await service.UpdateAsync(Guid.NewGuid(), "Name", "Description", 10m, 10, true, Guid.NewGuid());

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

        [Fact]
        public async Task UpdateServiceImageAsyncShouldDeleteBothOldAndNewImagesWhenSlugChangesWithNewFile()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);
            using var imageRepository = new EfDeletableEntityRepository<ServiceImage>(dbContext);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);
            var serviceEntity = new Service { Id = Guid.NewGuid(), Name = "Tap Repair", Description = "d", Slug = "tap-replacement", BasePrice = 50, CategoryId = category.Id };
            dbContext.Services.Add(serviceEntity);
            await dbContext.SaveChangesAsync();

            var imageStorageServiceMock = new Mock<IImageStorageService>();
            imageStorageServiceMock
                .Setup(x => x.SaveServiceImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), "tap-replacement"))
                .ReturnsAsync("/images/services/tap-replacement-hero.webp");

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(x => x.Length).Returns(10);
            fileMock.Setup(x => x.FileName).Returns("photo.jpg");
            fileMock.Setup(x => x.ContentType).Returns("image/jpeg");
            fileMock.Setup(x => x.OpenReadStream()).Returns(new MemoryStream(new byte[10]));

            var service = new ServicesService(repository, imageRepository, imageStorageServiceMock.Object);
            await service.UpdateServiceImageAsync(serviceEntity.Id, "tap-repair", "tap-replacement", fileMock.Object);

            imageStorageServiceMock.Verify(x => x.DeleteServiceImage("tap-repair"), Times.Once);
            imageStorageServiceMock.Verify(x => x.DeleteServiceImage("tap-replacement"), Times.Once);
            Assert.Equal("/images/services/tap-replacement-hero.webp", dbContext.ServiceImages.First().ImageUrl);
        }

        [Fact]
        public async Task UpdateServiceImageAsyncShouldRenameImageWhenSlugChangesWithNoNewFile()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);
            using var imageRepository = new EfDeletableEntityRepository<ServiceImage>(dbContext);

            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);
            var serviceEntity = new Service { Id = Guid.NewGuid(), Name = "Tap Repair", Description = "d", Slug = "tap-replacement", BasePrice = 50, CategoryId = category.Id };
            dbContext.Services.Add(serviceEntity);
            await dbContext.SaveChangesAsync();

            var imageStorageServiceMock = new Mock<IImageStorageService>();
            imageStorageServiceMock
                .Setup(x => x.GetServiceImagePublicUrl("tap-replacement"))
                .Returns("/images/services/tap-replacement-hero.webp");

            var service = new ServicesService(repository, imageRepository, imageStorageServiceMock.Object);
            await service.UpdateServiceImageAsync(serviceEntity.Id, "tap-repair", "tap-replacement", null);

            imageStorageServiceMock.Verify(x => x.RenameServiceImage("tap-repair", "tap-replacement"), Times.Once);
            imageStorageServiceMock.Verify(x => x.DeleteServiceImage(It.IsAny<string>()), Times.Never);
            imageStorageServiceMock.Verify(
                x => x.SaveServiceImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
            Assert.Equal("/images/services/tap-replacement-hero.webp", dbContext.ServiceImages.First().ImageUrl);
        }

        [Fact]
        public async Task UpdateServiceImageAsyncShouldDoNothingWhenNoNewFileAndSlugUnchanged()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<Service>(dbContext);
            using var imageRepository = new EfDeletableEntityRepository<ServiceImage>(dbContext);

            var imageStorageServiceMock = new Mock<IImageStorageService>();

            var service = new ServicesService(repository, imageRepository, imageStorageServiceMock.Object);
            await service.UpdateServiceImageAsync(Guid.NewGuid(), "tap-repair", "tap-repair", null);

            imageStorageServiceMock.Verify(x => x.RenameServiceImage(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            imageStorageServiceMock.Verify(x => x.DeleteServiceImage(It.IsAny<string>()), Times.Never);
            imageStorageServiceMock.Verify(
                x => x.SaveServiceImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Never);
            Assert.Equal(0, dbContext.ServiceImages.Count());
        }
    }
}
