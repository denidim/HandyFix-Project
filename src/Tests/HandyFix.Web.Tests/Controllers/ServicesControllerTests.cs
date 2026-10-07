namespace HandyFix.Web.Tests.Controllers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Categories;
    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.Controllers;
    using HandyFix.Web.ViewModels.ServiceAreas;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;

    using Moq;

    using Xunit;

    /// <summary>
    /// What the public pages do with a service an admin has switched off. Its form calls that
    /// "not visible to the public"; the lists already left it out, these two places did not
    /// (PROJECT_STATE Section 3ca).
    /// </summary>
    public class ServicesControllerTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DetailsShouldOnlyShowAServiceThatIsSwitchedOn(bool isActive)
        {
            var controller = BuildController(out var servicesService, out _);
            servicesService
                .Setup(x => x.GetBySlugAsync<ServiceDetailsViewModel>("gutter-clearing"))
                .ReturnsAsync(new ServiceDetailsViewModel
                {
                    Name = "Gutter Clearing",
                    Slug = "gutter-clearing",
                    CategoryName = "Handyman",
                    CategorySlug = "handyman",
                    IsActive = isActive,
                });
            servicesService
                .Setup(x => x.GetByCategoryAsync<ServiceViewModel>("Handyman", It.IsAny<bool>()))
                .ReturnsAsync(new List<ServiceViewModel>());

            var result = await controller.Details("handyman", "gutter-clearing");

            if (isActive)
            {
                Assert.IsType<ViewResult>(result);
            }
            else
            {
                Assert.IsType<NotFoundResult>(result);
            }
        }

        [Fact]
        public async Task PricingShouldTakeItsTypicalJobsFromTheServicesThatAreSwitchedOn()
        {
            // A category's own collection holds every service, switched on or not. The typical
            // jobs were looked up in it, so a switched-off one stayed on the page with a Book Now
            // button. Here "shelf-installation" is only in the category's collection.
            var controller = BuildController(out var servicesService, out var categoriesService);
            servicesService
                .Setup(x => x.GetAllAsync<ServiceViewModel>(true))
                .ReturnsAsync(new List<ServiceViewModel>
                {
                    new ServiceViewModel { Slug = "tv-mounting" },
                    new ServiceViewModel { Slug = "tap-repairs" },
                });
            categoriesService
                .Setup(x => x.GetAllAsync<CategoryViewModel>())
                .ReturnsAsync(new List<CategoryViewModel>
                {
                    new CategoryViewModel
                    {
                        Name = "Handyman",
                        Slug = "handyman",
                        Services = new List<ServiceViewModel> { new ServiceViewModel { Slug = "shelf-installation" }, new ServiceViewModel { Slug = "tv-mounting" } },
                    },
                });

            var result = await controller.Pricing();

            var model = Assert.IsType<PricingViewModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(new[] { "tap-repairs", "tv-mounting" }, model.TypicalJobs.Select(s => s.Slug));
        }

        private static ServicesController BuildController(out Mock<IServicesService> servicesService, out Mock<ICategoriesService> categoriesService)
        {
            servicesService = new Mock<IServicesService>();
            categoriesService = new Mock<ICategoriesService>();
            var serviceAreasService = new Mock<IServiceAreasService>();
            serviceAreasService
                .Setup(x => x.GetAllAsync<ServiceAreaViewModel>(It.IsAny<bool>()))
                .ReturnsAsync(new List<ServiceAreaViewModel>());

            return new ServicesController(categoriesService.Object, servicesService.Object, serviceAreasService.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            };
        }
    }
}
