namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Web.ViewModels.ServiceAreas;

    using Microsoft.EntityFrameworkCore;

    using Xunit;

    public class ServiceAreasServiceTests
    {
        [Fact]
        public async Task GetAllAsyncShouldOrderFeaturedAreasFirstByDefault()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var notFeatured = CreateArea("dorking", "Dorking", isFeatured: false, displayOrder: 1);
            var featured = CreateArea("guildford", "Guildford", isFeatured: true, displayOrder: 2);
            dbContext.ServiceAreas.AddRange(notFeatured, featured);
            await dbContext.SaveChangesAsync();

            var service = new ServiceAreasService(repository);
            var results = (await service.GetAllAsync<ServiceArea>()).ToList();

            Assert.Equal(featured.Id, results.First().Id);
            Assert.Equal(notFeatured.Id, results.Last().Id);
        }

        [Fact]
        public async Task GetAllAsyncShouldOrderByDisplayOrderWhenFeaturedFirstIsFalse()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var second = CreateArea("dorking", "Dorking", isFeatured: false, displayOrder: 2);
            var first = CreateArea("guildford", "Guildford", isFeatured: true, displayOrder: 1);
            dbContext.ServiceAreas.AddRange(second, first);
            await dbContext.SaveChangesAsync();

            var service = new ServiceAreasService(repository);
            var results = (await service.GetAllAsync<ServiceArea>(featuredFirst: false)).ToList();

            Assert.Equal(first.Id, results.First().Id);
            Assert.Equal(second.Id, results.Last().Id);
        }

        [Fact]
        public async Task GetBySlugAsyncShouldReturnMatchingArea()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var area = CreateArea("cobham", "Cobham", isFeatured: true, displayOrder: 1);
            dbContext.ServiceAreas.Add(area);
            await dbContext.SaveChangesAsync();

            var service = new ServiceAreasService(repository);
            var result = await service.GetBySlugAsync<ServiceArea>("cobham");

            Assert.NotNull(result);
            Assert.Equal("Cobham", result.Name);
        }

        [Fact]
        public async Task GetNearestAsyncShouldOrderByClosestDriveTimeExcludingCurrentArea()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var current = CreateArea("esher", "Esher", isFeatured: false, displayOrder: 1, driveTimeMinutes: 15);
            var closest = CreateArea("leatherhead", "Leatherhead", isFeatured: false, displayOrder: 2, driveTimeMinutes: 15);
            var further = CreateArea("guildford", "Guildford", isFeatured: false, displayOrder: 3, driveTimeMinutes: 23);
            var furthest = CreateArea("dorking", "Dorking", isFeatured: false, displayOrder: 4, driveTimeMinutes: 25);
            dbContext.ServiceAreas.AddRange(current, closest, further, furthest);
            await dbContext.SaveChangesAsync();

            var service = new ServiceAreasService(repository);
            var results = (await service.GetNearestAsync<ServiceArea>(current.Id, take: 2)).ToList();

            Assert.Equal(2, results.Count);
            Assert.DoesNotContain(results, x => x.Id == current.Id);
            Assert.Equal(closest.Id, results.First().Id);
        }

        [Fact]
        public async Task GetNearestAsyncShouldReturnEmptyWhenAreaDoesNotExist()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var service = new ServiceAreasService(repository);
            var results = await service.GetNearestAsync<ServiceArea>(Guid.NewGuid());

            Assert.Empty(results);
        }

        [Fact]
        public async Task GetBySlugAsyncShouldMapAllFaqsForTheArea()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var area = CreateArea("guildford", "Guildford", isFeatured: true, displayOrder: 1);
            area.Faqs.Add(new ServiceAreaFaq { Question = "Second question", Answer = "Second answer", DisplayOrder = 2 });
            area.Faqs.Add(new ServiceAreaFaq { Question = "First question", Answer = "First answer", DisplayOrder = 1 });
            dbContext.ServiceAreas.Add(area);
            await dbContext.SaveChangesAsync();

            var service = new ServiceAreasService(repository);
            var result = await service.GetBySlugAsync<ServiceAreaDetailsViewModel>("guildford");

            Assert.NotNull(result);
            var faqs = result.Faqs.ToList();
            Assert.Equal(2, faqs.Count);
            Assert.Contains(faqs, f => f.Question == "First question" && f.DisplayOrder == 1);
            Assert.Contains(faqs, f => f.Question == "Second question" && f.DisplayOrder == 2);
        }

        private static ServiceArea CreateArea(
            string slug, string name, bool isFeatured, int displayOrder, int driveTimeMinutes = 15)
        {
            return new ServiceArea
            {
                Slug = slug,
                Name = name,
                Region = "Surrey Borders",
                DriveTimeMinutes = driveTimeMinutes,
                IntroCopy = $"{name} is a well-established area we cover with a full range of handyman and plumbing services.",
                LocalNeighbourhoodsCopy = $"We know the streets and neighbourhoods around {name} well.",
                IsFeatured = isFeatured,
                DisplayOrder = displayOrder,
            };
        }
    }
}
