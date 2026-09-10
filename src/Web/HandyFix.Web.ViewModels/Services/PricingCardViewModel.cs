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

        public string CtaText { get; set; }

        public string CtaUrl { get; set; }
    }
}
