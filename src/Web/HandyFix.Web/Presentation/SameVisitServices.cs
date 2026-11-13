namespace HandyFix.Web.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using HandyFix.Web.ViewModels.Services;

    /// <summary>
    /// "Worth doing in the same visit" on a service page: two to four other services, chosen with
    /// the page's text (PROJECT_STATE.md Section 3ep), kept here as the towns' service cards are
    /// (AreaServices), so the same page always links to the same services. A service not listed
    /// here, or renamed so its address changed, keeps the old "You Might Also Need" cards instead.
    /// </summary>
    public static class SameVisitServices
    {
        private static readonly Dictionary<string, string[]> Choices = new(StringComparer.OrdinalIgnoreCase)
        {
            ["tap-repairs"] = new[] { "toilet-repairs", "silicone-mastic-resealing", "radiator-trv-replacement" },
            ["toilet-repairs"] = new[] { "tap-repairs", "silicone-mastic-resealing", "blocked-drains" },
            ["leak-repairs"] = new[] { "pipe-repairs", "silicone-mastic-resealing", "tap-repairs" },
            ["blocked-drains"] = new[] { "toilet-repairs", "kitchen-plumbing", "bathroom-plumbing" },
            ["boiler-installation-servicing"] = new[] { "radiator-trv-replacement", "pipe-repairs" },
        };

        /// <summary>
        /// The chosen services that are on offer, in the chosen order; none when the service has
        /// no choice of its own.
        /// </summary>
        public static IReadOnlyList<ServiceViewModel> For(string slug, IEnumerable<ServiceViewModel> activeServices)
        {
            if (slug == null || !Choices.TryGetValue(slug, out var chosen))
            {
                return Array.Empty<ServiceViewModel>();
            }

            var bySlug = activeServices.ToDictionary(s => s.Slug, StringComparer.OrdinalIgnoreCase);
            return chosen
                .Where(bySlug.ContainsKey)
                .Select(s => bySlug[s])
                .ToList();
        }
    }
}
