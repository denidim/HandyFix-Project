namespace HandyFix.Web.Tests.Controllers
{
    using System.Collections.Generic;
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
    /// Controller-level wiring only - the FAQ pruning/validation rules themselves now live in
    /// ServiceAreasService.PruneAndValidateFaqs and are covered in
    /// ServiceAreasServiceTests.PruneAndValidateFaqsTests.
    /// </summary>
    public class AdminServiceAreasControllerTests
    {
        [Fact]
        public async Task CreateShouldTranslateEveryServiceReturnedFaqErrorIntoModelState()
        {
            var controller = BuildController(out var serviceAreasService);

            serviceAreasService
                .Setup(x => x.PruneAndValidateFaqs(It.IsAny<ServiceAreaAdminInputModel>()))
                .Returns(new[]
                {
                    new ServiceAreaFaqValidationError { Key = "Faqs[0].Answer", Message = "Answer is required." },
                });

            var result = await controller.Create(ValidArea());

            Assert.False(controller.ModelState.IsValid);
            Assert.True(controller.ModelState.ContainsKey("Faqs[0].Answer"));
            Assert.IsType<ViewResult>(result);
            serviceAreasService.Verify(x => x.CreateAsync(It.IsAny<ServiceAreaAdminInputModel>()), Times.Never);
        }

        [Fact]
        public async Task CreateShouldProceedWhenTheServiceReportsNoFaqErrors()
        {
            var controller = BuildController(out var serviceAreasService);

            var result = await controller.Create(ValidArea());

            Assert.True(controller.ModelState.IsValid);
            Assert.IsType<RedirectToActionResult>(result);
            serviceAreasService.Verify(x => x.CreateAsync(It.IsAny<ServiceAreaAdminInputModel>()), Times.Once);
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
            // Pruning (in ServiceAreasService) can empty the list entirely; handing the view
            // back zero rows would leave the admin with a form they cannot add a FAQ to.
            var controller = BuildController(out _);

            var model = ValidArea();
            model.Slug = "NOT A VALID SLUG";
            model.Faqs = new List<ServiceAreaFaqInputModel>();
            controller.ModelState.AddModelError(nameof(model.Slug), "Invalid slug.");

            var result = await controller.Create(model);

            var returned = Assert.IsType<ServiceAreaAdminInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Single(returned.Faqs);
        }

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

            // Safe default so tests that aren't specifically about FAQ validation don't have to
            // set it up themselves - PruneAndValidateFaqs runs unconditionally on every POST.
            serviceAreasService
                .Setup(x => x.PruneAndValidateFaqs(It.IsAny<ServiceAreaAdminInputModel>()))
                .Returns(new List<ServiceAreaFaqValidationError>());

            return new ServiceAreasController(serviceAreasService.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
            };
        }
    }
}
