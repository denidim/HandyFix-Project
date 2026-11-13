namespace HandyFix.Web.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Reads a service page's list blocks as the admin form saves them: one point per line, and in
    /// "What we usually find" and "Before we arrive" an optional icon name in square brackets at
    /// the start of a line (PROJECT_STATE.md Section 3ep).
    /// </summary>
    public static class PagePoints
    {
        /// <summary>The icon of a "What we usually find" line that names none.</summary>
        public const string FindingIcon = "build";

        /// <summary>The icon of a "Before we arrive" line that names none.</summary>
        public const string BeforeIcon = "info";

        /// <summary>
        /// The icons a line may name. The site's icon font holds only the icons on
        /// tools/fonts/icon-names.txt, and one missing from it shows as its name, so a line may
        /// name only these; a test checks every one of them is in the font.
        /// </summary>
        public static readonly IReadOnlyList<string> Icons = new[]
        {
            "water_drop", "faucet", "plumbing", "construction", "build", "handyman",
            "home_repair_service", "refresh", "info", "warning", "search", "visibility",
            "inventory_2", "add_a_photo", "home", "key", "bolt", "timer", "shelves", "checklist",
            "health_and_safety", "directions_car", "emergency", "lock",
        };

        private static readonly Regex IconPrefix = new(@"^\[([^\]\s]*)\]\s*", RegexOptions.None, TimeSpan.FromSeconds(1));

        private static readonly Regex SentenceEnd = new(@"(?<=[.!?])\s+", RegexOptions.None, TimeSpan.FromSeconds(1));

        /// <summary>The block's lines, trimmed, without empty ones and without icon names.</summary>
        public static IReadOnlyList<string> LinesOf(string text) =>
            Split(text).Select(line => IconPrefix.Replace(line, string.Empty)).Where(line => line.Length > 0).ToList();

        /// <summary>
        /// The block's points, each with its icon (the default when the line names none or names
        /// one the site does not have) and its first sentence apart from the rest.
        /// </summary>
        public static IReadOnlyList<PagePoint> Of(string text, string defaultIcon)
        {
            var points = new List<PagePoint>();
            foreach (var line in Split(text))
            {
                Match match = IconPrefix.Match(line);
                var icon = match.Success && Icons.Contains(match.Groups[1].Value) ? match.Groups[1].Value : defaultIcon;
                var words = match.Success ? line.Substring(match.Length) : line;
                if (words.Length == 0)
                {
                    continue;
                }

                var sentences = SentenceEnd.Split(words, 2);
                points.Add(new PagePoint(icon, sentences[0], sentences.Length > 1 ? sentences[1] : string.Empty));
            }

            return points;
        }

        /// <summary>The names in square brackets that are not on <see cref="Icons"/>.</summary>
        public static IReadOnlyList<string> UnknownIcons(string text) =>
            Split(text)
                .Select(line => IconPrefix.Match(line))
                .Where(match => match.Success && !Icons.Contains(match.Groups[1].Value))
                .Select(match => match.Groups[1].Value)
                .Distinct()
                .ToList();

        private static IEnumerable<string> Split(string text) =>
            string.IsNullOrWhiteSpace(text)
                ? Enumerable.Empty<string>()
                : text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0);
    }
}
