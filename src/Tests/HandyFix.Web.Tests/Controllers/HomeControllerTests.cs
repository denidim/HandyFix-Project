namespace HandyFix.Web.Tests.Controllers
{
    using System;
    using System.Collections.Generic;

    using HandyFix.Services;
    using HandyFix.Services.Data.Categories;
    using HandyFix.Services.Data.Inquiries;
    using HandyFix.Services.Data.Reviews;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.Controllers;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Configuration;

    using Moq;

    using Xunit;

    public class HomeControllerTests
    {
        [Fact]
        public void AboutShouldReturnViewWithExpectedTitleAndMetaDescription()
        {
            var controller = BuildController();

            var result = controller.About();

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal("About Us - Plumbing Handyman Surrey", controller.ViewData["Title"]);
            Assert.NotNull(controller.ViewData["MetaDescription"]);
        }

        private static HomeController BuildController()
        {
            var reviewsService = new Mock<IReviewsService>();
            var inquiriesService = new Mock<IInquiriesService>();
            var servicesService = new Mock<IServicesService>();
            var categoriesService = new Mock<ICategoriesService>();
            var imageService = new Mock<IImageService>();
            var configuration = new Mock<IConfiguration>();

            var controller = new HomeController(
                reviewsService.Object,
                inquiriesService.Object,
                servicesService.Object,
                categoriesService.Object,
                imageService.Object,
                configuration.Object);

            var httpContext = new DefaultHttpContext();
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
            };

            return controller;
        }
    }
}
