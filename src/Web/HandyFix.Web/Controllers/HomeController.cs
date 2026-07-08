namespace HandyFix.Web.Controllers
{
    using System.Diagnostics;
    using System.Threading.Tasks;

    using HandyFix.Services.Data.Reviews;
    using HandyFix.Web.ViewModels;
    using HandyFix.Web.ViewModels.Reviews;

    using Microsoft.AspNetCore.Mvc;

    public class HomeController : BaseController
    {
        private readonly IReviewsService reviewsService;

        public HomeController(IReviewsService reviewsService)
        {
            this.reviewsService = reviewsService;
        }

        public IActionResult Index()
        {
            return this.View();
        }

        [Route("Reviews")]
        public async Task<IActionResult> Reviews()
        {
            var approvedReviews = await this.reviewsService.GetLatestApprovedAsync<ReviewViewModel>(50);
            var model = new ReviewsListViewModel
            {
                Reviews = approvedReviews,
                NewReview = new ReviewInputModel(),
            };

            this.ViewData["Title"] = "Customer Reviews - HandyFix London";
            return this.View(model);
        }

        public IActionResult Privacy()
        {
            return this.View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return this.View(
                new ErrorViewModel { RequestId = Activity.Current?.Id ?? this.HttpContext.TraceIdentifier });
        }
    }
}
