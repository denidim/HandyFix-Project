namespace HandyFix.Web.Controllers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    using HandyFix.Services.Data.Categories;
    using HandyFix.Services.Data.Services;
    using HandyFix.Web.ViewModels.Services;

    using Microsoft.AspNetCore.Mvc;

    public class SeoController : BaseController
    {
        private static readonly XNamespace SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

        private readonly ICategoriesService categoriesService;
        private readonly IServicesService servicesService;

        public SeoController(ICategoriesService categoriesService, IServicesService servicesService)
        {
            this.categoriesService = categoriesService;
            this.servicesService = servicesService;
        }

        [Route("sitemap.xml")]
        public async Task<IActionResult> Sitemap()
        {
            var protocol = this.Request.Scheme;

            var urls = new List<string>
            {
                this.Url.Action("Index", "Home", null, protocol),
                this.Url.Action("Index", "Services", null, protocol),
                this.Url.Action("Pricing", "Services", null, protocol),
                this.Url.Action("Index", "Booking", null, protocol),
                this.Url.Action("Contact", "Home", null, protocol),
                this.Url.Action("About", "Home", null, protocol),
                this.Url.Action("FAQ", "Home", null, protocol),
                this.Url.Action("ServiceAreas", "Home", null, protocol),
                this.Url.Action("Reviews", "Home", null, protocol),
                this.Url.Action("Privacy", "Home", null, protocol),
                this.Url.Action("Terms", "Home", null, protocol),
                this.Url.Action("CookiePolicy", "Home", null, protocol),
            };

            var categories = await this.categoriesService.GetAllAsync<CategoryViewModel>();
            foreach (var category in categories)
            {
                urls.Add(this.Url.RouteUrl("ServiceCategory", new { categorySlug = category.Slug }, protocol));
            }

            var services = await this.servicesService.GetAllAsync<ServiceViewModel>();
            foreach (var service in services)
            {
                urls.Add(this.Url.RouteUrl("ServiceDetails", new { categorySlug = service.CategorySlug, serviceSlug = service.Slug }, protocol));
            }

            var xml = new XElement(
                SitemapNamespace + "urlset",
                urls
                    .Where(u => !string.IsNullOrEmpty(u))
                    .Distinct()
                    .Select(u => new XElement(SitemapNamespace + "url", new XElement(SitemapNamespace + "loc", u))));

            var content = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + xml;

            return this.Content(content, "application/xml", Encoding.UTF8);
        }
    }
}
