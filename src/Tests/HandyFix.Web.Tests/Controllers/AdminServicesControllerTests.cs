namespace HandyFix.Web.Tests.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;
    using HandyFix.Services.Data.Categories;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.Areas.Administration.Controllers;
    using HandyFix.Web.ViewModels.Administration.Services;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;

    using Moq;

    using Xunit;

    /// <summary>
    /// The form round trip: what the Edit form opens with, and what Create and Edit pass on. The
    /// saving itself is covered in ServicesServiceTests.
    /// </summary>
    public class AdminServicesControllerTests
    {
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

        private static ServicesController BuildController(out Mock<IServicesService> servicesService, out Mock<ICategoriesService> categoriesService)
        {
            servicesService = new Mock<IServicesService>();
            categoriesService = new Mock<ICategoriesService>();
            categoriesService
                .Setup(x => x.GetAllAsync<CategoryViewModel>())
                .ReturnsAsync(new List<CategoryViewModel>());

            return new ServicesController(servicesService.Object, categoriesService.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };
        }
    }
}
