namespace HandyFix.Web.Tests.Controllers
{
    using System;
    using System.Threading.Tasks;

    using HandyFix.Services;
    using HandyFix.Services.Data.Categories;
    using HandyFix.Services.Data.Services;
    using HandyFix.Services.Data.Technicians;
    using HandyFix.Web.Areas.Administration.Controllers;
    using HandyFix.Web.ViewModels.Administration.Technicians;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;

    using Moq;

    using Xunit;

    /// <summary>
    /// Deletion behaves deliberately differently per entity, and the differences are easy to
    /// "tidy" into a bug later: technicians refuse deletion when bookings reference them
    /// (PROJECT_STATE.md section 3r), while services must clean up their image file on the way out.
    /// </summary>
    public class AdminDeletionTests
    {
        [Fact]
        public async Task DeletingATechnicianWithBookingsShouldBeRefusedWithAnExplanation()
        {
            // Booking.TechnicianId is a live FK into real job history. Deactivation is the
            // retire path; deleting is only for a row created in error.
            var controller = BuildTechniciansController(out var techniciansService);

            var id = Guid.NewGuid();
            techniciansService
                .Setup(x => x.GetByIdAsync<TechnicianAdminInputModel>(id))
                .ReturnsAsync(new TechnicianAdminInputModel { Id = id, FirstName = "John", LastName = "Doe" });

            techniciansService.Setup(x => x.DeleteAsync(id)).ReturnsAsync(false);

            var result = await controller.Delete(id);

            Assert.IsType<RedirectToActionResult>(result);

            // The admin has to be told why nothing happened, and pointed at deactivation -
            // a silent no-op is what made the review Delete button so confusing (section 3o).
            var message = Assert.IsType<string>(controller.TempData["ErrorMessage"]);
            Assert.Contains("can't be deleted", message);
            Assert.Contains("deactivate", message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(controller.TempData["SuccessMessage"]);
        }

        [Fact]
        public async Task DeletingATechnicianWithoutBookingsShouldReportSuccess()
        {
            var controller = BuildTechniciansController(out var techniciansService);

            var id = Guid.NewGuid();
            techniciansService
                .Setup(x => x.GetByIdAsync<TechnicianAdminInputModel>(id))
                .ReturnsAsync(new TechnicianAdminInputModel { Id = id, FirstName = "Ada", LastName = "Lovelace" });

            techniciansService.Setup(x => x.DeleteAsync(id)).ReturnsAsync(true);

            await controller.Delete(id);

            var message = Assert.IsType<string>(controller.TempData["SuccessMessage"]);
            Assert.Contains("Ada Lovelace", message);
            Assert.Null(controller.TempData["ErrorMessage"]);
        }

        [Fact]
        public async Task DeletingAnUnknownTechnicianShouldReturnNotFoundWithoutCallingTheService()
        {
            var controller = BuildTechniciansController(out var techniciansService);

            techniciansService
                .Setup(x => x.GetByIdAsync<TechnicianAdminInputModel>(It.IsAny<Guid>()))
                .ReturnsAsync((TechnicianAdminInputModel)null);

            var result = await controller.Delete(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
            techniciansService.Verify(x => x.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task DeletingAServiceShouldRemoveItsImageBeforeTheRow()
        {
            // Order is load-bearing: the image path is derived from the service's slug, so
            // once the row is gone the file can no longer be located and would be orphaned
            // in wwwroot forever.
            var controller = BuildServicesController(out var servicesService, out var imageStorageService);

            var id = Guid.NewGuid();
            servicesService
                .Setup(x => x.GetByIdAsync<ServiceDetailsViewModel>(id))
                .ReturnsAsync(new ServiceDetailsViewModel { Id = id, Slug = "tap-repairs" });

            var sequence = new MockSequence();
            imageStorageService.InSequence(sequence).Setup(x => x.DeleteServiceImage("tap-repairs"));
            servicesService.InSequence(sequence).Setup(x => x.DeleteAsync(id)).Returns(Task.CompletedTask);

            var result = await controller.Delete(id);

            imageStorageService.Verify(x => x.DeleteServiceImage("tap-repairs"), Times.Once);
            servicesService.Verify(x => x.DeleteAsync(id), Times.Once);
            Assert.IsType<RedirectToActionResult>(result);
        }

        [Fact]
        public async Task DeletingAnUnknownServiceShouldNotAttemptAnImageDelete()
        {
            // A null slug would resolve to a path that is not this service's image.
            var controller = BuildServicesController(out var servicesService, out var imageStorageService);

            servicesService
                .Setup(x => x.GetByIdAsync<ServiceDetailsViewModel>(It.IsAny<Guid>()))
                .ReturnsAsync((ServiceDetailsViewModel)null);

            await controller.Delete(Guid.NewGuid());

            imageStorageService.Verify(x => x.DeleteServiceImage(It.IsAny<string>()), Times.Never);
        }

        private static TechniciansController BuildTechniciansController(out Mock<ITechniciansService> techniciansService)
        {
            techniciansService = new Mock<ITechniciansService>();

            return new TechniciansController(techniciansService.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
            };
        }

        private static Areas.Administration.Controllers.ServicesController BuildServicesController(
            out Mock<IServicesService> servicesService,
            out Mock<IImageStorageService> imageStorageService)
        {
            servicesService = new Mock<IServicesService>();
            imageStorageService = new Mock<IImageStorageService>();

            return new Areas.Administration.Controllers.ServicesController(
                servicesService.Object,
                new Mock<ICategoriesService>().Object,
                imageStorageService.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
            };
        }
    }
}
