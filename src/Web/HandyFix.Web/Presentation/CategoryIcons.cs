namespace HandyFix.Web.Presentation
{
    using HandyFix.Common;

    /// <summary>
    /// The icon drawn for a category's services on the pricing cards. The Pricing and service
    /// pages each had a copy, matched on the category's name; the slug is what the code fixes
    /// (PROJECT_STATE.md Section 3dg), so it is matched on that now.
    /// </summary>
    public static class CategoryIcons
    {
        public static string ForSlug(string categorySlug) => categorySlug switch
        {
            CategorySlugs.Plumbing => "water_drop",
            CategorySlugs.Handyman => "handyman",
            CategorySlugs.SmallBuildingWorks => "foundation",
            _ => "home_repair_service",
        };
    }
}
