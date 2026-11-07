namespace HandyFix.Web.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    /// <summary>
    /// A service's description as the paragraphs the service page draws: a blank line starts a
    /// new one. Until PROJECT_STATE.md Section 3do the page put the whole text in one paragraph,
    /// and HTML joins every line of it, so a text written in parts read as one block. A single
    /// line break stays inside its paragraph, where HTML shows it as a space, as before.
    /// </summary>
    public static class Paragraphs
    {
        private static readonly Regex BlankLine = new(@"\r?\n[ \t]*\r?\n", RegexOptions.None, TimeSpan.FromSeconds(1));

        public static IReadOnlyList<string> Of(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            return BlankLine.Split(text)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToList();
        }
    }
}
