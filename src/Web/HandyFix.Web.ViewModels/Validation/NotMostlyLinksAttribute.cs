namespace HandyFix.Web.ViewModels.Validation
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using System.Text.RegularExpressions;

    // Refuses a message that is web links and little else. One or two links are normal ("please
    // fit this tap: https://..."), so a message passes as long as it has no more than three and
    // still says something once they are taken out. Checked on the server only.
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class NotMostlyLinksAttribute : ValidationAttribute
    {
        private const int MaxLinks = 3;
        private const int MinOwnCharacters = 10;

        private static readonly Regex Link = new Regex(
            @"(?:https?://|www\.)\S+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        public NotMostlyLinksAttribute()
        {
            this.ErrorMessage = "Please describe it in your own words. A message that is mostly web links cannot be sent.";
        }

        public override bool IsValid(object value)
        {
            // An empty field is [Required]'s business, not this rule's.
            if (value is not string text || string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            var linkCount = Link.Matches(text).Count;
            if (linkCount == 0)
            {
                return true;
            }

            if (linkCount > MaxLinks)
            {
                return false;
            }

            var ownWords = Whitespace.Replace(Link.Replace(text, " "), " ").Trim();
            return ownWords.Length >= MinOwnCharacters;
        }
    }
}
