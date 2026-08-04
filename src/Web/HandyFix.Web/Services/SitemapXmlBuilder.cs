namespace HandyFix.Web.Services
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;

    /// <summary>
    /// Pure XML formatting, deliberately kept out of SeoController - gathering the actual URLs
    /// stays there since it depends on IUrlHelper, which has no business being in the service
    /// layer.
    /// </summary>
    public static class SitemapXmlBuilder
    {
        private static readonly XNamespace SitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

        public static string Build(IEnumerable<string> urls)
        {
            var xml = new XElement(
                SitemapNamespace + "urlset",
                urls
                    .Where(u => !string.IsNullOrEmpty(u))
                    .Distinct()
                    .Select(u => new XElement(SitemapNamespace + "url", new XElement(SitemapNamespace + "loc", u))));

            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" + xml;
        }
    }
}
