namespace HandyFix.Web.ViewModels.Services
{
    public class PricingCardViewModel
    {
        public string Name { get; set; }

        public string IconName { get; set; }

        public string ImageUrl { get; set; }

        public string Description { get; set; }

        public decimal BasePrice { get; set; }

        public int EstimatedDurationMinutes { get; set; }

        public bool IsQuoteBased { get; set; }

        // CSS class for the card's division colour identity (pricing-card-plumbing/
        // -handyman/-building) -- see pages/pricing.css.
        public string DivisionClass { get; set; }

        public string CtaText { get; set; }

        public string CtaUrl { get; set; }
    }
}
