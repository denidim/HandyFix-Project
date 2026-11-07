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

            // The featured one comes first even though it is further away.
            var notFeatured = CreateArea("dorking", "Dorking", isFeatured: false, driveTimeMinutes: 10);
            var featured = CreateArea("guildford", "Guildford", isFeatured: true, driveTimeMinutes: 20);
            dbContext.ServiceAreas.AddRange(notFeatured, featured);
            await dbContext.SaveChangesAsync();

            var service = ServiceWith(dbContext, KnowsEveryPlace());
            var results = (await service.GetAllAsync<ServiceArea>()).ToList();

            Assert.Equal(featured.Id, results.First().Id);
            Assert.Equal(notFeatured.Id, results.Last().Id);
        }

        [Fact]
        public async Task GetAllAsyncShouldOrderNearestFirstThenByNameWhenFeaturedFirstIsFalse()
        {
            using var dbContext = NewContext();

            // Added out of order, so an order left to the database fails here.
            dbContext.ServiceAreas.AddRange(
                CreateArea("dorking", "Dorking", isFeatured: false, driveTimeMinutes: 25),
                CreateArea("guildford", "Guildford", isFeatured: true, driveTimeMinutes: 10),
                CreateArea("esher", "Esher", isFeatured: false, driveTimeMinutes: 10));
            await dbContext.SaveChangesAsync();

            var service = ServiceWith(dbContext, KnowsEveryPlace());
            var results = await service.GetAllAsync<ServiceArea>(featuredFirst: false);

            Assert.Equal(new[] { "Esher", "Guildford", "Dorking" }, results.Select(x => x.Name));
        }

        [Fact]
        public async Task GetBySlugAsyncShouldReturnMatchingArea()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var area = CreateArea("cobham", "Cobham", isFeatured: true);
            dbContext.ServiceAreas.Add(area);
            await dbContext.SaveChangesAsync();

            var service = ServiceWith(dbContext, KnowsEveryPlace());
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

            var current = CreateArea("esher", "Esher", isFeatured: false, driveTimeMinutes: 15);
            var closest = CreateArea("leatherhead", "Leatherhead", isFeatured: false, driveTimeMinutes: 15);
            var further = CreateArea("guildford", "Guildford", isFeatured: false, driveTimeMinutes: 23);
            var furthest = CreateArea("dorking", "Dorking", isFeatured: false, driveTimeMinutes: 25);
            dbContext.ServiceAreas.AddRange(current, closest, further, furthest);
            await dbContext.SaveChangesAsync();

            var service = ServiceWith(dbContext, KnowsEveryPlace());
            var results = (await service.GetNearestAsync<ServiceArea>(current.Id, take: 2)).ToList();

            Assert.Equal(2, results.Count);
            Assert.DoesNotContain(results, x => x.Id == current.Id);
            Assert.Equal(closest.Id, results.First().Id);
        }

        // Two areas the same drive away came back in whatever order the database gave, so an area
        // page could show a different neighbour from one visit to the next. A tie goes to the
        // name. Each pair is added the other way round, so an order left to the database fails
        // here.
        [Fact]
        public async Task GetNearestAsyncShouldBreakATieInDriveTimeTheSameWayEveryTime()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var current = CreateArea("cobham", "Cobham", isFeatured: false, driveTimeMinutes: 15);

            // Five minutes either side: by name.
            var weybridge = CreateArea("weybridge", "Weybridge", isFeatured: false, driveTimeMinutes: 20);
            var esher = CreateArea("esher", "Esher", isFeatured: false, driveTimeMinutes: 10);

            // Ten minutes either side: by name again.
            var addlestone = CreateArea("addlestone", "Addlestone", isFeatured: false, driveTimeMinutes: 25);
            var walton = CreateArea("walton-on-thames", "Walton-on-Thames", isFeatured: false, driveTimeMinutes: 5);

            dbContext.ServiceAreas.AddRange(current, weybridge, esher, addlestone, walton);
            await dbContext.SaveChangesAsync();

            var service = ServiceWith(dbContext, KnowsEveryPlace());
            var results = await service.GetNearestAsync<ServiceArea>(current.Id, take: 4);

            Assert.Equal(new[] { "Esher", "Weybridge", "Addlestone", "Walton-on-Thames" }, results.Select(x => x.Name));
        }

        [Fact]
        public async Task GetNearestAsyncShouldReturnEmptyWhenAreaDoesNotExist()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var service = ServiceWith(dbContext, KnowsEveryPlace());
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

            var area = CreateArea("guildford", "Guildford", isFeatured: true);
            area.Faqs.Add(new ServiceAreaFaq { Question = "Second question", Answer = "Second answer", DisplayOrder = 2 });
            area.Faqs.Add(new ServiceAreaFaq { Question = "First question", Answer = "First answer", DisplayOrder = 1 });
            dbContext.ServiceAreas.Add(area);
            await dbContext.SaveChangesAsync();

            var service = ServiceWith(dbContext, KnowsEveryPlace());
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

            var service = ServiceWith(dbContext, KnowsEveryPlace());
            var id = (await service.CreateAsync(BuildInput("Richmond", faqCount: 2))).AreaId.Value;

            var created = dbContext.ServiceAreas.Single(x => x.Id == id);
            Assert.Equal("richmond", created.Slug);
            Assert.Equal("Richmond", created.Name);

            var faqs = dbContext.ServiceAreaFaqs.Where(x => x.ServiceAreaId == id).OrderBy(x => x.DisplayOrder).ToList();
            Assert.Equal(2, faqs.Count);
            Assert.Equal(1, faqs[0].DisplayOrder);
            Assert.Equal(2, faqs[1].DisplayOrder);
        }

        // The page address is made from the name; the admin no longer types it (PROJECT_STATE.md
        // Section 3dn).
        [Theory]
        [InlineData("Richmond upon Thames", "richmond-upon-thames")]
        [InlineData("  Walton-on-Thames & Weybridge ", "walton-on-thames-and-weybridge")]
        [InlineData("St. Margarets", "st-margarets")]
        public async Task CreateAsyncShouldMakeTheSlugFromTheName(string name, string expected)
        {
            using var dbContext = NewContext();
            var service = ServiceWith(dbContext, KnowsEveryPlace());

            var id = (await service.CreateAsync(BuildInput(name, faqCount: 0))).AreaId.Value;

            ServiceArea created = dbContext.ServiceAreas.Single(x => x.Id == id);
            Assert.Equal(expected, created.Slug);
            Assert.Equal(name.Trim(), created.Name);
        }

        // A taken name is refused under the Name box, even when the area that has it was soft
        // deleted: IX_ServiceAreas_Slug has no IsDeleted filter, so the save would otherwise fail
        // with a raw 500.
        [Fact]
        public async Task CreateAsyncShouldRefuseANameTakenEvenByADeletedArea()
        {
            using var dbContext = NewContext();
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);
            var area = CreateArea("guildford", "Guildford", isFeatured: false);
            dbContext.ServiceAreas.Add(area);
            await dbContext.SaveChangesAsync();
            repository.Delete(area);
            await repository.SaveChangesAsync();

            var service = ServiceWith(dbContext, KnowsEveryPlace());
            ServiceAreaSaveResult result = await service.CreateAsync(BuildInput("Guildford", faqCount: 0));

            Assert.False(result.Succeeded);
            Assert.Equal(nameof(ServiceAreaAdminInputModel.Name), result.ErrorKey);
            Assert.Equal("There is already an area called Guildford.", result.ErrorMessage);
            Assert.Single(dbContext.ServiceAreas.IgnoreQueryFilters());
        }

        // An empty districts box is filled from the town's name.
        [Fact]
        public async Task CreateAsyncShouldFillTheDistrictsFromTheTownsName()
        {
            using var dbContext = NewContext();
            var locator = new Mock<IPostcodeLocator>();
            locator.Setup(x => x.FindTownDistrictAsync("Molesey", It.IsAny<GeoPoint>())).ReturnsAsync("kt8");
            locator.Setup(x => x.LocateDistrictAsync("KT8")).ReturnsAsync(new GeoPoint(51.4002, -0.3550));
            var service = ServiceWith(dbContext, locator.Object);

            var id = (await service.CreateAsync(BuildInput("Molesey", faqCount: 0))).AreaId.Value;

            ServiceArea created = dbContext.ServiceAreas.Single(x => x.Id == id);
            Assert.Equal("molesey", created.Slug);
            Assert.Equal("KT8", created.PostcodeDistricts);

            // North-west of the base, placed from the district it found.
            Assert.Equal(318, created.DiagramBearing);
        }

        // A town the lookup cannot find: nothing is saved, and one sentence goes under the name.
        [Fact]
        public async Task CreateAsyncShouldSaveNothingWhenTheTownIsNotFound()
        {
            using var dbContext = NewContext();
            var service = ServiceWith(dbContext, Mock.Of<IPostcodeLocator>());

            ServiceAreaSaveResult result = await service.CreateAsync(BuildInput("Molsey", faqCount: 1));

            Assert.False(result.Succeeded);
            Assert.Equal(nameof(ServiceAreaAdminInputModel.Name), result.ErrorKey);
            Assert.Equal("We could not find a town called Molsey. Check the spelling.", result.ErrorMessage);
            Assert.Empty(dbContext.ServiceAreas);
            Assert.Empty(dbContext.ServiceAreaFaqs);
        }

        // The drive time from the base comes from the route service; when it does not answer, from
        // the distance (Guildford's GU1 is about 23 km away: 5 + 1.1 x 23 = 30). Never 0, which the
        // pages show as the base itself.
        [Theory]
        [InlineData(31, 31)]
        [InlineData(null, 30)]
        [InlineData(0, 1)]
        public async Task CreateAsyncShouldSetTheDriveTimeFromTheRouteServiceElseFromTheDistance(int? routeMinutes, int expected)
        {
            using var dbContext = NewContext();
            var locator = new Mock<IPostcodeLocator>();
            locator.Setup(x => x.LocateDistrictAsync("GU1")).ReturnsAsync(new GeoPoint(51.2431, -0.5648));
            var driveTimes = new Mock<IDriveTimeCalculator>();
            driveTimes.Setup(x => x.DriveMinutesAsync(It.IsAny<GeoPoint>(), It.IsAny<GeoPoint>())).ReturnsAsync(routeMinutes);
            var service = ServiceWith(dbContext, locator.Object, driveTimes.Object);

            var input = BuildInput("Guildford", faqCount: 0);
            input.PostcodeDistricts = "GU1";
            var id = (await service.CreateAsync(input)).AreaId.Value;

            Assert.Equal(expected, dbContext.ServiceAreas.Single(x => x.Id == id).DriveTimeMinutes);
        }

        [Fact]
        public async Task UpdateAsyncShouldReplaceFaqsRatherThanAppendThem()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options;

            using var dbContext = new ApplicationDbContext(options);
            using var repository = new EfDeletableEntityRepository<ServiceArea>(dbContext);

            var service = ServiceWith(dbContext, KnowsEveryPlace());
            var id = (await service.CreateAsync(BuildInput("Richmond", faqCount: 3))).AreaId.Value;

            var update = BuildInput("Richmond", faqCount: 1);
            update.Name = "Richmond upon Thames";
            await service.UpdateAsync(id, update);

            // The name changes; the page address made from the first one does not.
            Assert.Equal("Richmond upon Thames", dbContext.ServiceAreas.Single(x => x.Id == id).Name);
            Assert.Equal("richmond", dbContext.ServiceAreas.Single(x => x.Id == id).Slug);

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

            var service = ServiceWith(dbContext, KnowsEveryPlace());
            var id = (await service.CreateAsync(BuildInput("Richmond", faqCount: 2))).AreaId.Value;

            await service.DeleteAsync(id);

            // IgnoreQueryFilters, because a soft delete would still satisfy a filtered check while
            // leaving the row - and its slug - physically present under the unique index.
            Assert.Empty(dbContext.ServiceAreas.IgnoreQueryFilters().Where(x => x.Id == id));
            Assert.Empty(dbContext.ServiceAreaFaqs.IgnoreQueryFilters().Where(x => x.ServiceAreaId == id));
            Assert.True((await service.CreateAsync(BuildInput("Richmond", faqCount: 0))).Succeeded);
        }

        // The booking form takes a booking only for a postcode in a district some area lists
        // (PROJECT_STATE.md Section 3cb).
        [Fact]
        public async Task CreateAndUpdateAsyncShouldSaveThePostcodeDistrictsTidied()
        {
            using var dbContext = NewContext();
            var service = ServiceWith(dbContext, KnowsEveryPlace());

            var input = BuildInput("Surbiton", faqCount: 1);
            input.PostcodeDistricts = "kt5,KT6 ,  kt7";
            var id = (await service.CreateAsync(input)).AreaId.Value;

            Assert.Equal("KT5, KT6, KT7", dbContext.ServiceAreas.Single(x => x.Id == id).PostcodeDistricts);

            // An emptied box is filled from the town's name again, never saved empty.
            input.PostcodeDistricts = "  ";
            await service.UpdateAsync(id, input);

            Assert.Equal("TW9", dbContext.ServiceAreas.Single(x => x.Id == id).PostcodeDistricts);
        }

        // Emptied districts on the Edit page, and no town of that name: nothing is changed.
        [Fact]
        public async Task UpdateAsyncShouldSaveNothingWhenTheDistrictsAreEmptiedAndTheTownIsNotFound()
        {
            using var dbContext = NewContext();
            var area = CreateArea("surbiton", "Surbiton", isFeatured: false);
            area.PostcodeDistricts = "KT5";
            dbContext.ServiceAreas.Add(area);
            await dbContext.SaveChangesAsync();
            var service = ServiceWith(dbContext, Mock.Of<IPostcodeLocator>());

            var input = BuildInput("Surbitn", faqCount: 0);
            ServiceAreaSaveResult result = await service.UpdateAsync(area.Id, input);

            Assert.False(result.Succeeded);
            Assert.Equal("We could not find a town called Surbitn. Check the spelling.", result.ErrorMessage);
            ServiceArea saved = dbContext.ServiceAreas.Single();
            Assert.Equal("Surbiton", saved.Name);
            Assert.Equal("KT5", saved.PostcodeDistricts);
        }

        // A new area is placed on the coverage diagram from where its first district is
        // (PROJECT_STATE.md Section 3dm). Guildford's GU1 lies south-west of the base: 234
        // degrees, which is also where the hand-set diagram had it.
        [Fact]
        public async Task CreateAsyncShouldPlaceTheAreaOnTheDiagramFromItsFirstDistrict()
        {
            using var dbContext = NewContext();
            var locator = new Mock<IPostcodeLocator>();
            locator.Setup(x => x.LocateDistrictAsync("GU1")).ReturnsAsync(new GeoPoint(51.2431, -0.5648));
            var service = ServiceWith(dbContext, locator.Object);

            var input = BuildInput("Guildford", faqCount: 1);
            input.PostcodeDistricts = "gu1, GU2";
            var id = (await service.CreateAsync(input)).AreaId.Value;

            Assert.Equal(234, dbContext.ServiceAreas.Single(x => x.Id == id).DiagramBearing);
            Assert.True(await service.IsOnDiagramAsync(id));
            locator.Verify(x => x.LocateDistrictAsync(It.IsAny<string>()), Times.Once());
        }

        // A new area needs its place, for the diagram and the drive time: a typed first district
        // that cannot be found saves nothing, and says so under the districts box.
        [Fact]
        public async Task CreateAsyncShouldSaveNothingWhenItsFirstDistrictIsNotFound()
        {
            using var dbContext = NewContext();
            var service = ServiceWith(dbContext, Mock.Of<IPostcodeLocator>());

            var input = BuildInput("Richmond", faqCount: 1);
            input.PostcodeDistricts = "ZZ99";
            ServiceAreaSaveResult result = await service.CreateAsync(input);

            Assert.False(result.Succeeded);
            Assert.Equal(nameof(ServiceAreaAdminInputModel.PostcodeDistricts), result.ErrorKey);
            Assert.Equal("We could not find where ZZ99 is. Check the postcode districts.", result.ErrorMessage);
            Assert.Empty(dbContext.ServiceAreas);
        }

        // A place set by hand (the 14 that came with the site) stays when other fields are saved;
        // changing the districts places the area again.
        [Fact]
        public async Task UpdateAsyncShouldPlaceTheAreaAgainOnlyWhenItsDistrictsChange()
        {
            using var dbContext = NewContext();
            var locator = new Mock<IPostcodeLocator>();
            locator.Setup(x => x.LocateDistrictAsync("RH4")).ReturnsAsync(new GeoPoint(51.2298, -0.3349));
            var service = ServiceWith(dbContext, locator.Object);

            var area = CreateArea("dorking", "Dorking", isFeatured: false);
            area.PostcodeDistricts = "RH5";
            area.DiagramBearing = 189;
            dbContext.ServiceAreas.Add(area);
            await dbContext.SaveChangesAsync();

            var input = BuildInput("Dorking", faqCount: 1);
            input.PostcodeDistricts = "RH5";
            input.DriveTimeMinutes = 26;
            await service.UpdateAsync(area.Id, input);

            // The drive time is never changed by the form.
            Assert.Equal(15, dbContext.ServiceAreas.Single(x => x.Id == area.Id).DriveTimeMinutes);
            Assert.Equal(189, dbContext.ServiceAreas.Single(x => x.Id == area.Id).DiagramBearing);
            locator.Verify(x => x.LocateDistrictAsync(It.IsAny<string>()), Times.Never());

            input.PostcodeDistricts = "RH4, RH5";
            await service.UpdateAsync(area.Id, input);

            Assert.Equal(188, dbContext.ServiceAreas.Single(x => x.Id == area.Id).DiagramBearing);

            // The side picked on the Edit page is kept; "Automatic" is none.
            input.DiagramLabelSide = DiagramLabelSide.Below;
            await service.UpdateAsync(area.Id, input);
            Assert.Equal(DiagramLabelSide.Below, dbContext.ServiceAreas.Single(x => x.Id == area.Id).DiagramLabelSide);

            input.DiagramLabelSide = null;
            await service.UpdateAsync(area.Id, input);
            Assert.Null(dbContext.ServiceAreas.Single(x => x.Id == area.Id).DiagramLabelSide);
        }

        [Fact]
        public async Task GetServedPostcodeDistrictsAsyncShouldGiveEveryAreasDistrictsOnceInOrder()
        {
            using ApplicationDbContext dbContext = ContextWithAreasCovering("KT5, KT6", "KT1, KT6", null, string.Empty);
            var service = new ServiceAreasService(
                new EfDeletableEntityRepository<ServiceArea>(dbContext),
                new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext),
                new Mock<IImageStorageService>().Object,
                Mock.Of<IPostcodeLocator>(),
                Mock.Of<IDriveTimeCalculator>());

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
                new Mock<IImageStorageService>().Object,
                Mock.Of<IPostcodeLocator>(),
                Mock.Of<IDriveTimeCalculator>());

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
                new Mock<IImageStorageService>().Object,
                Mock.Of<IPostcodeLocator>(),
                Mock.Of<IDriveTimeCalculator>());

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
                new Mock<IImageStorageService>().Object,
                Mock.Of<IPostcodeLocator>(),
                Mock.Of<IDriveTimeCalculator>());

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
                storage.Object,
                Mock.Of<IPostcodeLocator>(),
                Mock.Of<IDriveTimeCalculator>());

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
                storage.Object,
                Mock.Of<IPostcodeLocator>(),
                Mock.Of<IDriveTimeCalculator>());

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
                storage.Object,
                Mock.Of<IPostcodeLocator>(),
                Mock.Of<IDriveTimeCalculator>());

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

        // Finds every town and district, at Richmond (TW9), north of the base.
        private static IPostcodeLocator KnowsEveryPlace()
        {
            var locator = new Mock<IPostcodeLocator>();
            locator.Setup(x => x.FindTownDistrictAsync(It.IsAny<string>(), It.IsAny<GeoPoint>())).ReturnsAsync("TW9");
            locator.Setup(x => x.LocateDistrictAsync(It.IsAny<string>())).ReturnsAsync(new GeoPoint(51.4613, -0.3037));
            return locator.Object;
        }

        private static ApplicationDbContext NewContext()
        {
            return new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options);
        }

        private static ServiceAreasService ServiceWith(ApplicationDbContext dbContext, IPostcodeLocator locator, IDriveTimeCalculator driveTimes = null)
        {
            return new ServiceAreasService(
                new EfDeletableEntityRepository<ServiceArea>(dbContext),
                new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext),
                new Mock<IImageStorageService>().Object,
                locator,
                driveTimes ?? Mock.Of<IDriveTimeCalculator>());
        }

        private static ApplicationDbContext ContextWithAreasCovering(params string[] districtLists)
        {
            var dbContext = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()).Options);

            for (var i = 0; i < districtLists.Length; i++)
            {
                ServiceArea area = CreateArea($"area-{i}", $"Area {i}", isFeatured: false);
                area.PostcodeDistricts = districtLists[i];
                dbContext.ServiceAreas.Add(area);
            }

            dbContext.SaveChanges();
            return dbContext;
        }

        private static ServiceAreaAdminInputModel BuildInput(string name, int faqCount)
        {
            var model = new ServiceAreaAdminInputModel
            {
                Name = name,
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
            string slug, string name, bool isFeatured, int driveTimeMinutes = 15)
        {
            return new ServiceArea
            {
                Slug = slug,
                Name = name,
                DriveTimeMinutes = driveTimeMinutes,
                IntroCopy = $"{name} is a well-established area we cover with a full range of handyman and plumbing services.",
                LocalNeighbourhoodsCopy = $"We know the streets and neighbourhoods around {name} well.",
                IsFeatured = isFeatured,
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
                var service = new ServiceAreasService(null, null, null, null, null);

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
                var service = new ServiceAreasService(null, null, null, null, null);

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
                var service = new ServiceAreasService(null, null, null, null, null);

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
