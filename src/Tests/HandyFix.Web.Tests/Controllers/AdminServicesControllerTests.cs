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

            Assert.IsType<RedirectToActionResult>(result);
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

            Assert.IsType<RedirectToActionResult>(result);
            servicesService.Verify(
                x => x.CreateAsync(model.Name, model.Description, model.BasePrice, model.EstimatedDurationMinutes, model.CategoryId, 12, true),
                Times.Once);
        }

        [Fact]
        public void ANewServiceFormShouldStartOnTheSharedDefaultOrder()
        {
            // A new service left at 0 would jump ahead of every category's general call-out.
            Assert.Equal(Service.DefaultDisplayOrder, new ServiceAdminInputModel().DisplayOrder);
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
