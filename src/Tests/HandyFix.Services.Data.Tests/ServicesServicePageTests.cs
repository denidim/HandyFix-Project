namespace HandyFix.Services.Data.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Data.Repositories;
    using HandyFix.Services;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.ViewModels.Administration.Services;

    using Microsoft.EntityFrameworkCore;

    using Moq;

    using Xunit;

    // A service page's own blocks and questions, saved from the admin form (PROJECT_STATE.md
    // Section 3ep).
    public class ServicesServicePageTests
    {
        [Fact]
        public async Task TheBlocksAreSavedOnePointPerLineAndAnEmptyOneAsNothing()
        {
            using var dbContext = NewContext();
            Service tap = await AddServiceAsync(dbContext);

            await NewService(dbContext).SavePageAsync(tap.Id, new ServiceAdminInputModel
            {
                Summary = "  A dripping tap,\r\n fixed.  ",
                CommonFindings = " [water_drop] A worn washer. It drips. \r\n\r\n  \r\n[refresh] A perished O-ring.\r\n",
                VisitSteps = "We turn the water off.\nWe fit the part.",
                BeforeWeArrive = "   ",
                Included = "New washers",
                NotIncluded = null,
            });

            Service saved = await dbContext.Services.SingleAsync();
            Assert.Equal("A dripping tap, fixed.", saved.Summary);
            Assert.Equal("[water_drop] A worn washer. It drips.\n[refresh] A perished O-ring.", saved.CommonFindings);
            Assert.Equal("We turn the water off.\nWe fit the part.", saved.VisitSteps);
            Assert.Null(saved.BeforeWeArrive);
            Assert.Equal("New washers", saved.Included);
            Assert.Null(saved.NotIncluded);
        }

        // Replaced as a town's questions are: the old rows go for good, the new ones are numbered
        // in the order the form sent them, and a half-empty row is not saved.
        [Fact]
        public async Task TheQuestionsAreReplacedInTheOrderSent()
        {
            using var dbContext = NewContext();
            Service tap = await AddServiceAsync(dbContext);
            dbContext.ServiceFaqs.Add(new ServiceFaq { ServiceId = tap.Id, Question = "An old question?", Answer = "An old answer.", DisplayOrder = 1 });
            await dbContext.SaveChangesAsync();

            await NewService(dbContext).SavePageAsync(tap.Id, new ServiceAdminInputModel
            {
                Faqs = new List<ServiceFaqInputModel>
                {
                    new() { Question = " Is a repair worth it? ", Answer = " Usually. " },
                    new() { Question = "Half a row?", Answer = " " },
                    new() { Question = "Do you carry the part?", Answer = "Most of them." },
                },
            });

            var faqs = await dbContext.ServiceFaqs.IgnoreQueryFilters().OrderBy(f => f.DisplayOrder).ToListAsync();
            Assert.Equal(new[] { "Is a repair worth it?", "Do you carry the part?" }, faqs.Select(f => f.Question));
            Assert.Equal(new[] { 1, 2 }, faqs.Select(f => f.DisplayOrder));
            Assert.Equal("Usually.", faqs[0].Answer);
        }

        [Fact]
        public void BlankQuestionRowsAreDroppedBeforeTheRestAreChecked()
        {
            var model = new ServiceAdminInputModel
            {
                Faqs = new List<ServiceFaqInputModel>
                {
                    new(),
                    new() { Question = "   ", Answer = null },
                    new() { Question = "Why?", Answer = "Because it is." },
                    new() { Question = "Is this one long enough?", Answer = null },
                },
            };

            var errors = NewService(null).PruneAndValidateFaqs(model).ToList();

            // The two blank rows are gone, so the errors name rows 0 and 1 of what is left.
            Assert.Equal(2, model.Faqs.Count);
            Assert.Equal(
                new[] { "Faqs[0].Question", "Faqs[1].Answer" },
                errors.Select(e => e.Key));
        }

        private static ApplicationDbContext NewContext() =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static ServicesService NewService(ApplicationDbContext dbContext) =>
            dbContext == null
                ? new ServicesService(null, null, null, new Mock<IImageStorageService>().Object)
                : new ServicesService(
                    new EfDeletableEntityRepository<Service>(dbContext),
                    null,
                    new EfDeletableEntityRepository<ServiceFaq>(dbContext),
                    new Mock<IImageStorageService>().Object);

        private static async Task<Service> AddServiceAsync(ApplicationDbContext dbContext)
        {
            var category = new ServiceCategory { Id = Guid.NewGuid(), Name = "Plumbing", Description = "Plumbing work", Slug = "plumbing" };
            var tap = new Service { Name = "Tap Repairs", Description = "Fixing dripping taps.", Slug = "tap-repairs", BasePrice = 90, CategoryId = category.Id };
            dbContext.ServiceCategories.Add(category);
            dbContext.Services.Add(tap);
            await dbContext.SaveChangesAsync();
            return tap;
        }
    }
}
