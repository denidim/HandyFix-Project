namespace HandyFix.Web.Presentation
{
    using System.Collections.Generic;

    /// <summary>
    /// The four services a town's page shows: two plumbing and two handyman, picked by the town's
    /// slug. Until PROJECT_STATE.md Section 3el every town showed the first two of each category,
    /// so every area page carried the same four cards. The pick is worked out from the slug and
    /// nothing else: a town shows the same four on every visit (a search engine has to find the
    /// same page each time it comes), and a town added in the admin panel gets its own with no
    /// step for the admin. Adding a service, switching one off or moving one in its category's
    /// order changes what several towns show; that is accepted.
    /// </summary>
    public static class AreaServices
    {
        public static IReadOnlyList<T> For<T>(string areaSlug, IReadOnlyList<T> plumbing, IReadOnlyList<T> handyman)
        {
            uint number = StableNumberOf(areaSlug);

            var picked = new List<T>(4);
            picked.AddRange(TwoFrom(plumbing, number));

            // Other bits of the number, so the handyman pair does not move in step with the
            // plumbing pair.
            picked.AddRange(TwoFrom(handyman, number >> 8));

            return picked;
        }

        // Two neighbours in the category's order, going round to the first after the last.
        private static IEnumerable<T> TwoFrom<T>(IReadOnlyList<T> services, uint number)
        {
            if (services.Count <= 2)
            {
                return services;
            }

            int first = (int)(number % (uint)services.Count);
            return new[] { services[first], services[(first + 1) % services.Count] };
        }

        // FNV-1a over the slug's characters. string.GetHashCode() will not do here: .NET gives
        // the same text a different number in every run of the site, so each restart would deal
        // every town new services.
        private static uint StableNumberOf(string text)
        {
            unchecked
            {
                uint number = 2166136261;
                foreach (char character in text ?? string.Empty)
                {
                    number ^= character;
                    number *= 16777619;
                }

                return number;
            }
        }
    }
}
