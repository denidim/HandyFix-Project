namespace HandyFix.Web.Tests.Controllers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Services;
    using HandyFix.Services.Data.Categories;
    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Services.Data.Reviews;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.Controllers;
    using HandyFix.Web.Services.Forms;
    using HandyFix.Web.Settings;
    using HandyFix.Web.ViewModels.Home;
    using HandyFix.Web.ViewModels.Reviews;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Logging.Abstractions;
    using Microsoft.Extensions.Options;

    using Moq;

    using Xunit;

    /// <summary>
    /// Our Google reviews, typed in by the admin, show on /Reviews and the home page with no
    /// switch to turn on (PROJECT_STATE.md Section 3eh; until then Business:ShowOnSiteReviews hid
    /// them, Section 3n). The full-stack checks of the page itself are in ReviewsPageWebTests.
    /// </summary>
    public class ReviewVisibilityTests
    {
        [Fact]
        public async Task ReviewsPageShouldShowTheNewestApprovedReviews()
        {
            var controller = BuildHomeController(out var reviewsService);

            var result = await controller.Reviews();

            var model = Assert.IsType<ReviewsListViewModel>(Assert.IsType<ViewResult>(result).Model);

            Assert.Single(model.Reviews);
            reviewsService.Verify(x => x.GetLatestApprovedAsync<ReviewViewModel>(50), Times.Once);
        }

        [Fact]
        public async Task ReviewsPageShouldCarryBothConfiguredGoogleLinksThroughToTheCard()
        {
            // The two links are config keys, so a new Business Profile is an edit to appsettings,
            // not a code change (VISION_AND_CONTEXT.md section 7). Each must reach its own button:
            // swapped, "Read Our Google Reviews" would open the form for writing one.
            var controller = BuildHomeController(
                out _,
                googleReviewsUrl: "https://g.page/r/example",
                googleLeaveReviewUrl: "https://g.page/r/example/review");

            var result = await controller.Reviews();
            var model = Assert.IsType<ReviewsListViewModel>(Assert.IsType<ViewResult>(result).Model);

            Assert.Equal("https://g.page/r/example", model.GoogleReviewsUrl);
            Assert.Equal("https://g.page/r/example/review", model.GoogleLeaveReviewUrl);
        }

        [Fact]
        public async Task ReviewsPageShouldLeaveTheGoogleLinksEmptyWhenTheyAreNotConfigured()
        {
            // Not guessed: with no links the view shows the card without buttons, rather than
            // buttons that go nowhere.
            var controller = BuildHomeController(out _);

            var result = await controller.Reviews();
            var model = Assert.IsType<ReviewsListViewModel>(Assert.IsType<ViewResult>(result).Model);

            Assert.True(string.IsNullOrEmpty(model.GoogleReviewsUrl));
            Assert.True(string.IsNullOrEmpty(model.GoogleLeaveReviewUrl));
        }

        [Fact]
        public async Task HomepageShouldShowTheSixNewestApprovedReviews()
        {
            var controller = BuildHomeController(out var reviewsService);

            var result = await controller.Index();

            var model = Assert.IsType<HomeIndexViewModel>(Assert.IsType<ViewResult>(result).Model);

            Assert.Single(model.SliderReviews);
            reviewsService.Verify(x => x.GetLatestApprovedAsync<ReviewViewModel>(6), Times.Once);
        }

        private static HomeController BuildHomeController(
            out Mock<IReviewsService> reviewsService,
            string googleReviewsUrl = null,
            string googleLeaveReviewUrl = null)
        {
            reviewsService = new Mock<IReviewsService>();
            reviewsService
                .Setup(x => x.GetLatestApprovedAsync<ReviewViewModel>(It.IsAny<int>()))
                .ReturnsAsync(new List<ReviewViewModel> { new ReviewViewModel() });

            var servicesService = new Mock<IServicesService>();
            servicesService
                .Setup(x => x.GetAllAsync<ServiceViewModel>(It.IsAny<bool>()))
                .ReturnsAsync(new List<ServiceViewModel> { new ServiceViewModel() });

            var categoriesService = new Mock<ICategoriesService>();
            categoriesService
                .Setup(x => x.GetAllAsync<CategoryViewModel>())
                .ReturnsAsync(new List<CategoryViewModel>());

            var business = new BusinessOptions
            {
                GoogleReviewsUrl = googleReviewsUrl,
                GoogleLeaveReviewUrl = googleLeaveReviewUrl,
            };

            return new HomeController(
                reviewsService.Object,
                new Mock<IInquiriesService>().Object,
                servicesService.Object,
                categoriesService.Object,
                new Mock<IImageService>().Object,
                new Mock<IImageStorageService>().Object,
                new Mock<IFormGuard>().Object,
                Options.Create(business),
                NullLogger<HomeController>.Instance)
            {
                // Both pages name their own address for search engines, which needs a request and
                // the site's routes. With no routes here the address comes back empty; WebTests
                // checks the real ones.
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
                Url = Mock.Of<IUrlHelper>(),
            };
        }
    }
}
