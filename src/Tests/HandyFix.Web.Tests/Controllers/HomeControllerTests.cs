namespace HandyFix.Web.Tests.Controllers
{
    using System;
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
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
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

        [Fact]
        public void JoinTeamGetShouldReturnViewWithEmptyApplicationModel()
        {
            var controller = BuildController();

            var result = controller.JoinTeam();

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.IsType<JoinTeamInputModel>(viewResult.Model);
            Assert.Equal("Join Our Team - Careers", controller.ViewData["Title"]);
        }

        [Fact]
        public async Task JoinTeamPostShouldRedisplayFormWithoutSavingWhenModelIsInvalid()
        {
            var inquiriesService = new Mock<IInquiriesService>();
            var controller = BuildController(inquiriesService);
            controller.ModelState.AddModelError(nameof(JoinTeamInputModel.Trade), "Please choose your main trade.");
            var model = new JoinTeamInputModel { Name = "Jane Smith" };

            var result = await controller.JoinTeam(model);

            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Same(model, viewResult.Model);
            inquiriesService.Verify(
                s => s.CreateInquiryAsync(It.IsAny<ContactInputModel>(), It.IsAny<IReadOnlyList<string>>()),
                Times.Never);
        }

        [Fact]
        public async Task JoinTeamPostShouldSaveApplicationAsPrefixedEnquiryAndRedirectWhenModelIsValid()
        {
            var inquiriesService = new Mock<IInquiriesService>();
            var controller = BuildController(inquiriesService);
            var model = new JoinTeamInputModel
            {
                Name = "Jane Smith",
                Email = "jane@example.com",
                PhoneNumber = "07000000000",
                Trade = "Plumbing",
                YearsExperience = 8,
                Availability = "Full-time",
                HasOwnTools = true,
                HasOwnTransport = false,
                AboutYou = "NVQ Level 3 qualified.",
            };

            var result = await controller.JoinTeam(model);

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("JoinTeam", redirect.ActionName);
            inquiriesService.Verify(
                s => s.CreateInquiryAsync(
                    It.Is<ContactInputModel>(c =>
                        c.Name == "Jane Smith" &&
                        c.Email == "jane@example.com" &&
                        c.PhoneNumber == "07000000000" &&
                        c.Message.StartsWith(JoinTeamInputModel.MessagePrefix) &&
                        c.Message.Contains("Trade: Plumbing") &&
                        c.Message.Contains("Years of experience: 8") &&
                        c.Message.Contains("Own tools: Yes") &&
                        c.Message.Contains("Own transport: No") &&
                        c.Message.Contains("NVQ Level 3 qualified.")),
                    It.Is<IReadOnlyList<string>>(urls => urls.Count == 0)),
                Times.Once);
        }

        [Fact]
        public async Task ContactGetShouldPreselectTheCategoryOfTheRequestedService()
        {
            var servicesService = new Mock<IServicesService>();
            servicesService
                .Setup(s => s.GetAllAsync<ServiceViewModel>(It.IsAny<bool>()))
                .ReturnsAsync(new List<ServiceViewModel>
                {
                    new ServiceViewModel { Name = "Tap Repairs", CategoryName = "Plumbing" },
                    new ServiceViewModel { Name = "Wall & Floor Tiling", CategoryName = "Small Building & Refurbishments" },
                });
            var categoriesService = CategoriesMock("Handyman", "Plumbing", "Small Building & Refurbishments");
            var controller = BuildController(servicesService: servicesService, categoriesService: categoriesService);

            var result = await controller.Contact("Wall & Floor Tiling");

            var model = Assert.IsType<ContactInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal("Small Building & Refurbishments", model.Category);
            Assert.StartsWith("Hi, I would like to request a quote / survey for: Wall & Floor Tiling.", model.Message);
            Assert.Equal(
                new[] { "Handyman", "Plumbing", "Small Building & Refurbishments" },
                (IEnumerable<string>)controller.ViewData["ContactCategories"]);
        }

        [Fact]
        public async Task ContactGetWithoutServiceShouldLeaveCategoryUnselected()
        {
            var controller = BuildController(categoriesService: CategoriesMock("Handyman", "Plumbing"));

            var result = await controller.Contact();

            var model = Assert.IsType<ContactInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Null(model.Category);
            Assert.Null(model.Message);
        }

        private static HomeController BuildController(
            Mock<IInquiriesService> inquiriesService = null,
            Mock<IServicesService> servicesService = null,
            Mock<ICategoriesService> categoriesService = null)
        {
            var reviewsService = new Mock<IReviewsService>();
            var imageService = new Mock<IImageService>();
            var configuration = new Mock<IConfiguration>();

            var controller = new HomeController(
                reviewsService.Object,
                (inquiriesService ?? new Mock<IInquiriesService>()).Object,
                (servicesService ?? new Mock<IServicesService>()).Object,
                (categoriesService ?? new Mock<ICategoriesService>()).Object,
                imageService.Object,
                configuration.Object);

            var httpContext = new DefaultHttpContext();
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
            };
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

            return controller;
        }

        private static Mock<ICategoriesService> CategoriesMock(params string[] names)
        {
            var categoriesService = new Mock<ICategoriesService>();
            categoriesService
                .Setup(c => c.GetAllAsync<CategoryViewModel>())
                .ReturnsAsync(names.Select(n => new CategoryViewModel { Name = n }).ToList());
            return categoriesService;
        }
    }
}
