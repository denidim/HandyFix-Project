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
    using HandyFix.Web.ViewModels.Home;
    using HandyFix.Web.ViewModels.Reviews;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;

    using Moq;

    using Xunit;

    /// <summary>
    /// On-site review display is deferred until the Google Business Profile import ships, and is
    /// gated behind Business:ShowOnSiteReviews (PROJECT_STATE.md section 3n). The gate has to do
    /// two things: hide the display, and skip the fetch entirely - a stray Admin-approved row must
    /// not be able to leak onto a public page before the toggle is deliberately flipped.
    /// </summary>
    public class ReviewVisibilityTests
    {
        [Fact]
        public async Task ReviewsPageShouldNotFetchOrExposeReviewsWhileTheToggleIsOff()
        {
            var controller = BuildHomeController(showOnSiteReviews: false, out var reviewsService);

            var result = await controller.Reviews();

            var model = Assert.IsType<ReviewsListViewModel>(Assert.IsType<ViewResult>(result).Model);

            Assert.False(model.ShowOnSiteReviews);
            Assert.Empty(model.Reviews);

            // Not fetched at all, rather than fetched and then hidden by the view.
            reviewsService.Verify(x => x.GetLatestApprovedAsync<ReviewViewModel>(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task ReviewsPageShouldShowReviewsOnceTheToggleIsOn()
        {
            var controller = BuildHomeController(showOnSiteReviews: true, out var reviewsService);

            var result = await controller.Reviews();

            var model = Assert.IsType<ReviewsListViewModel>(Assert.IsType<ViewResult>(result).Model);

            Assert.True(model.ShowOnSiteReviews);
            Assert.Single(model.Reviews);
            reviewsService.Verify(x => x.GetLatestApprovedAsync<ReviewViewModel>(50), Times.Once);
        }

        [Fact]
        public async Task ReviewsPageShouldCarryTheConfiguredGoogleUrlThroughToTheCta()
        {
            // The whole point of the config key is that adding the real profile later is an
            // edit to appsettings, not a code change (VISION_AND_CONTEXT.md section 7).
            var controller = BuildHomeController(
                showOnSiteReviews: false,
                out _,
                googleReviewsUrl: "https://g.page/r/example/review");

            var result = await controller.Reviews();
            var model = Assert.IsType<ReviewsListViewModel>(Assert.IsType<ViewResult>(result).Model);

            Assert.Equal("https://g.page/r/example/review", model.GoogleReviewsUrl);
        }

        [Fact]
        public async Task ReviewsPageShouldLeaveTheGoogleUrlEmptyWhenItIsNotConfiguredYet()
        {
            // Empty by default and deliberately not guessed - no Business Profile exists yet.
            var controller = BuildHomeController(showOnSiteReviews: false, out _);

            var result = await controller.Reviews();
            var model = Assert.IsType<ReviewsListViewModel>(Assert.IsType<ViewResult>(result).Model);

            Assert.True(string.IsNullOrEmpty(model.GoogleReviewsUrl));
        }

        [Fact]
        public async Task HomepageShouldNotFetchOrExposeSliderReviewsWhileTheToggleIsOff()
        {
            var controller = BuildHomeController(showOnSiteReviews: false, out var reviewsService);

            var result = await controller.Index();

            var model = Assert.IsType<HomeIndexViewModel>(Assert.IsType<ViewResult>(result).Model);

            Assert.False(model.ShowOnSiteReviews);
            Assert.Empty(model.SliderReviews);
            reviewsService.Verify(x => x.GetLatestApprovedAsync<ReviewViewModel>(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task HomepageShouldShowSliderReviewsOnceTheToggleIsOn()
        {
            var controller = BuildHomeController(showOnSiteReviews: true, out var reviewsService);

            var result = await controller.Index();

            var model = Assert.IsType<HomeIndexViewModel>(Assert.IsType<ViewResult>(result).Model);

            Assert.True(model.ShowOnSiteReviews);
            Assert.Single(model.SliderReviews);
            reviewsService.Verify(x => x.GetLatestApprovedAsync<ReviewViewModel>(6), Times.Once);
        }

        private static HomeController BuildHomeController(
            bool showOnSiteReviews,
            out Mock<IReviewsService> reviewsService,
            string googleReviewsUrl = null)
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

            var settings = new Dictionary<string, string>
            {
                ["Business:ShowOnSiteReviews"] = showOnSiteReviews.ToString().ToLowerInvariant(),
            };

            if (googleReviewsUrl != null)
            {
                settings["Business:GoogleReviewsUrl"] = googleReviewsUrl;
            }

            var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

            return new HomeController(
                reviewsService.Object,
                new Mock<IInquiriesService>().Object,
                servicesService.Object,
                categoriesService.Object,
                new Mock<IImageService>().Object,
                configuration);
        }
    }
}
