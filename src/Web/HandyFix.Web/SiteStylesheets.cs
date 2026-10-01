namespace HandyFix.Web
{
    /// <summary>
    /// The public site's stylesheets, in the order they are combined into one file.
    /// </summary>
    /// <remarks>
    /// Order is the cascade: on equal specificity a later file overrides an earlier one, so a
    /// new stylesheet goes at the end of its group, not at the end of the list. A file under
    /// wwwroot/css that is not listed here is not loaded at all; a test checks none is left out.
    /// <c>pages/admin.css</c> is not part of this: the admin layout links it by itself.
    /// </remarks>
    public static class SiteStylesheets
    {
        /// <summary>
        /// The address the combined stylesheet is served at.
        /// </summary>
        public const string BundleRoute = "/css/site.min.css";

        /// <summary>
        /// Source files, relative to wwwroot.
        /// </summary>
        public static readonly string[] SourceFiles =
        {
            // Web fonts: @import lines, which only work at the very top.
            "/css/fonts.css",

            "/css/base/variables.css",
            "/css/base/reset.css",
            "/css/base/typography.css",
            "/css/base/layout.css",
            "/css/base/utilities.css",

            "/css/components/navbar.css",
            "/css/components/footer.css",
            "/css/components/logo.css",
            "/css/components/buttons.css",
            "/css/components/cards.css",
            "/css/components/forms.css",
            "/css/components/image-upload.css",

            "/css/pages/home.css",
            "/css/pages/services.css",
            "/css/pages/pricing.css",
            "/css/pages/service-details.css",
            "/css/pages/booking.css",
            "/css/pages/service-category.css",
            "/css/pages/areas.css",
            "/css/pages/payment.css",
            "/css/pages/auth.css",
            "/css/pages/pages-info.css",

            // Paper styles override everything above.
            "/css/print.css",

            // Icon font defaults, last as they were in the old site.css.
            "/css/icons.css",
        };
    }
}
