namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services.Data.Services;

    using Microsoft.EntityFrameworkCore;

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

            var service = new ServicesService(repository);
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

            var service = new ServicesService(repository);
            await service.DeleteAsync(serviceEntity.Id);

            var inDb = dbContext.Services.IgnoreQueryFilters().FirstOrDefault(x => x.Id == serviceEntity.Id);
            Assert.NotNull(inDb);
            Assert.True(inDb.IsDeleted);
        }
    }
}
