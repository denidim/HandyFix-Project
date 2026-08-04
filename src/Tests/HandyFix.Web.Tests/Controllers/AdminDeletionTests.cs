namespace HandyFix.Web.Tests.Controllers
{
    using System;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Technicians;
    using HandyFix.Web.Areas.Administration.Controllers;
    using HandyFix.Web.ViewModels.Administration.Technicians;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;

    using Moq;

    using Xunit;

    /// <summary>
    /// Technicians refuse deletion when bookings reference them (PROJECT_STATE.md section 3r) -
    /// a silent no-op is what made the review Delete button so confusing (section 3o), so this
    /// covers the admin gets a real explanation either way.
    /// (Services' "clean up the image before the row is gone" rule now lives in
    /// ServicesService.DeleteAsync itself, not the controller - see ServicesServiceTests.)
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

        private static TechniciansController BuildTechniciansController(out Mock<ITechniciansService> techniciansService)
        {
            techniciansService = new Mock<ITechniciansService>();

            return new TechniciansController(techniciansService.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
            };
        }

    }
}
