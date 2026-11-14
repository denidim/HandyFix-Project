namespace HandyFix.Web.Tests.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Services;
    using HandyFix.Services.Data.Gallery;
    using HandyFix.Web.Controllers;
    using HandyFix.Web.Presentation;
    using HandyFix.Web.ViewModels.Gallery;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.Extensions.Logging.Abstractions;

    using Moq;

    using Xunit;

    using AdminGalleryController = HandyFix.Web.Areas.Administration.Controllers.GalleryController;

    public class GalleryControllerTests
    {
        [Fact]
        public async Task PublicIndexShouldReturnViewWithPhotosAndSeoMetadata()
        {
            var photos = new List<GalleryPhotoViewModel>
            {
                new GalleryPhotoViewModel { Id = Guid.NewGuid(), ImageUrl = "https://photos.example/gallery/a.webp", Caption = "New basin mixer fitted" },
            };
            var service = new Mock<IGalleryService>();
            service.Setup(s => s.GetAllAsync()).ReturnsAsync(photos);

            var controller = new GalleryController(service.Object);
            AttachHttpContext(controller);

            IActionResult result = await controller.Index();

            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<GalleryListViewModel>(viewResult.Model);
            Assert.Single(model.Photos);
            Assert.Equal("page-bg-services-category", controller.ViewData["BodyClass"]);
            Assert.Equal("Recent Plumbing & Handyman Work in Surrey", controller.ViewData["Title"]);
            Assert.Equal(PageTitle.BookOnline, controller.ViewData["TitleEnding"]);
            Assert.NotNull(controller.ViewData["MetaDescription"]);
        }

        [Fact]
        public async Task AdminUploadWithoutFilesShouldReturnIndexWithValidationError()
        {
            var service = new Mock<IGalleryService>();
            service.Setup(s => s.GetAllAsync()).ReturnsAsync(Array.Empty<GalleryPhotoViewModel>());
            var controller = BuildAdminController(service);

            IActionResult result = await controller.Upload(new GalleryBatchUploadInputModel { Caption = "Job done" });

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Index", viewResult.ViewName);
            Assert.False(controller.ModelState.IsValid);
            service.Verify(s => s.CreateBatchAsync(It.IsAny<GalleryBatchUploadInputModel>()), Times.Never);
        }

        [Fact]
        public async Task AdminUploadWithValidationExceptionShouldRedisplayIndexWithMessage()
        {
            var file = new Mock<IFormFile>();
            file.SetupGet(f => f.Length).Returns(100);

            var service = new Mock<IGalleryService>();
            service.Setup(s => s.GetAllAsync()).ReturnsAsync(Array.Empty<GalleryPhotoViewModel>());
            service.Setup(s => s.CreateBatchAsync(It.IsAny<GalleryBatchUploadInputModel>()))
                .ThrowsAsync(new ImageUploadValidationException("Unsupported file type."));

            var controller = BuildAdminController(service);

            IActionResult result = await controller.Upload(new GalleryBatchUploadInputModel
            {
                Caption = "Valid caption",
                Photos = new List<IFormFile> { file.Object },
            });

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("Index", viewResult.ViewName);
            Assert.False(controller.ModelState.IsValid);
        }

        [Fact]
        public async Task AdminEditToggleMoveAndDeleteShouldCallServiceAndRedirect()
        {
            Guid photoId = Guid.NewGuid();
            var service = new Mock<IGalleryService>();
            service.Setup(s => s.GetByIdAsync<GalleryPhotoEditInputModel>(photoId))
                .ReturnsAsync(new GalleryPhotoEditInputModel { Id = photoId, Caption = "Existing" });
            service.Setup(s => s.UpdateAsync(photoId, It.IsAny<GalleryPhotoEditInputModel>())).ReturnsAsync(true);
            service.Setup(s => s.ToggleHomePageAsync(photoId)).ReturnsAsync(true);
            service.Setup(s => s.MoveAsync(photoId, -1)).ReturnsAsync(true);
            service.Setup(s => s.DeleteAsync(photoId)).ReturnsAsync(true);

            AdminGalleryController controller = BuildAdminController(service);

            var editGet = Assert.IsType<ViewResult>(await controller.Edit(photoId));
            Assert.IsType<GalleryPhotoEditInputModel>(editGet.Model);

            var editPost = Assert.IsType<RedirectToActionResult>(await controller.Edit(new GalleryPhotoEditInputModel { Id = photoId, Caption = "Updated" }));
            Assert.Equal("Index", editPost.ActionName);

            var toggle = Assert.IsType<RedirectToActionResult>(await controller.ToggleHome(photoId));
            Assert.Equal("Index", toggle.ActionName);

            var move = Assert.IsType<RedirectToActionResult>(await controller.Move(photoId, -1));
            Assert.Equal("Index", move.ActionName);

            var delete = Assert.IsType<RedirectToActionResult>(await controller.Delete(photoId));
            Assert.Equal("Index", delete.ActionName);
        }

        private static AdminGalleryController BuildAdminController(Mock<IGalleryService> service)
        {
            var controller = new AdminGalleryController(service.Object, NullLogger<AdminGalleryController>.Instance);
            AttachHttpContext(controller);
            return controller;
        }

        private static void AttachHttpContext(Controller controller)
        {
            var httpContext = new DefaultHttpContext();
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
            controller.Url = Mock.Of<IUrlHelper>();
        }
    }
}
