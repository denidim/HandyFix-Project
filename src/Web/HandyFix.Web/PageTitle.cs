namespace HandyFix.Web
{
    using HandyFix.Common;

    // The title a public page shows in the tab, as the blue line in Google and on a share card.
    // Google names the site itself on the row above a result, from the home page's WebSite record,
    // so a page ends its title with what the visitor can do there, not with the firm's name a
    // second time (the SEO master plan's item 7). A page names its own ending through
    // SetPageMeta. One that names none keeps the firm's name: the legal pages and the pages
    // Google never shows (not found, an invoice, a confirmed booking, the login).
    public static class PageTitle
    {
        // A page where a slot can be booked online.
        public const string BookOnline = "Book Online";

        // The emergency page: an emergency is taken by phone and WhatsApp, never booked as a slot.
        public const string CallOrWhatsApp = "Call or WhatsApp";

        // The title stands as it is: it already says what the page offers (the home page, a
        // free survey).
        public const string NoEnding = "";

        private const string Separator = " - ";

        public static string Full(string title, string ending)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return GlobalConstants.SystemName;
            }

            ending ??= GlobalConstants.SystemName;
            return ending.Length == 0 ? title : title + Separator + ending;
        }
    }
}
