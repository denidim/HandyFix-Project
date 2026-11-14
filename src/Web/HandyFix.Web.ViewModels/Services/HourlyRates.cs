namespace HandyFix.Web.ViewModels.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using HandyFix.Common;

    // The two hourly rates a page quotes in its title, its description or its text: the lowest
    // price among each division's active services, the same number the Pricing page's rate cards
    // show (CategoryViewModel.BasePrice). They are read here and never typed into a text, so a
    // price changed in the admin changes every line that quotes it (the SEO master plan's item 7).
    public class HourlyRates
    {
        public decimal Plumbing { get; private set; }

        public decimal Handyman { get; private set; }

        // For a line that says "from": the lower of the two.
        public decimal Lowest => Math.Min(this.Plumbing, this.Handyman);

        public static HourlyRates Of(IEnumerable<CategoryViewModel> categories)
        {
            var list = (categories ?? Enumerable.Empty<CategoryViewModel>()).ToList();

            return new HourlyRates
            {
                Plumbing = list.FirstOrDefault(c => c.Slug == CategorySlugs.Plumbing)?.BasePrice ?? 0m,
                Handyman = list.FirstOrDefault(c => c.Slug == CategorySlugs.Handyman)?.BasePrice ?? 0m,
            };
        }
    }
}
