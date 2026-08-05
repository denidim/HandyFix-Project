namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services.Data.Categories;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.EntityFrameworkCore;

    using Xunit;

    public class CategoriesServiceTests
    {
        [Fact]
        public async Task CreateAsyncShouldAddCategoryToDatabase()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceCategory>(dbContext);

            var service = new CategoriesService(repository);
            await service.CreateAsync("Plumbing", "Leaking pipes repairs");

            Assert.Equal(1, dbContext.ServiceCategories.Count());
            Assert.Equal("Plumbing", dbContext.ServiceCategories.First().Name);
        }

        [Fact]
        public async Task GetAllAsyncShouldReturnCategoriesOrderedByName()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceCategory>(dbContext);

            dbContext.ServiceCategories.Add(new ServiceCategory { Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" });
            dbContext.ServiceCategories.Add(new ServiceCategory { Name = "Handyman", Description = "general repairs", Slug = "handyman" });
            await dbContext.SaveChangesAsync();

            var service = new CategoriesService(repository);
            var results = (await service.GetAllAsync<CategoryViewModel>()).ToList();

            Assert.Equal("Handyman", results.First().Name);
            Assert.Equal("Plumbing", results.Last().Name);
        }

        [Fact]
        public async Task GetAllAsyncShouldComputeBasePriceAsMinimumOfItsServices()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceCategory>(dbContext);

            var category = new ServiceCategory { Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" };
            dbContext.ServiceCategories.Add(category);
            dbContext.Services.Add(new Service { Name = "Tap Repair", Description = "d", Slug = "tap-repair", BasePrice = 80m, CategoryId = category.Id, IsActive = true });
            dbContext.Services.Add(new Service { Name = "Leak Fix", Description = "d", Slug = "leak-fix", BasePrice = 45m, CategoryId = category.Id, IsActive = true });
            await dbContext.SaveChangesAsync();

            var service = new CategoriesService(repository);
            var result = (await service.GetAllAsync<CategoryViewModel>()).First();

            Assert.Equal(45m, result.BasePrice);
        }

        [Fact]
        public async Task GetAllAsyncShouldReturnZeroBasePriceWhenCategoryHasNoServices()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceCategory>(dbContext);

            dbContext.ServiceCategories.Add(new ServiceCategory { Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" });
            await dbContext.SaveChangesAsync();

            var service = new CategoriesService(repository);
            var result = (await service.GetAllAsync<CategoryViewModel>()).First();

            Assert.Equal(0m, result.BasePrice);
        }

        [Fact]
        public async Task GetBySlugAsyncShouldMatchSlugCaseInsensitively()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceCategory>(dbContext);

            dbContext.ServiceCategories.Add(new ServiceCategory { Name = "Plumbing", Description = "leak repairs", Slug = "plumbing" });
            await dbContext.SaveChangesAsync();

            var service = new CategoriesService(repository);
            var result = await service.GetBySlugAsync<CategoryViewModel>("PLUMBING");

            Assert.NotNull(result);
            Assert.Equal("Plumbing", result.Name);
        }

        [Fact]
        public async Task GetBySlugAsyncShouldReturnNullWhenNoCategoryMatches()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceCategory>(dbContext);

            var service = new CategoriesService(repository);
            var result = await service.GetBySlugAsync<CategoryViewModel>("does-not-exist");

            Assert.Null(result);
        }
    }
}
