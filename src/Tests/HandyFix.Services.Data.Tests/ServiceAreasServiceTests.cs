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

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext));
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

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext));
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

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext));
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

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext));
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

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext));
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

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext));
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

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext));
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

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext));
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

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext));
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

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext));
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

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext));

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

            var service = new ServiceAreasService(repository, new EfDeletableEntityRepository<ServiceAreaFaq>(dbContext));

            Assert.True(await service.SlugExistsAsync("guildford"));
            Assert.False(await service.SlugExistsAsync("guildford", excludeAreaId: area.Id));
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
                var service = new ServiceAreasService(null, null);

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
                var service = new ServiceAreasService(null, null);

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
                var service = new ServiceAreasService(null, null);

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
