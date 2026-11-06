namespace HandyFix.Web.Tests.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;
    using HandyFix.Services;
    using HandyFix.Services.Data.Categories;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.Areas.Administration.Controllers;
    using HandyFix.Web.ViewModels.Administration.Services;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.Extensions.Logging;

    using Moq;

    using Xunit;

    /// <summary>
    /// The form round trip: what the Edit form opens with, and what Create and Edit pass on. The
    /// saving itself is covered in ServicesServiceTests.
    /// </summary>
    public class AdminServicesControllerTests
    {
        private const string RefusedPictureMessage = "Unsupported file type. Only JPEG, PNG, and WEBP images are allowed.";

        private const string PictureNotSavedMessage = "The picture could not be saved. The service itself is saved: try the picture again.";

        [Fact]
        public async Task EditGetShouldOpenTheFormOnEveryFieldItPostsBack()
        {
            // The form posts IsActive, DisplayOrder and IsPopular. IsActive was once not copied
            // into the form, so an inactive service opened ticked and saving any other change
            // switched it back on (PROJECT_STATE Section 3ca). The values here are all the
            // opposite of the input model's defaults, so a field left uncopied fails the test.
            var controller = BuildController(out var servicesService, out _);
            var id = Guid.NewGuid();
            servicesService
                .Setup(x => x.GetByIdAsync<ServiceDetailsViewModel>(id))
                .ReturnsAsync(new ServiceDetailsViewModel
                {
                    Id = id,
                    Name = "Gutter Clearing",
                    Slug = "gutter-clearing",
                    CategoryName = "Handyman",
                    IsActive = false,
                    DisplayOrder = 3,
                    IsPopular = true,
                });

            var result = await controller.Edit(id);

            var model = Assert.IsType<ServiceAdminInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.False(model.IsActive);
            Assert.Equal(3, model.DisplayOrder);
            Assert.True(model.IsPopular);
        }

        [Fact]
        public async Task EditPostShouldPassOnTheOrderAndThePopularTick()
        {
            var controller = BuildController(out var servicesService, out _);
            var model = ValidService();
            model.Id = Guid.NewGuid();
            model.IsActive = false;
            model.DisplayOrder = 0;
            model.IsPopular = true;

            var result = await controller.Edit(model);

            AssertGoesBackToTheAdminList(result);
            servicesService.Verify(
                x => x.UpdateAsync(model.Id.Value, model.Name, model.Description, model.BasePrice, model.EstimatedDurationMinutes, false, model.CategoryId, 0, true),
                Times.Once);
        }

        [Fact]
        public async Task CreatePostShouldPassOnTheOrderAndThePopularTick()
        {
            var controller = BuildController(out var servicesService, out _);
            var model = ValidService();
            model.DisplayOrder = 12;
            model.IsPopular = true;

            var result = await controller.Create(model);

            AssertGoesBackToTheAdminList(result);
            servicesService.Verify(
                x => x.CreateAsync(model.Name, model.Description, model.BasePrice, model.EstimatedDurationMinutes, model.CategoryId, 12, true),
                Times.Once);
        }

        [Fact]
        public async Task CreateShouldRefuseANameAlreadyInUseWithAMessage()
        {
            // Saving it would break the unique index on the slug and show an error page.
            var controller = BuildController(out var servicesService, out _);
            var model = ValidService();
            servicesService.Setup(x => x.NameIsTakenAsync(model.Name, null)).ReturnsAsync(true);

            var result = await controller.Create(model);

            Assert.IsType<ViewResult>(result);
            Assert.True(controller.ModelState.ContainsKey(nameof(model.Name)));
            servicesService.Verify(
                x => x.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task EditShouldRefuseAnotherServicesNameButAllowItsOwn()
        {
            // The check leaves out the service being edited. Asked without its id, every save of
            // an unchanged name would be refused. One controller per call: a controller keeps a
            // single ModelState.
            var model = ValidService();
            model.Id = Guid.NewGuid();

            var allowing = BuildController(out var allowingService, out _);
            allowingService.Setup(x => x.NameIsTakenAsync(model.Name, null)).ReturnsAsync(true);
            allowingService.Setup(x => x.NameIsTakenAsync(model.Name, model.Id)).ReturnsAsync(false);

            AssertGoesBackToTheAdminList(await allowing.Edit(model));

            var refusing = BuildController(out var refusingService, out _);
            refusingService.Setup(x => x.NameIsTakenAsync(model.Name, model.Id)).ReturnsAsync(true);

            var result = await refusing.Edit(model);

            Assert.IsType<ViewResult>(result);
            Assert.True(refusing.ModelState.ContainsKey(nameof(model.Name)));
            refusingService.Verify(
                x => x.UpdateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>()),
                Times.Never);
        }

        [Fact]
        public async Task DeleteShouldGoBackToTheAdminList()
        {
            var controller = BuildController(out var servicesService, out _);
            var id = Guid.NewGuid();

            var result = await controller.Delete(id);

            AssertGoesBackToTheAdminList(result);
            servicesService.Verify(x => x.DeleteAsync(id), Times.Once);
        }

        // A save that left no line on the list gave the admin no sign it had gone through
        // (PROJECT_STATE Section 3cr). Service areas and technicians already said so.
        [Fact]
        public async Task CreateShouldSayTheServiceWasAdded()
        {
            var controller = BuildController(out _, out _);

            await controller.Create(ValidService());

            Assert.Equal("Service \"Gutter Clearing\" was added.", controller.TempData["SuccessMessage"]);
        }

        [Fact]
        public async Task EditShouldSayTheServiceWasUpdated()
        {
            var controller = BuildController(out _, out _);
            var model = ValidService();
            model.Id = Guid.NewGuid();

            await controller.Edit(model);

            Assert.Equal("Service \"Gutter Clearing\" was updated.", controller.TempData["SuccessMessage"]);
        }

        [Fact]
        public async Task DeleteShouldSayWhichServiceWasDeleted()
        {
            var controller = BuildController(out var servicesService, out _);
            var id = Guid.NewGuid();
            servicesService
                .Setup(x => x.GetByIdAsync<ServiceDetailsViewModel>(id))
                .ReturnsAsync(new ServiceDetailsViewModel { Id = id, Name = "Gutter Clearing" });

            await controller.Delete(id);

            Assert.Equal("Service \"Gutter Clearing\" was deleted.", controller.TempData["SuccessMessage"]);
            servicesService.Verify(x => x.DeleteAsync(id), Times.Once);
        }

        // The picture's error goes under the picture box. A file the site does not take is the
        // admin's to put right, and the words written for it reach them as they are.
        [Fact]
        public async Task EditWithAPictureTheSiteDoesNotTakeShowsTheMessageWrittenForIt()
        {
            var controller = BuildController(out var servicesService, out _, out var logger);
            servicesService
                .Setup(x => x.SetServiceImageAsync(It.IsAny<Guid>(), It.IsAny<IFormFile>()))
                .ThrowsAsync(new ImageUploadValidationException(RefusedPictureMessage));

            var result = await SaveAsync(controller, editing: true);

            Assert.IsType<ViewResult>(result);
            Assert.Equal(RefusedPictureMessage, PictureErrorShown(controller));
            VerifyNothingLogged(logger);
        }

        // Any other failure used to put its own text under the picture box: the bucket's, the
        // picture library's. It is logged, and the admin is told in our words that the rest of the
        // service was kept, which is true because the service is saved before its picture.
        [Fact]
        public async Task EditWithAPictureThatFailsForAnyOtherReasonIsLoggedAndToldInOurOwnWords()
        {
            var controller = BuildController(out var servicesService, out _, out var logger);
            var failure = StorageOutOfReach();
            servicesService
                .Setup(x => x.SetServiceImageAsync(It.IsAny<Guid>(), It.IsAny<IFormFile>()))
                .ThrowsAsync(failure);

            var result = await SaveAsync(controller, editing: true);

            Assert.IsType<ViewResult>(result);
            Assert.Equal(PictureNotSavedMessage, PictureErrorShown(controller));
            VerifyLoggedOnce(logger, failure);
            AssertSavedBeforeThePicture(servicesService, nameof(IServicesService.UpdateAsync));
        }

        // A new service is saved before its picture. The Create form used to come back with the
        // error, and sent again it only said the name was taken; the admin now goes to the new
        // service's Edit page, where the picture can be tried again.
        [Fact]
        public async Task CreateWithAPictureTheSiteDoesNotTakeOpensTheNewServiceWithTheReason()
        {
            var controller = BuildController(out var servicesService, out _, out var logger);
            var serviceId = Guid.NewGuid();
            servicesService
                .Setup(x => x.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(serviceId);
            servicesService
                .Setup(x => x.SetServiceImageAsync(It.IsAny<Guid>(), It.IsAny<IFormFile>()))
                .ThrowsAsync(new ImageUploadValidationException(RefusedPictureMessage));

            var result = await controller.Create(ValidService());

            AssertGoesToTheEditPageOf(result, serviceId);
            Assert.Equal($"The service is saved, but not its picture. {RefusedPictureMessage}", controller.TempData["PictureError"]);
            Assert.Null(controller.TempData["SuccessMessage"]);
            VerifyNothingLogged(logger);
        }

        [Fact]
        public async Task CreateWithAPictureThatFailsForAnyOtherReasonOpensTheNewServiceAndIsLogged()
        {
            var controller = BuildController(out var servicesService, out _, out var logger);
            var serviceId = Guid.NewGuid();
            var failure = StorageOutOfReach();
            servicesService
                .Setup(x => x.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>()))
                .ReturnsAsync(serviceId);
            servicesService
                .Setup(x => x.SetServiceImageAsync(It.IsAny<Guid>(), It.IsAny<IFormFile>()))
                .ThrowsAsync(failure);

            var result = await controller.Create(ValidService());

            AssertGoesToTheEditPageOf(result, serviceId);
            Assert.Equal(PictureNotSavedMessage, controller.TempData["PictureError"]);
            VerifyLoggedOnce(logger, failure);
            AssertSavedBeforeThePicture(servicesService, nameof(IServicesService.CreateAsync));
        }

        // The Edit page a new service lands on shows the reason under its picture box.
        [Fact]
        public async Task EditPageShowsThePictureErrorANewServiceArrivedWith()
        {
            var controller = BuildController(out var servicesService, out _);
            var id = Guid.NewGuid();
            servicesService
                .Setup(x => x.GetByIdAsync<ServiceDetailsViewModel>(id))
                .ReturnsAsync(new ServiceDetailsViewModel { Id = id, Name = "Gutter Clearing" });
            controller.TempData["PictureError"] = PictureNotSavedMessage;

            var result = await controller.Edit(id);

            Assert.IsType<ViewResult>(result);
            Assert.Equal(PictureNotSavedMessage, PictureErrorShown(controller));
        }

        [Fact]
        public async Task EditPageOpenedPlainlyShowsNoPictureError()
        {
            var controller = BuildController(out var servicesService, out _);
            var id = Guid.NewGuid();
            servicesService
                .Setup(x => x.GetByIdAsync<ServiceDetailsViewModel>(id))
                .ReturnsAsync(new ServiceDetailsViewModel { Id = id, Name = "Gutter Clearing" });

            await controller.Edit(id);

            Assert.True(controller.ModelState.IsValid);
        }

        [Fact]
        public void ANewServiceFormShouldStartOnTheSharedDefaultOrder()
        {
            // A new service left at 0 would jump ahead of every category's general call-out.
            Assert.Equal(Service.DefaultDisplayOrder, new ServiceAdminInputModel().DisplayOrder);
        }

        // The redirect has to name the controller and the area. Naming only the action resolved
        // to the public ServicesController, which has a fixed address of its own, and every save
        // left the admin on the public /Services page (PROJECT_STATE Section 3ca). WebTests checks
        // that these three values produce the admin address.
        private static void AssertGoesBackToTheAdminList(IActionResult result)
        {
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Equal("Services", redirect.ControllerName);
            Assert.Equal("Administration", redirect.RouteValues["area"]);
        }

        private static ServiceAdminInputModel ValidService() => new ServiceAdminInputModel
        {
            Name = "Gutter Clearing",
            Description = "Clearing leaves and debris from gutters and downpipes.",
            BasePrice = 60m,
            EstimatedDurationMinutes = 90,
            CategoryId = Guid.NewGuid(),
        };

        // The same service through Create, or through Edit as one that already exists.
        private static Task<IActionResult> SaveAsync(ServicesController controller, bool editing)
        {
            var model = ValidService();
            if (!editing)
            {
                return controller.Create(model);
            }

            model.Id = Guid.NewGuid();
            return controller.Edit(model);
        }

        private static string PictureErrorShown(ServicesController controller) =>
            Assert.Single(controller.ModelState[nameof(ServiceAdminInputModel.ImageFile)].Errors).ErrorMessage;

        private static InvalidOperationException StorageOutOfReach() =>
            new InvalidOperationException("Cloudflare R2 is not fully configured. Missing one or more required settings in CloudflareR2 section.");

        // Like the list redirect, it names the controller and the area, or it would resolve to
        // the public ServicesController.
        private static void AssertGoesToTheEditPageOf(IActionResult result, Guid serviceId)
        {
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Edit", redirect.ActionName);
            Assert.Equal("Services", redirect.ControllerName);
            Assert.Equal("Administration", redirect.RouteValues["area"]);
            Assert.Equal(serviceId, redirect.RouteValues["id"]);
        }

        private static void AssertSavedBeforeThePicture(Mock<IServicesService> servicesService, string saveMethod)
        {
            List<string> calls = servicesService.Invocations.Select(x => x.Method.Name).ToList();
            int saved = calls.IndexOf(saveMethod);
            Assert.InRange(saved, 0, calls.IndexOf(nameof(IServicesService.SetServiceImageAsync)) - 1);
        }

        private static void VerifyNothingLogged(Mock<ILogger<ServicesController>> logger) =>
            logger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never);

        private static void VerifyLoggedOnce(Mock<ILogger<ServicesController>> logger, Exception failure) =>
            logger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    failure,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);

        private static ServicesController BuildController(out Mock<IServicesService> servicesService, out Mock<ICategoriesService> categoriesService) =>
            BuildController(out servicesService, out categoriesService, out _);

        private static ServicesController BuildController(out Mock<IServicesService> servicesService, out Mock<ICategoriesService> categoriesService, out Mock<ILogger<ServicesController>> logger)
        {
            servicesService = new Mock<IServicesService>();
            categoriesService = new Mock<ICategoriesService>();
            categoriesService
                .Setup(x => x.GetAllAsync<CategoryViewModel>())
                .ReturnsAsync(new List<CategoryViewModel>());
            logger = new Mock<ILogger<ServicesController>>();

            return new ServicesController(servicesService.Object, categoriesService.Object, logger.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
            };
        }
    }
}
