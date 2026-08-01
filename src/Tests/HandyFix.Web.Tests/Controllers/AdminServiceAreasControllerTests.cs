namespace HandyFix.Web.Tests.Controllers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Web.Areas.Administration.Controllers;
    using HandyFix.Web.ViewModels.ServiceAreas;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;

    using Moq;

    using Xunit;

    /// <summary>
    /// The FAQ builder's contract, per PROJECT_STATE.md section 3m: blank rows are pruned before
    /// validation, so every ModelState error key lines up with the index the row actually renders
    /// at. ServiceAreaFaqInputModel carries no DataAnnotations precisely so the binder cannot
    /// raise errors against pre-prune indices.
    /// </summary>
    public class AdminServiceAreasControllerTests
    {
        [Fact]
        public async Task CreateShouldSilentlyDropFullyBlankFaqRows()
        {
            var controller = BuildController(out var serviceAreasService);

            var model = ValidArea();
            model.Faqs = new List<ServiceAreaFaqInputModel>
            {
                Faq("How quickly can you get here?", "Usually within two working days."),
                new ServiceAreaFaqInputModel(),
                new ServiceAreaFaqInputModel { Question = "   ", Answer = null },
            };

            var result = await controller.Create(model);

            // A blank row is the admin leaving an unused slot alone, not an error.
            Assert.True(controller.ModelState.IsValid);
            Assert.IsType<RedirectToActionResult>(result);

            serviceAreasService.Verify(
                x => x.CreateAsync(It.Is<ServiceAreaAdminInputModel>(m => m.Faqs.Count == 1)),
                Times.Once);
        }

        [Fact]
        public async Task CreateShouldKeyFaqErrorsToThePostPruneIndex()
        {
            // The actual trap this guards. Row 0 is blank and gets pruned; row 1 is
            // half-filled and invalid. After pruning it renders at index 0, so an error
            // keyed Faqs[1].Answer would attach to a row the admin cannot see.
            var controller = BuildController(out var serviceAreasService);

            var model = ValidArea();
            model.Faqs = new List<ServiceAreaFaqInputModel>
            {
                new ServiceAreaFaqInputModel(),
                Faq("Do you cover weekends?", null),
            };

            var result = await controller.Create(model);

            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey("Faqs[0].Answer"));
            Assert.False(controller.ModelState.ContainsKey("Faqs[1].Answer"));

            Assert.IsType<ViewResult>(result);
            serviceAreasService.Verify(x => x.CreateAsync(It.IsAny<ServiceAreaAdminInputModel>()), Times.Never);
        }

        [Fact]
        public async Task CreateShouldRejectAHalfFilledFaqRow()
        {
            var controller = BuildController(out _);

            var model = ValidArea();
            model.Faqs = new List<ServiceAreaFaqInputModel> { Faq(null, "Yes, we do.") };

            await controller.Create(model);

            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey("Faqs[0].Question"));
        }

        [Fact]
        public async Task CreateShouldRejectADuplicateSlugIncludingDeletedAreas()
        {
            // SlugExistsAsync checks soft-deleted rows too: the unique index on Slug is not
            // filtered on IsDeleted, so a colliding slug would fail at SaveChanges - and on
            // the next boot the seeder would hit the same violation and fail app startup.
            var controller = BuildController(out var serviceAreasService);

            serviceAreasService
                .Setup(x => x.SlugExistsAsync("chessington", null))
                .ReturnsAsync(true);

            var model = ValidArea();
            model.Slug = "chessington";

            var result = await controller.Create(model);

            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey(nameof(model.Slug)));
            Assert.IsType<ViewResult>(result);
            serviceAreasService.Verify(x => x.CreateAsync(It.IsAny<ServiceAreaAdminInputModel>()), Times.Never);
        }

        [Fact]
        public async Task CreateShouldGiveTheRedisplayedFormAtLeastOneFaqRowToTypeInto()
        {
            // Pruning can empty the list entirely; handing the view back zero rows would
            // leave the admin with a form they cannot add a FAQ to.
            var controller = BuildController(out _);

            var model = ValidArea();
            model.Slug = "NOT A VALID SLUG";
            model.Faqs = new List<ServiceAreaFaqInputModel> { new ServiceAreaFaqInputModel() };
            controller.ModelState.AddModelError(nameof(model.Slug), "Invalid slug.");

            var result = await controller.Create(model);

            var returned = Assert.IsType<ServiceAreaAdminInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Single(returned.Faqs);
        }

        private static ServiceAreaFaqInputModel Faq(string question, string answer) =>
            new ServiceAreaFaqInputModel { Question = question, Answer = answer };

        private static ServiceAreaAdminInputModel ValidArea() => new ServiceAreaAdminInputModel
        {
            Name = "Chessington",
            Slug = "chessington",
            Region = "Surrey",
            DriveTimeMinutes = 0,
            Faqs = new List<ServiceAreaFaqInputModel>(),
        };

        private static ServiceAreasController BuildController(out Mock<IServiceAreasService> serviceAreasService)
        {
            serviceAreasService = new Mock<IServiceAreasService>();

            return new ServiceAreasController(serviceAreasService.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
            };
        }
    }
}
