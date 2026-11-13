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
            ["radiator-trv-replacement"] = new[] { "leak-repairs", "pipe-repairs", "boiler-installation-servicing" },
            ["tv-mounting"] = new[] { "shelf-installation", "wall-mounting", "minor-electrical-tasks" },
            ["furniture-assembly"] = new[] { "shelf-installation", "curtain-and-blind-fitting", "wall-mounting" },
            ["shelf-installation"] = new[] { "wall-mounting", "tv-mounting", "furniture-assembly" },
            ["full-bathroom-refurbishment"] = new[] { "radiator-trv-replacement", "plastering-ceiling-repairs" },
            ["emergency-plumbing"] = new[] { "pipe-repairs", "general-plumbing-maintenance" },
            ["shower-installation"] = new[] { "bath-shower-screen-fitting", "silicone-mastic-resealing", "bathroom-plumbing" },
            ["kitchen-plumbing"] = new[] { "washing-machine-dishwasher-install", "tap-repairs", "blocked-drains" },
            ["washing-machine-dishwasher-install"] = new[] { "kitchen-plumbing", "minor-electrical-tasks" },
            ["outside-garden-tap-installation"] = new[] { "kitchen-plumbing", "gutter-clearing" },
            ["pipe-repairs"] = new[] { "leak-repairs", "general-plumbing-maintenance", "tap-repairs" },
            ["bathroom-plumbing"] = new[] { "silicone-mastic-resealing", "bath-shower-screen-fitting", "shower-installation" },
            ["general-plumbing-maintenance"] = new[] { "tap-repairs", "toilet-repairs", "pipe-repairs" },
            ["door-trimming-shaving"] = new[] { "door-repairs", "lock-handle-replacement", "flooring-installation" },
            ["door-repairs"] = new[] { "door-trimming-shaving", "lock-handle-replacement", "painting-touch-ups" },
            ["silicone-mastic-resealing"] = new[] { "bath-shower-screen-fitting", "tap-repairs", "wall-floor-tiling" },
            ["wall-mounting"] = new[] { "shelf-installation", "tv-mounting", "curtain-and-blind-fitting" },
            ["lock-handle-replacement"] = new[] { "door-repairs", "minor-electrical-tasks" },
            ["bath-shower-screen-fitting"] = new[] { "silicone-mastic-resealing", "shower-installation", "wall-mounting" },
            ["minor-electrical-tasks"] = new[] { "tv-mounting", "plastering-ceiling-repairs", "wall-mounting" },
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
