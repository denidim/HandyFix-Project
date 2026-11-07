namespace HandyFix.Web.Settings
{
    // The "Business" section of appsettings.json: what the public pages show about the business
    // on Google. A new Google Business Profile is an edit there, not a code change.
    public class BusinessOptions
    {
        public const string Section = "Business";

        // Off until the Google Business Profile import ships (PROJECT_STATE.md Section 3n). Off,
        // the pages do not even fetch the reviews, so a stray approved one cannot show.
        public bool ShowOnSiteReviews { get; set; }

        // The business on Google Maps, where its reviews are read.
        public string GoogleReviewsUrl { get; set; }

        // Google's form for leaving a review.
        public string GoogleLeaveReviewUrl { get; set; }
    }
}
