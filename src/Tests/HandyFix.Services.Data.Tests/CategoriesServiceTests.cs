namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services.Data.Categories;

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
    }
}
