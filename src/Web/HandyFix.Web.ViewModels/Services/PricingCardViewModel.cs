namespace HandyFix.Web.ViewModels.Services
{
    public class PricingCardViewModel
    {
        public string Name { get; set; }

        public string IconName { get; set; }

        public string ImageUrl { get; set; }

        // The picture's middle copy, or null when it has none: the card's page looks it up before
        // the page is drawn and passes it on.
        public string MediumCopyUrl { get; set; }

        public string Description { get; set; }

        public decimal BasePrice { get; set; }

        public int EstimatedDurationMinutes { get; set; }

        public bool IsQuoteBased { get; set; }

        // CSS class for the card's division colour identity (pricing-card-plumbing/
        // -handyman/-building) -- see pages/pricing.css.
        public string DivisionClass { get; set; }

        public string CtaText { get; set; }

        public string CtaUrl { get; set; }

        // The service's own page, on a card that should lead there: the name becomes a link to
        // it and the picture goes there too, while the button keeps its own address. Null leaves
        // the name as plain text and the picture going where the button goes.
        public string DetailsUrl { get; set; }
    }
}
