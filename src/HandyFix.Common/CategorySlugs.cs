namespace HandyFix.Common
{
    // The three service categories' slugs: the last part of each category page's address, and
    // how the code tells the divisions apart. ServiceCategoriesSeeder writes them over the
    // database's own at every start, and there is no admin page for categories, so these are
    // the slugs the database holds.
    public static class CategorySlugs
    {
        public const string Plumbing = "plumbing";

        public const string Handyman = "handyman";

        // Quote only: these jobs are not booked online.
        public const string SmallBuildingWorks = "small-building-works";
    }
}
