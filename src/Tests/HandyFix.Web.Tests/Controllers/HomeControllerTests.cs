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
    using HandyFix.Web.Services.Forms;
    using HandyFix.Web.ViewModels.Home;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.ViewFeatures;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging.Abstractions;

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
            Assert.Equal("About Us", controller.ViewData["Title"]);
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
        public async Task ContactPostShouldUploadThePhotosSaveTheEnquiryAndRedirect()
        {
            var inquiriesService = new Mock<IInquiriesService>();
            var controller = BuildController(inquiriesService, imageService: ImageServiceReturning("https://photos.example/inquiries/a.jpg"));
            var model = ValidContact();

            var result = await controller.Contact(model);

            Assert.Equal("Contact", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Contains("has been received", Assert.IsType<string>(controller.TempData["SuccessMessage"]));
            inquiriesService.Verify(
                s => s.CreateInquiryAsync(model, It.Is<IReadOnlyList<string>>(urls => urls.Single() == "https://photos.example/inquiries/a.jpg")),
                Times.Once);
        }

        // The same enquiry sent again, by a double click or a resend, is thanked like the first
        // and saved once. The photos are not uploaded a second time either (PROJECT_STATE
        // Section 3cb).
        [Fact]
        public async Task ContactPostShouldThankTheVisitorWithoutSavingTheSameEnquiryTwice()
        {
            var inquiriesService = new Mock<IInquiriesService>();
            inquiriesService.Setup(s => s.IsRecentDuplicateAsync(It.IsAny<ContactInputModel>())).ReturnsAsync(true);
            var imageService = ImageServiceReturning("https://photos.example/inquiries/a.jpg");
            var controller = BuildController(inquiriesService, imageService: imageService);

            var result = await controller.Contact(ValidContact());

            Assert.Equal("Contact", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Contains("has been received", Assert.IsType<string>(controller.TempData["SuccessMessage"]));
            inquiriesService.Verify(
                s => s.CreateInquiryAsync(It.IsAny<ContactInputModel>(), It.IsAny<IReadOnlyList<string>>()),
                Times.Never);
            imageService.Verify(s => s.UploadImagesAsync(It.IsAny<IEnumerable<IFormFile>>(), It.IsAny<string>()), Times.Never);
        }

        // A submission with a program's mark on it (the hidden box filled in, or sent faster than
        // anyone types) gets the very same reply as a real one, and nothing is saved, uploaded or
        // looked up for it (PROJECT_STATE Section 3cb).
        [Fact]
        public async Task ContactPostShouldAnswerAProgramLikeAPersonAndSaveNothing()
        {
            var inquiriesService = new Mock<IInquiriesService>();
            var imageService = ImageServiceReturning("https://photos.example/inquiries/a.jpg");
            var controller = BuildController(inquiriesService, imageService: imageService, formGuard: GuardAnswering(FormGuardResult.Automated));

            var result = await controller.Contact(ValidContact());

            Assert.Equal("Contact", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Equal(
                "Thank you! Your enquiry has been received. Our team will contact you shortly.",
                Assert.IsType<string>(controller.TempData["SuccessMessage"]));
            inquiriesService.VerifyNoOtherCalls();
            imageService.Verify(s => s.UploadImagesAsync(It.IsAny<IEnumerable<IFormFile>>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task JoinTeamPostShouldAnswerAProgramLikeAPersonAndSaveNothing()
        {
            var inquiriesService = new Mock<IInquiriesService>();
            var controller = BuildController(inquiriesService, formGuard: GuardAnswering(FormGuardResult.Automated));
            var model = new JoinTeamInputModel
            {
                Name = "Jane Smith",
                Email = "jane@example.com",
                PhoneNumber = "07000000000",
                Trade = "Plumbing",
                YearsExperience = 8,
                Availability = "Full-time",
            };

            var result = await controller.JoinTeam(model);

            Assert.Equal("JoinTeam", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Contains("Thanks for applying", Assert.IsType<string>(controller.TempData["SuccessMessage"]));
            inquiriesService.VerifyNoOtherCalls();
        }

        // The "are you a person" check did not pass. A person can land here (a slow connection, a
        // blocked widget), so unlike a program's mark this one is said out loud: the form comes
        // back as typed with a message and a way round it, and nothing is saved.
        [Fact]
        public async Task ContactPostShouldComeBackWithAMessageWhenThePersonCheckDidNotPass()
        {
            var inquiriesService = new Mock<IInquiriesService>();
            var controller = BuildController(
                inquiriesService,
                categoriesService: CategoriesMock("Plumbing"),
                formGuard: GuardAnswering(FormGuardResult.ChallengeFailed));
            var model = ValidContact();

            var result = await controller.Contact(model);

            Assert.Same(model, Assert.IsType<ViewResult>(result).Model);
            var message = controller.ModelState[string.Empty].Errors.Single().ErrorMessage;
            Assert.Contains("could not confirm that you are a person", message);
            Assert.Contains(HandyFix.Common.GlobalConstants.BusinessPhone, message);
            Assert.Null(controller.TempData["SuccessMessage"]);
            inquiriesService.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task JoinTeamPostShouldComeBackWithAMessageWhenThePersonCheckDidNotPass()
        {
            var inquiriesService = new Mock<IInquiriesService>();
            var controller = BuildController(inquiriesService, formGuard: GuardAnswering(FormGuardResult.ChallengeFailed));
            var model = new JoinTeamInputModel
            {
                Name = "Jane Smith",
                Email = "jane@example.com",
                PhoneNumber = "07000000000",
                Trade = "Plumbing",
                YearsExperience = 8,
                Availability = "Full-time",
            };

            var result = await controller.JoinTeam(model);

            Assert.Same(model, Assert.IsType<ViewResult>(result).Model);
            Assert.Contains("could not confirm that you are a person", controller.ModelState[string.Empty].Errors.Single().ErrorMessage);
            Assert.Equal("Join Our Team - Careers", controller.ViewData["Title"]);
            inquiriesService.VerifyNoOtherCalls();
        }

        // Each form tells the guard which form it is: that name is what its Turnstile token is
        // good for.
        [Fact]
        public async Task TheHomeFormsShouldEachTellTheGuardWhichFormTheyAre()
        {
            Mock<IFormGuard> formGuard = GuardAnswering(FormGuardResult.Passed);
            var controller = BuildController(formGuard: formGuard);

            await controller.Contact(ValidContact());
            await controller.JoinTeam(new JoinTeamInputModel { Name = "Jane Smith", Email = "jane@example.com", PhoneNumber = "07000000000", Trade = "Plumbing", YearsExperience = 8, Availability = "Full-time" });

            formGuard.Verify(g => g.CheckAsync(It.IsAny<HttpContext>(), "contact"), Times.Once);
            formGuard.Verify(g => g.CheckAsync(It.IsAny<HttpContext>(), "join-team"), Times.Once);
        }

        // The form's own rules come first: a submission they refuse comes back with its messages
        // whoever sent it, and the guard is not asked.
        [Fact]
        public async Task ContactPostShouldShowValidationMessagesBeforeTheGuardIsAsked()
        {
            Mock<IFormGuard> formGuard = GuardAnswering(FormGuardResult.Automated);
            var controller = BuildController(categoriesService: CategoriesMock("Plumbing"), formGuard: formGuard);
            controller.ModelState.AddModelError(nameof(ContactInputModel.Email), "Please enter a full email address, for example name@example.com.");

            var result = await controller.Contact(ValidContact());

            Assert.IsType<ViewResult>(result);
            Assert.Null(controller.TempData["SuccessMessage"]);
            formGuard.Verify(g => g.CheckAsync(It.IsAny<HttpContext>(), It.IsAny<string>()), Times.Never);
        }

        // Too many photos, one too large, a file that is not a picture: the visitor can put that
        // right, so the form comes back with the reason. It used to end in an error page with
        // everything they had typed gone (PROJECT_STATE Section 3cb).
        [Fact]
        public async Task ContactPostShouldComeBackWithTheReasonWhenAPhotoIsNotOneWeTake()
        {
            var inquiriesService = new Mock<IInquiriesService>();
            var imageService = new Mock<IImageService>();
            imageService
                .Setup(s => s.UploadImagesAsync(It.IsAny<IEnumerable<IFormFile>>(), It.IsAny<string>()))
                .ThrowsAsync(new ImageUploadValidationException("File \"notes.pdf\" has an unsupported type \"application/pdf\". Allowed types: JPEG, PNG, WEBP."));
            var controller = BuildController(inquiriesService, categoriesService: CategoriesMock("Plumbing"), imageService: imageService);
            var model = ValidContact();

            var result = await controller.Contact(model);

            Assert.Same(model, Assert.IsType<ViewResult>(result).Model);
            Assert.Contains("unsupported type", controller.ModelState[nameof(ContactInputModel.Images)].Errors.Single().ErrorMessage);
            Assert.NotNull(controller.ViewData["ContactCategories"]);
            inquiriesService.Verify(
                s => s.CreateInquiryAsync(It.IsAny<ContactInputModel>(), It.IsAny<IReadOnlyList<string>>()),
                Times.Never);
        }

        // Storage out of reach is not the visitor's to fix. The enquiry is worth more than its
        // photos, so it is saved without them and both sides are told.
        [Fact]
        public async Task ContactPostShouldSaveTheEnquiryWithoutItsPhotosWhenStorageCannotBeReached()
        {
            var inquiriesService = new Mock<IInquiriesService>();
            var imageService = new Mock<IImageService>();
            imageService
                .Setup(s => s.UploadImagesAsync(It.IsAny<IEnumerable<IFormFile>>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Cloudflare R2 is not fully configured. Missing one or more required settings in CloudflareR2 section."));
            var controller = BuildController(inquiriesService, imageService: imageService);
            var model = ValidContact();
            model.Images = new List<IFormFile> { PhotoOf(2048), PhotoOf(4096), PhotoOf(0) };

            var result = await controller.Contact(model);

            Assert.Equal("Contact", Assert.IsType<RedirectToActionResult>(result).ActionName);
            inquiriesService.Verify(
                s => s.CreateInquiryAsync(
                    It.Is<ContactInputModel>(c => c.PhotosNotSaved == 2),
                    It.Is<IReadOnlyList<string>>(urls => urls.Count == 0)),
                Times.Once);

            var message = Assert.IsType<string>(controller.TempData["SuccessMessage"]);
            Assert.Contains("has been received", message);
            Assert.Contains("photos could not be uploaded", message);
            Assert.DoesNotContain("Cloudflare", message);
        }

        [Fact]
        public async Task JoinTeamPostShouldThankTheApplicantWithoutSavingTheSameApplicationTwice()
        {
            var inquiriesService = new Mock<IInquiriesService>();
            inquiriesService
                .Setup(s => s.IsRecentDuplicateAsync(It.Is<ContactInputModel>(c => c.Message.StartsWith(JoinTeamInputModel.MessagePrefix))))
                .ReturnsAsync(true);
            var controller = BuildController(inquiriesService);
            var model = new JoinTeamInputModel
            {
                Name = "Jane Smith",
                Email = "jane@example.com",
                PhoneNumber = "07000000000",
                Trade = "Plumbing",
                YearsExperience = 8,
                Availability = "Full-time",
            };

            var result = await controller.JoinTeam(model);

            Assert.Equal("JoinTeam", Assert.IsType<RedirectToActionResult>(result).ActionName);
            Assert.Contains("Thanks for applying", Assert.IsType<string>(controller.TempData["SuccessMessage"]));
            inquiriesService.Verify(
                s => s.CreateInquiryAsync(It.IsAny<ContactInputModel>(), It.IsAny<IReadOnlyList<string>>()),
                Times.Never);
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

        [Fact]
        public async Task ContactGetWithServiceAndDateShouldAskAboutThatDay()
        {
            var servicesService = new Mock<IServicesService>();
            servicesService
                .Setup(s => s.GetAllAsync<ServiceViewModel>(It.IsAny<bool>()))
                .ReturnsAsync(new List<ServiceViewModel>
                {
                    new ServiceViewModel { Name = "General Plumbing Maintenance", CategoryName = "Plumbing" },
                });
            var controller = BuildController(servicesService: servicesService, categoriesService: CategoriesMock("Handyman", "Plumbing"));

            var result = await controller.Contact("General Plumbing Maintenance", "2026-09-27");

            var model = Assert.IsType<ContactInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal("Plumbing", model.Category);
            Assert.StartsWith("Hi, I'd like to book General Plumbing Maintenance on Sun 27 Sep, but I couldn't find a time", model.Message);
        }

        [Theory]
        [InlineData("not-a-date")]
        [InlineData("27/09/2026")]
        [InlineData("2026-02-30")]
        public async Task ContactGetWithUnreadableDateShouldFallBackToTheQuoteMessage(string date)
        {
            var servicesService = new Mock<IServicesService>();
            servicesService
                .Setup(s => s.GetAllAsync<ServiceViewModel>(It.IsAny<bool>()))
                .ReturnsAsync(new List<ServiceViewModel>
                {
                    new ServiceViewModel { Name = "Tap Repairs", CategoryName = "Plumbing" },
                });
            var controller = BuildController(servicesService: servicesService, categoriesService: CategoriesMock("Handyman", "Plumbing"));

            var result = await controller.Contact("Tap Repairs", date);

            var model = Assert.IsType<ContactInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.StartsWith("Hi, I would like to request a quote / survey for: Tap Repairs.", model.Message);
        }

        [Theory]
        [InlineData("small-building-works", "Small Building & Refurbishments")]
        [InlineData("SMALL-BUILDING-WORKS", "Small Building & Refurbishments")]
        [InlineData("no-such-category", null)]
        public async Task ContactGetWithCategorySlugShouldPreselectThatCategory(string slug, string expected)
        {
            // The home booking widget's quote button sends the slug when Small Building is chosen
            // there (PROJECT_STATE Section 3bz).
            var categoriesService = new Mock<ICategoriesService>();
            categoriesService
                .Setup(c => c.GetAllAsync<CategoryViewModel>())
                .ReturnsAsync(new List<CategoryViewModel>
                {
                    new CategoryViewModel { Name = "Plumbing", Slug = "plumbing" },
                    new CategoryViewModel { Name = "Small Building & Refurbishments", Slug = "small-building-works" },
                });
            var controller = BuildController(categoriesService: categoriesService);

            var result = await controller.Contact(categorySlug: slug);

            var model = Assert.IsType<ContactInputModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(expected, model.Category);
            Assert.Null(model.Message);
        }

        [Fact]
        public async Task IndexShouldShowTheServicesMarkedPopularInListOrder()
        {
            // "Popular" was the first four services in the alphabet. It is now the four an admin
            // ticked (PROJECT_STATE Section 3ca); a fifth ticked one does not fit the grid.
            var controller = BuildController(servicesService: ServicesMock(
                ("Bath Screen Fitting", false),
                ("Emergency Plumbing", true),
                ("Full Bathroom Refurbishment", true),
                ("Furniture Assembly", true),
                ("Gutter Clearing", false),
                ("Kitchen Fitting", true),
                ("Tap Repairs", true)));

            var result = await controller.Index();

            var model = Assert.IsType<HomeIndexViewModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(
                new[] { "Emergency Plumbing", "Full Bathroom Refurbishment", "Furniture Assembly", "Kitchen Fitting" },
                model.PopularServices.Select(s => s.Name));
        }

        [Theory]
        [InlineData("Gutter Clearing", new[] { "Gutter Clearing", "Bath Screen Fitting", "Blocked Drains", "Door Repairs" })]
        [InlineData(null, new[] { "Bath Screen Fitting", "Blocked Drains", "Door Repairs", "Gutter Clearing" })]
        public async Task IndexShouldFillTheGridFromTheListWhenFewerThanFourAreMarked(string popular, string[] expected)
        {
            // The grid holds four. One ticked service, or none, must not leave it part-empty.
            var names = new[] { "Bath Screen Fitting", "Blocked Drains", "Door Repairs", "Gutter Clearing", "Tap Repairs" };
            var controller = BuildController(servicesService: ServicesMock(names.Select(n => (n, n == popular)).ToArray()));

            var result = await controller.Index();

            var model = Assert.IsType<HomeIndexViewModel>(Assert.IsType<ViewResult>(result).Model);
            Assert.Equal(expected, model.PopularServices.Select(s => s.Name));
        }

        private static Mock<IServicesService> ServicesMock(params (string Name, bool IsPopular)[] services)
        {
            var servicesService = new Mock<IServicesService>();
            servicesService
                .Setup(s => s.GetAllAsync<ServiceViewModel>(It.IsAny<bool>()))
                .ReturnsAsync(services.Select(s => new ServiceViewModel { Name = s.Name, IsPopular = s.IsPopular }).ToList());
            return servicesService;
        }

        private static ContactInputModel ValidContact() => new ContactInputModel
        {
            Name = "Jane Doe",
            Email = "jane@example.com",
            PhoneNumber = "07700 900123",
            Message = "Kitchen tap is dripping.",
            Category = "Plumbing",
        };

        private static Mock<IFormGuard> GuardAnswering(FormGuardResult result)
        {
            var formGuard = new Mock<IFormGuard>();
            formGuard.Setup(g => g.CheckAsync(It.IsAny<HttpContext>(), It.IsAny<string>())).ReturnsAsync(result);
            return formGuard;
        }

        private static IFormFile PhotoOf(long bytes)
        {
            var photo = new Mock<IFormFile>();
            photo.SetupGet(f => f.Length).Returns(bytes);
            return photo.Object;
        }

        private static Mock<IImageService> ImageServiceReturning(params string[] urls)
        {
            var imageService = new Mock<IImageService>();
            imageService
                .Setup(s => s.UploadImagesAsync(It.IsAny<IEnumerable<IFormFile>>(), It.IsAny<string>()))
                .ReturnsAsync(urls.ToList());
            return imageService;
        }

        private static HomeController BuildController(
            Mock<IInquiriesService> inquiriesService = null,
            Mock<IServicesService> servicesService = null,
            Mock<ICategoriesService> categoriesService = null,
            Mock<IImageService> imageService = null,
            Mock<IFormGuard> formGuard = null)
        {
            var reviewsService = new Mock<IReviewsService>();

            var controller = new HomeController(
                reviewsService.Object,
                (inquiriesService ?? new Mock<IInquiriesService>()).Object,
                (servicesService ?? new Mock<IServicesService>()).Object,
                (categoriesService ?? new Mock<ICategoriesService>()).Object,
                (imageService ?? ImageServiceReturning()).Object,
                (formGuard ?? new Mock<IFormGuard>()).Object,
                new ConfigurationBuilder().Build(),
                NullLogger<HomeController>.Instance);

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
