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
    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Web.ViewModels.ServiceAreas;

    using Microsoft.AspNetCore.Http;

    using Microsoft.EntityFrameworkCore;

    using Moq;

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

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext), new Mock<IImageStorageService>().Object);
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

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext), new Mock<IImageStorageService>().Object);
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

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext), new Mock<IImageStorageService>().Object);
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

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext), new Mock<IImageStorageService>().Object);
            var results = (await service.GetNearestAsync<ServiceArea>(current.Id, take: 2)).ToList();

            Assert.Equal(2, results.Count);
            Assert.DoesNotContain(results, x => x.Id == current.Id);
            Assert.Equal(closest.Id, results.First().Id);
        }

        // Two areas the same drive away came back in whatever order the database gave, so an area
        // page could show a different neighbour from one visit to the next. A tie goes to the
        // order the area lists use, then to the name. Each pair is added the other way round, so
        // an order left to the database fails here.
        [Fact]
        public async Task GetNearestAsyncShouldBreakATieInDriveTimeTheSameWayEveryTime()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var current = CreateArea("cobham", "Cobham", isFeatured: false, displayOrder: 1, driveTimeMinutes: 15);

            // Five minutes either side, with the same display order: by name.
            var weybridge = CreateArea("weybridge", "Weybridge", isFeatured: false, displayOrder: 7, driveTimeMinutes: 20);
            var esher = CreateArea("esher", "Esher", isFeatured: false, displayOrder: 7, driveTimeMinutes: 10);

            // Ten minutes either side: by display order, ahead of the name.
            var addlestone = CreateArea("addlestone", "Addlestone", isFeatured: false, displayOrder: 9, driveTimeMinutes: 25);
            var walton = CreateArea("walton-on-thames", "Walton-on-Thames", isFeatured: false, displayOrder: 8, driveTimeMinutes: 5);

            dbContext.ServiceAreas.AddRange(current, weybridge, esher, addlestone, walton);
            await dbContext.SaveChangesAsync();

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext), new Mock<IImageStorageService>().Object);
            var results = await service.GetNearestAsync<ServiceArea>(current.Id, take: 4);

            Assert.Equal(new[] { "Esher", "Weybridge", "Walton-on-Thames", "Addlestone" }, results.Select(x => x.Name));
        }

        [Fact]
        public async Task GetNearestAsyncShouldReturnEmptyWhenAreaDoesNotExist()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext), new Mock<IImageStorageService>().Object);
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

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext), new Mock<IImageStorageService>().Object);
            var result = await service.GetBySlugAsync<ServiceAreaDetailsViewModel>("guildford");

            Assert.NotNull(result);
            var faqs = result.Faqs.ToList();
            Assert.Equal(2, faqs.Count);
            Assert.Contains(faqs, f => f.Question == "First question" && f.DisplayOrder == 1);
            Assert.Contains(faqs, f => f.Question == "Second question" && f.DisplayOrder == 2);
        }

        [Fact]
        public async Task CreateAsyncShouldPersistTheAreaAndItsFaqsInOrder()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext), new Mock<IImageStorageService>().Object);
            var id = await service.CreateAsync(BuildInput("richmond", "Richmond", faqCount: 2));

            var created = dbContext.ServiceAreas.Single(x => x.Id == id);
            Assert.Equal("richmond", created.Slug);
            Assert.Equal("Richmond", created.Name);

            var faqs = dbContext.ServiceAreaFaqs.Where(x => x.ServiceAreaId == id).OrderBy(x => x.DisplayOrder).ToList();
            Assert.Equal(2, faqs.Count);
            Assert.Equal(1, faqs[0].DisplayOrder);
            Assert.Equal(2, faqs[1].DisplayOrder);
        }

        [Fact]
        public async Task CreateAsyncShouldNormalizeTheSlugToLowercase()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext), new Mock<IImageStorageService>().Object);
            var input = BuildInput("  Richmond-Upon-Thames  ", "Richmond upon Thames", faqCount: 0);
            var id = await service.CreateAsync(input);

            Assert.Equal("richmond-upon-thames", dbContext.ServiceAreas.Single(x => x.Id == id).Slug);
        }

        [Fact]
        public async Task UpdateAsyncShouldReplaceFaqsRatherThanAppendThem()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext), new Mock<IImageStorageService>().Object);
            var id = await service.CreateAsync(BuildInput("richmond", "Richmond", faqCount: 3));

            var update = BuildInput("richmond", "Richmond", faqCount: 1);
            update.Name = "Richmond upon Thames";
            await service.UpdateAsync(id, update);

            Assert.Equal("Richmond upon Thames", dbContext.ServiceAreas.Single(x => x.Id == id).Name);

            // Hard-replaced, so no soft-deleted orphans should be left behind either.
            Assert.Single(dbContext.ServiceAreaFaqs.Where(x => x.ServiceAreaId == id));
            Assert.Single(dbContext.ServiceAreaFaqs.IgnoreQueryFilters().Where(x => x.ServiceAreaId == id));
        }

        [Fact]
        public async Task DeleteAsyncShouldHardDeleteTheAreaAndItsFaqsSoTheSlugCanBeReused()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext), new Mock<IImageStorageService>().Object);
            var id = await service.CreateAsync(BuildInput("richmond", "Richmond", faqCount: 2));

            await service.DeleteAsync(id);

            // IgnoreQueryFilters, because a soft delete would still satisfy a filtered check while
            // leaving the row - and its slug - physically present under the unique index.
            Assert.Empty(dbContext.ServiceAreas.IgnoreQueryFilters().Where(x => x.Id == id));
            Assert.Empty(dbContext.ServiceAreaFaqs.IgnoreQueryFilters().Where(x => x.ServiceAreaId == id));
            Assert.False(await service.SlugExistsAsync("richmond"));
        }

        [Fact]
        public async Task SlugExistsAsyncShouldStillReportSoftDeletedSlugsAsTaken()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var area = CreateArea("guildford", "Guildford", isFeatured: false, displayOrder: 1);
            dbContext.ServiceAreas.Add(area);
            await dbContext.SaveChangesAsync();

            repository.Delete(area);
            await repository.SaveChangesAsync();

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext), new Mock<IImageStorageService>().Object);

            // The row is invisible to every normal query now, but IX_ServiceAreas_Slug has no
            // IsDeleted filter, so the slug is still reserved at the database level. Reporting it
            // as free would let the admin form accept a duplicate and fail with a raw 500 on save.
            Assert.True(await service.SlugExistsAsync("guildford"));
        }

        [Fact]
        public async Task SlugExistsAsyncShouldIgnoreTheAreaBeingEdited()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var area = CreateArea("guildford", "Guildford", isFeatured: false, displayOrder: 1);
            dbContext.ServiceAreas.Add(area);
            await dbContext.SaveChangesAsync();

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext), new Mock<IImageStorageService>().Object);

            Assert.True(await service.SlugExistsAsync("guildford"));
            Assert.False(await service.SlugExistsAsync("guildford", excludeAreaId: area.Id));
        }

        // The booking form takes a booking only for a postcode in a district some area lists
        // (PROJECT_STATE.md Section 3cb).
        [Fact]
        public async Task CreateAndUpdateAsyncShouldSaveThePostcodeDistrictsTidied()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            var service = new ServiceAreasService(
                new EfDeletableEntityRepository<ServiceArea>(dbContext),
                new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext),
                new Mock<IImageStorageService>().Object);

            var input = BuildInput("surbiton", "Surbiton", faqCount: 1);
            input.PostcodeDistricts = "kt5,KT6 ,  kt7";
            var id = await service.CreateAsync(input);

            Assert.Equal("KT5, KT6, KT7", dbContext.ServiceAreas.Single(x => x.Id == id).PostcodeDistricts);

            // An emptied box is no list, not an empty text.
            input.PostcodeDistricts = "  ";
            await service.UpdateAsync(id, input);

            Assert.Null(dbContext.ServiceAreas.Single(x => x.Id == id).PostcodeDistricts);
        }

        [Fact]
        public async Task GetServedPostcodeDistrictsAsyncShouldGiveEveryAreasDistrictsOnceInOrder()
        {
            using ApplicationDbContext dbContext = ContextWithAreasCovering("KT5, KT6", "KT1, KT6", null, string.Empty);
            var service = new ServiceAreasService(
                new EfDeletableEntityRepository<ServiceArea>(dbContext),
                new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext),
                new Mock<IImageStorageService>().Object);

            Assert.Equal(new[] { "KT1", "KT5", "KT6" }, await service.GetServedPostcodeDistrictsAsync());
        }

        [Theory]
        [InlineData("KT5 8AB", true)]
        [InlineData("kt11aa", true)]
        [InlineData("KT9 2QN", false)]
        [InlineData("M1 1AE", false)]
        public async Task IsPostcodeServedAsyncShouldAskWhetherSomeAreaListsTheDistrict(string postcode, bool expected)
        {
            using ApplicationDbContext dbContext = ContextWithAreasCovering("KT5, KT6", "KT1");
            var service = new ServiceAreasService(
                new EfDeletableEntityRepository<ServiceArea>(dbContext),
                new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext),
                new Mock<IImageStorageService>().Object);

            Assert.Equal(expected, await service.IsPostcodeServedAsync(postcode));
        }

        // With no district on any area the check has nothing to go on. It is off, so an empty
        // setup cannot turn every booking away.
        [Fact]
        public async Task IsPostcodeServedAsyncShouldAcceptEveryPostcodeWhenNoAreaListsADistrict()
        {
            using ApplicationDbContext dbContext = ContextWithAreasCovering(null, string.Empty);
            var service = new ServiceAreasService(
                new EfDeletableEntityRepository<ServiceArea>(dbContext),
                new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext),
                new Mock<IImageStorageService>().Object);

            Assert.True(await service.IsPostcodeServedAsync("M1 1AE"));
        }

        // A deleted area's districts stop counting the moment it is gone.
        [Fact]
        public async Task IsPostcodeServedAsyncShouldNotCountADeletedAreasDistricts()
        {
            using ApplicationDbContext dbContext = ContextWithAreasCovering("KT5", "KT9");
            var service = new ServiceAreasService(
                new EfDeletableEntityRepository<ServiceArea>(dbContext),
                new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext),
                new Mock<IImageStorageService>().Object);

            Guid chessington = dbContext.ServiceAreas.Single(x => x.PostcodeDistricts == "KT9").Id;
            await service.DeleteAsync(chessington);

            Assert.False(await service.IsPostcodeServedAsync("KT9 2QN"));
            Assert.True(await service.IsPostcodeServedAsync("KT5 8AB"));
        }

        // An area's picture is changed in this order: the new one goes up, the area points at
        // it, and only then does the old upload leave the bucket (PROJECT_STATE.md Section 3dl).
        [Fact]
        public async Task SetHeroImageAsyncShouldPointTheAreaAtTheNewPictureThenDeleteTheOldOne()
        {
            using ApplicationDbContext dbContext = ContextWithAreasCovering("KT5");
            ServiceArea area = dbContext.ServiceAreas.Single();
            area.HeroImageUrl = "https://photos.example/areas/old.webp";
            dbContext.SaveChanges();

            var storage = new Mock<IImageStorageService>();
            string urlWhenOldDeleted = null;
            storage
                .Setup(x => x.SaveAreaImageAsync(It.IsAny<Stream>(), "photo.jpg", "image/jpeg", area.Slug))
                .ReturnsAsync("https://photos.example/areas/new.webp");
            storage
                .Setup(x => x.DeleteAreaImageAsync("https://photos.example/areas/old.webp"))
                .Callback(() => urlWhenOldDeleted = dbContext.ServiceAreas.Single().HeroImageUrl)
                .Returns(Task.CompletedTask);

            var service = new ServiceAreasService(
                new EfDeletableEntityRepository<ServiceArea>(dbContext),
                new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext),
                storage.Object);

            await service.SetHeroImageAsync(area.Id, Upload());

            Assert.Equal("https://photos.example/areas/new.webp", dbContext.ServiceAreas.Single().HeroImageUrl);
            Assert.Equal("https://photos.example/areas/new.webp", urlWhenOldDeleted);
        }

        // A picture that cannot be made leaves the area with the one it had.
        [Fact]
        public async Task SetHeroImageAsyncShouldKeepTheOldPictureWhenTheNewOneFails()
        {
            using ApplicationDbContext dbContext = ContextWithAreasCovering("KT5");
            ServiceArea area = dbContext.ServiceAreas.Single();
            area.HeroImageUrl = "https://photos.example/areas/old.webp";
            dbContext.SaveChanges();

            var storage = new Mock<IImageStorageService>();
            storage
                .Setup(x => x.SaveAreaImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new ImageUploadValidationException("Failed to decode uploaded image."));

            var service = new ServiceAreasService(
                new EfDeletableEntityRepository<ServiceArea>(dbContext),
                new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext),
                storage.Object);

            await Assert.ThrowsAsync<ImageUploadValidationException>(() => service.SetHeroImageAsync(area.Id, Upload()));

            Assert.Equal("https://photos.example/areas/old.webp", dbContext.ServiceAreas.Single().HeroImageUrl);
            storage.Verify(x => x.DeleteAreaImageAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteAsyncShouldTakeTheAreasUploadedPictureOutOfTheBucketAfterTheArea()
        {
            using ApplicationDbContext dbContext = ContextWithAreasCovering("KT5");
            ServiceArea area = dbContext.ServiceAreas.Single();
            area.HeroImageUrl = "https://photos.example/areas/old.webp";
            dbContext.SaveChanges();

            var storage = new Mock<IImageStorageService>();
            var areaGoneFirst = false;
            storage
                .Setup(x => x.DeleteAreaImageAsync("https://photos.example/areas/old.webp"))
                .Callback(() => areaGoneFirst = !dbContext.ServiceAreas.Any())
                .Returns(Task.CompletedTask);

            var service = new ServiceAreasService(
                new EfDeletableEntityRepository<ServiceArea>(dbContext),
                new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext),
                storage.Object);

            await service.DeleteAsync(area.Id);

            Assert.True(areaGoneFirst);
        }

        // A file as the admin's form sends it.
        private static IFormFile Upload()
        {
            var file = new Mock<IFormFile>();
            file.Setup(x => x.Length).Returns(10);
            file.Setup(x => x.FileName).Returns("photo.jpg");
            file.Setup(x => x.ContentType).Returns("image/jpeg");
            file.Setup(x => x.OpenReadStream()).Returns(new MemoryStream(new byte[10]));
            return file.Object;
        }

        private static ApplicationDbContext ContextWithAreasCovering(params string[] districtLists)
        {
            var dbContext = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options);

            for (var i = 0; i < districtLists.Length; i++)
            {
                ServiceArea area = CreateArea($"area-{i}", $"Area {i}", isFeatured: false, displayOrder: i);
                area.PostcodeDistricts = districtLists[i];
                dbContext.ServiceAreas.Add(area);
            }

            dbContext.SaveChanges();
            return dbContext;
        }

        private static ServiceAreaAdminInputModel BuildInput(string slug, string name, int faqCount)
        {
            var model = new ServiceAreaAdminInputModel
            {
                Slug = slug,
                Name = name,
                Region = "Surrey Borders",
                DriveTimeMinutes = 20,
                DisplayOrder = 5,
                IsFeatured = false,
                IntroCopy = $"{name} is an area we cover with a full range of handyman and plumbing services.",
                LocalNeighbourhoodsCopy = $"We know the streets and neighbourhoods around {name} well.",
            };

            for (var i = 1; i <= faqCount; i++)
            {
                model.Faqs.Add(new ServiceAreaFaqInputModel
                {
                    Question = $"Question number {i}?",
                    Answer = $"Answer number {i}.",
                });
            }

            return model;
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

        /// <summary>
        /// The FAQ builder's contract, per PROJECT_STATE.md section 3m: blank rows are pruned
        /// before validation, so every returned error key lines up with the index the row will
        /// actually render at. ServiceAreaFaqInputModel carries no DataAnnotations precisely so
        /// the binder cannot raise errors against pre-prune indices.
        /// </summary>
        public class PruneAndValidateFaqsTests
        {
            [Fact]
            public void ShouldSilentlyDropFullyBlankFaqRows()
            {
                var service = new ServiceAreasService(null, null, null);

                var model = new ServiceAreaAdminInputModel
                {
                    Faqs = new System.Collections.Generic.List<ServiceAreaFaqInputModel>
                    {
                        Faq("How quickly can you get here?", "Usually within two working days."),
                        new ServiceAreaFaqInputModel(),
                        new ServiceAreaFaqInputModel { Question = "   ", Answer = null },
                    },
                };

                var errors = service.PruneAndValidateFaqs(model).ToList();

                // A blank row is the admin leaving an unused slot alone, not an error.
                Assert.Empty(errors);
                Assert.Single(model.Faqs);
            }

            [Fact]
            public void ShouldKeyErrorsToThePostPruneIndex()
            {
                // The actual trap this guards. Row 0 is blank and gets pruned; row 1 is
                // half-filled and invalid. After pruning it renders at index 0, so an error
                // keyed Faqs[1].Answer would attach to a row the admin cannot see.
                var service = new ServiceAreasService(null, null, null);

                var model = new ServiceAreaAdminInputModel
                {
                    Faqs = new System.Collections.Generic.List<ServiceAreaFaqInputModel>
                    {
                        new ServiceAreaFaqInputModel(),
                        Faq("Do you cover weekends?", null),
                    },
                };

                var errors = service.PruneAndValidateFaqs(model).ToList();

                Assert.Contains(errors, e => e.Key == "Faqs[0].Answer");
                Assert.DoesNotContain(errors, e => e.Key == "Faqs[1].Answer");
            }

            [Fact]
            public void ShouldRejectAHalfFilledFaqRow()
            {
                var service = new ServiceAreasService(null, null, null);

                var model = new ServiceAreaAdminInputModel
                {
                    Faqs = new System.Collections.Generic.List<ServiceAreaFaqInputModel> { Faq(null, "Yes, we do.") },
                };

                var errors = service.PruneAndValidateFaqs(model).ToList();

                Assert.Contains(errors, e => e.Key == "Faqs[0].Question");
            }

            private static ServiceAreaFaqInputModel Faq(string question, string answer) =>
                new ServiceAreaFaqInputModel { Question = question, Answer = answer };
        }
    }
}
