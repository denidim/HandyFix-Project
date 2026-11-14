namespace HandyFix.Web.Tests.Controllers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services;
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

        // The master plan's formula (item 7): the price people compare on, then what the visitor
        // can do there: book a slot, or call for an emergency, which is never booked as a slot. A
        // job that is only quoted offers the survey and nothing after it. The emergency's rate is
        // its own admin price, like any other.
        [Theory]
        [InlineData("Tap Repairs", "plumbing", 90, false, "Tap Repairs in Surrey from £90", PageTitle.BookOnline)]
        [InlineData("Emergency Plumbing", "plumbing", 150, false, "Emergency Plumbing in Surrey from £150", PageTitle.CallOrWhatsApp)]
        [InlineData("Kitchen Fitting & Alterations", "small-building-works", 0, false, "Kitchen Fitting & Alterations in Surrey, Free Survey", PageTitle.NoEnding)]
        [InlineData("Gutter Clearing", "handyman", 60, true, "Gutter Clearing in Surrey, Free Survey", PageTitle.NoEnding)]
        public async Task DetailsShouldNameThePageByTheServiceTheCountyAndThePrice(string name, string categorySlug, int price, bool isQuoteOnly, string expected, string expectedEnding)
        {
            var controller = BuildController(out var servicesService, out _);
            servicesService
                .Setup(x => x.GetBySlugAsync<ServiceDetailsViewModel>("the-service"))
                .ReturnsAsync(new ServiceDetailsViewModel
                {
                    Name = name,
                    Slug = "the-service",
                    CategoryName = "Any",
                    CategorySlug = categorySlug,
                    BasePrice = price,
                    IsQuoteOnly = isQuoteOnly,
                    IsEmergency = name.StartsWith("Emergency"),
                    IsActive = true,
                });
            servicesService
                .Setup(x => x.GetByCategoryAsync<ServiceViewModel>("Any", It.IsAny<bool>()))
                .ReturnsAsync(new List<ServiceViewModel>());

            await controller.Details(categorySlug, "the-service");

            Assert.Equal(expected, controller.ViewData["Title"]);
            Assert.Equal(expectedEnding, controller.ViewData["TitleEnding"]);
        }

        // The description for search engines is the service's own summary. The formula before it
        // was one sentence for every page, with the name in lower case ("Need tv mounting ...").
        // A service without a summary gives the start of its description, as its card does.
        [Theory]
        [InlineData("A TV put on the wall, level and with its cables tidy.", "A TV put on the wall, level and with its cables tidy.")]
        [InlineData(null, "The description's first sentence is long enough to stand alone on a card.")]
        public async Task DetailsShouldDescribeThePageByTheServicesOwnSummary(string summary, string expected)
        {
            var controller = BuildController(out var servicesService, out _);
            servicesService
                .Setup(x => x.GetBySlugAsync<ServiceDetailsViewModel>("tv-mounting"))
                .ReturnsAsync(new ServiceDetailsViewModel
                {
                    Name = "TV Mounting",
                    Slug = "tv-mounting",
                    CategoryName = "Handyman",
                    CategorySlug = "handyman",
                    Summary = summary,
                    Description = "The description's first sentence is long enough to stand alone on a card. The second is not printed.",
                    BasePrice = 60,
                    IsActive = true,
                });
            servicesService
                .Setup(x => x.GetByCategoryAsync<ServiceViewModel>("Handyman", It.IsAny<bool>()))
                .ReturnsAsync(new List<ServiceViewModel>());

            await controller.Details("handyman", "tv-mounting");

            Assert.Equal(expected, controller.ViewData["MetaDescription"]);
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

            return new ServicesController(categoriesService.Object, servicesService.Object, serviceAreasService.Object, new Mock<IImageStorageService>().Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },

                // A page names its own address for search engines, which needs the site's routes.
                // There are none here, so the address comes back empty; WebTests checks the real ones.
                Url = Mock.Of<IUrlHelper>(),
            };
        }
    }
}
