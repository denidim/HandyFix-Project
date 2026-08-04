namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services;
    using HandyFix.Services.Data.Services;

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
    }
}
