namespace HandyFix.Web.Tests.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Reviews;
    using HandyFix.Services.Data.ServiceAreas;
    using HandyFix.Web.Areas.Administration.Controllers;
    using HandyFix.Web.ViewModels.Reviews;
    using HandyFix.Web.ViewModels.ServiceAreas;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;

    using Moq;

    using Xunit;

    // The admin's Add Review and Edit Review forms (PROJECT_STATE.md Section 3eh).
    public class AdminReviewsControllerTests
    {
        [Fact]
        public async Task CreateOffersOurTownsAndTodaysDate()
        {
            var controller = BuildController(out _);

            var result = await controller.Create();

            var model = Assert.IsType<ReviewInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(new[] { "Cobham", "Esher" }, model.TownSuggestions);
            Assert.Equal(DateTime.Today, model.ReviewedOn);
            Assert.Equal(5, model.Rating);
        }

        [Fact]
        public async Task ACreateThatFailsTheChecksIsShownAgainWithTheTownsAndNothingSaved()
        {
            var controller = BuildController(out var reviewsService);
            controller.ModelState.AddModelError(nameof(ReviewInputModel.Comment), "The review's text is required.");

            var result = await controller.Create(new ReviewInputModel { CustomerName = "Sarah K." });

            var model = Assert.IsType<ReviewInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(new[] { "Cobham", "Esher" }, model.TownSuggestions);
            reviewsService.Verify(x => x.CreateAsync(It.IsAny<ReviewInputModel>()), Times.Never);
        }

        [Fact]
        public async Task ACreateSavesTheReviewAndGoesBackToTheList()
        {
            var controller = BuildController(out var reviewsService);
            var input = new ReviewInputModel { CustomerName = "Sarah K.", Rating = 5, Comment = "Great job.", ReviewedOn = DateTime.Today };

            var result = await controller.Create(input);

            Assert.Equal(nameof(ReviewsController.Index), Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Equal("Sarah K.'s review was added. It is on the website now.", controller.TempData["SuccessMessage"]);
            reviewsService.Verify(x => x.CreateAsync(input), Times.Once);
        }

        [Fact]
        public async Task EditingAReviewThatIsNotThereIsNotFound()
        {
            var controller = BuildController(out _);

            Assert.IsType<NotFoundResult>(await controller.Edit(Guid.NewGuid()));
            Assert.IsType<NotFoundResult>(await controller.Edit(new ReviewInputModel { Id = Guid.NewGuid(), CustomerName = "Sarah K." }));
            Assert.IsType<NotFoundResult>(await controller.Edit(new ReviewInputModel { CustomerName = "Sarah K." }));
        }

        private static ReviewsController BuildController(out Mock<IReviewsService> reviewsService)
        {
            reviewsService = new Mock<IReviewsService>();
            reviewsService.Setup(x => x.UpdateAsync(It.IsAny<Guid>(), It.IsAny<ReviewInputModel>())).ReturnsAsync(false);

            var areasService = new Mock<IServiceAreasService>();
            areasService
                .Setup(x => x.GetAllAsync<ServiceAreaViewModel>(false))
                .ReturnsAsync(new List<ServiceAreaViewModel> { new ServiceAreaViewModel { Name = "Esher" }, new ServiceAreaViewModel { Name = "Cobham" } });

            return new ReviewsController(reviewsService.Object, areasService.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
                TempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>()),
            };
        }
    }
}
