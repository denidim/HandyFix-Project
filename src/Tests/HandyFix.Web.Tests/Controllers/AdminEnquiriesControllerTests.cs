namespace HandyFix.Web.Tests.Controllers
{
    using System;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Web.Areas.Administration.Controllers;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.Extensions.Logging.Abstractions;

    using Moq;

    using Xunit;

    // "Delete Permanent" on an enquiry is a real delete, photos included (PROJECT_STATE.md
    // Section 3cb). Removing the photos from storage can fail, and the admin has to be told.
    public class AdminEnquiriesControllerTests
    {
        [Fact]
        public async Task DeleteShouldGoBackToTheListAndSayTheEnquiryIsGone()
        {
            var controller = BuildController(out var inquiriesService);
            var id = Guid.NewGuid();

            var result = await controller.Delete(id);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
            Assert.Contains("deleted for good", Assert.IsType<string>(controller.TempData["SuccessMessage"]));
            Assert.Null(controller.TempData["ErrorMessage"]);
            inquiriesService.Verify(x => x.DeleteAsync(id), Times.Once);
        }

        [Fact]
        public async Task DeleteShouldGoBackToTheEnquiryWithAMessageWhenItCouldNotBeDeleted()
        {
            var controller = BuildController(out var inquiriesService);
            var id = Guid.NewGuid();
            inquiriesService
                .Setup(x => x.DeleteAsync(id))
                .ThrowsAsync(new InvalidOperationException("Storage is unreachable."));

            var result = await controller.Delete(id);

            // Not an error page: the enquiry is still there and the button can be pressed again.
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Details", redirect.ActionName);
            Assert.Equal(id, redirect.RouteValues["id"]);
            Assert.Contains("could not be deleted", Assert.IsType<string>(controller.TempData["ErrorMessage"]));
            Assert.Null(controller.TempData["SuccessMessage"]);
        }

        private static EnquiriesController BuildController(out Mock<IInquiriesService> inquiriesService)
        {
            inquiriesService = new Mock<IInquiriesService>();

            return new EnquiriesController(inquiriesService.Object, NullLogger<EnquiriesController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
            };
        }
    }
}
