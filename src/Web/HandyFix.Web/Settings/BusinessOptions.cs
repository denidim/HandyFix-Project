namespace HandyFix.Web.Settings
{
    // The "Business" section of appsettings.json: what the public pages show about the business
    // on Google. A new Google Business Profile is an edit there, not a code change.
    public class BusinessOptions
    {
        public const string Section = "Business";

        // The business on Google Maps, where its reviews are read.
        public string GoogleReviewsUrl { get; set; }

        // Google's form for leaving a review.
        public string GoogleLeaveReviewUrl { get; set; }
    }
}
