namespace HandyFix.Web.Tests.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    using HandyFix.Services;
    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Web.Areas.Administration.Controllers;
    using HandyFix.Web.ViewModels.ServiceAreas;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.Logging.Abstractions;

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

        // A name taken or a town not found comes back from the service: it goes under its field,
        // the form shows again, and no picture is tried for an area that was not saved
        // (PROJECT_STATE.md Section 3dn).
        [Fact]
        public async Task CreateShouldShowTheServicesReasonUnderItsFieldAndSaveNoPicture()
        {
            var controller = BuildController(out var serviceAreasService);
            serviceAreasService
                .Setup(x => x.CreateAsync(It.IsAny<ServiceAreaAdminInputModel>()))
                .ReturnsAsync(ServiceAreaSaveResult.Failed("Name", "We could not find a town called Molsey. Check the spelling."));

            var result = await controller.Create(ValidArea());

            var returned = Assert.IsType<ServiceAreaAdminInputModel>(Assert.IsType<ViewResult>(result).Model);
            var error = Assert.Single(controller.ModelState["Name"].Errors);
            Assert.Equal("We could not find a town called Molsey. Check the spelling.", error.ErrorMessage);
            Assert.Single(returned.Faqs);
            serviceAreasService.Verify(x => x.SetHeroImageAsync(It.IsAny<Guid>(), It.IsAny<IFormFile>()), Times.Never);
        }

        // The same on the Edit page, and the form shows again with the saved page address.
        [Fact]
        public async Task EditShouldShowTheServicesReasonUnderItsFieldWithTheSavedAddress()
        {
            var controller = BuildController(out var serviceAreasService);
            var model = ValidArea();
            model.Id = Guid.NewGuid();
            serviceAreasService
                .Setup(x => x.GetByIdAsync<ServiceAreaAdminInputModel>(model.Id.Value))
                .ReturnsAsync(new ServiceAreaAdminInputModel { Slug = "chessington", DriveTimeMinutes = 0 });
            serviceAreasService
                .Setup(x => x.UpdateAsync(model.Id.Value, It.IsAny<ServiceAreaAdminInputModel>()))
                .ReturnsAsync(ServiceAreaSaveResult.Failed("Name", "We could not find a town called Chesington. Check the spelling."));

            var result = await controller.Edit(model);

            var returned = Assert.IsType<ServiceAreaAdminInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal("chessington", returned.Slug);
            Assert.Single(controller.ModelState["Name"].Errors);
            serviceAreasService.Verify(x => x.SetHeroImageAsync(It.IsAny<Guid>(), It.IsAny<IFormFile>()), Times.Never);
        }

        [Fact]
        public async Task CreateShouldGiveTheRedisplayedFormAtLeastOneFaqRowToTypeInto()
        {
            // Pruning (in ServiceAreasService) can empty the list entirely; handing the view
            // back zero rows would leave the admin with a form they cannot add a FAQ to.
            var controller = BuildController(out _);

            var model = ValidArea();
            model.Faqs = new List<ServiceAreaFaqInputModel>();
            controller.ModelState.AddModelError(nameof(model.Name), "Area name is required.");

            var result = await controller.Create(model);

            var returned = Assert.IsType<ServiceAreaAdminInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Single(returned.Faqs);
        }

        // The area is saved before its picture. A picture the site will not take used to have no
        // box to go in; now the admin lands on the new area's Edit page with the reason, as for a
        // service (PROJECT_STATE.md Section 3dl).
        [Fact]
        public async Task CreateWhosePictureIsRefusedOpensTheNewAreasEditPageWithTheReason()
        {
            var controller = BuildController(out var serviceAreasService);
            var areaId = Guid.NewGuid();
            serviceAreasService.Setup(x => x.CreateAsync(It.IsAny<ServiceAreaAdminInputModel>())).ReturnsAsync(ServiceAreaSaveResult.Saved(areaId));
            serviceAreasService
                .Setup(x => x.SetHeroImageAsync(areaId, It.IsAny<IFormFile>()))
                .ThrowsAsync(new ImageUploadValidationException("Failed to decode uploaded image."));

            var result = await controller.Create(ValidArea());

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(ServiceAreasController.Edit), redirect.ActionName);
            Assert.Equal(areaId, redirect.RouteValues["id"]);
            Assert.Equal("The area is saved, but not its picture. Failed to decode uploaded image.", controller.TempData["AreaPictureError"]);
        }

        // A bucket out of reach is logged and said in our words under the picture box; the form
        // comes back with the picture the area still has.
        [Fact]
        public async Task EditWhosePictureCannotBeSavedShowsTheFormAgainInOurWords()
        {
            var controller = BuildController(out var serviceAreasService);
            var model = ValidArea();
            model.Id = Guid.NewGuid();
            serviceAreasService
                .Setup(x => x.GetByIdAsync<ServiceAreaAdminInputModel>(model.Id.Value))
                .ReturnsAsync(new ServiceAreaAdminInputModel { Slug = "chessington", HeroImageUrl = "https://photos.example/areas/old.webp" });
            serviceAreasService
                .Setup(x => x.SetHeroImageAsync(model.Id.Value, It.IsAny<IFormFile>()))
                .ThrowsAsync(new InvalidOperationException("The bucket could not be reached."));

            var result = await controller.Edit(model);

            var returned = Assert.IsType<ServiceAreaAdminInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal("https://photos.example/areas/old.webp", returned.CurrentPictureUrl);
            var error = Assert.Single(controller.ModelState[nameof(model.HeroImageFile)].Errors);
            Assert.Equal("The picture could not be saved. The area itself is saved: try the picture again.", error.ErrorMessage);
        }

        private static ServiceAreaAdminInputModel ValidArea() => new ServiceAreaAdminInputModel
        {
            Name = "Chessington",
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

            // Saving works unless a test says otherwise.
            serviceAreasService
                .Setup(x => x.CreateAsync(It.IsAny<ServiceAreaAdminInputModel>()))
                .ReturnsAsync(ServiceAreaSaveResult.Saved(Guid.NewGuid()));
            serviceAreasService
                .Setup(x => x.UpdateAsync(It.IsAny<Guid>(), It.IsAny<ServiceAreaAdminInputModel>()))
                .ReturnsAsync((Guid id, ServiceAreaAdminInputModel _) => ServiceAreaSaveResult.Saved(id));

            return new ServiceAreasController(
                serviceAreasService.Object,
                Mock.Of<IWebHostEnvironment>(e => e.WebRootPath == Path.GetTempPath() && e.WebRootFileProvider == new NullFileProvider()),
                NullLogger<ServiceAreasController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
            };
        }
    }
}
