namespace HandyFix.Web.ViewModels.Services
{
    using System;
    using System.Linq;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    using Mapster;

    using IMapFromService = HandyFix.Services.Mapping.IMapFrom<HandyFix.Data.Models.Service>;

    public class ServiceViewModel : IMapFromService, IHaveCustomMappings
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        // The service's card text on a town page, when it has one (PROJECT_STATE.md Section 3ep).
        public string Summary { get; set; }

        public decimal BasePrice { get; set; }

        public int EstimatedDurationMinutes { get; set; }

        public string CategoryName { get; set; }

        public string CategorySlug { get; set; }

        public string Slug { get; set; }

        public string ImageUrl { get; set; }

        public bool IsActive { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsPopular { get; set; }

        public bool IsGasSafeWork { get; set; }

        public bool IsQuoteOnly { get; set; }

        public bool IsEmergency { get; set; }

        /// <summary>
        /// Set by the controller of a page that shows the picture small, not mapped: the address of
        /// its small copy, or null when it has none. Looked up before the page is drawn
        /// (PROJECT_STATE.md Section 3dj).
        /// </summary>
        public string SmallCopyUrl { get; set; }

        /// <summary>
        /// Set by the controller of a page that shows the picture on a card, not mapped: the
        /// address of its middle copy, or null when it has none.
        /// </summary>
        public string MediumCopyUrl { get; set; }

        public void CreateMappings(TypeAdapterConfig config)
        {
            config.NewConfig<Service, ServiceViewModel>()
                .Ignore(dest => dest.SmallCopyUrl)
                .Ignore(dest => dest.MediumCopyUrl)
                .Map(dest => dest.ImageUrl, src =>
                    src.Images != null && src.Images.Any(i => !string.IsNullOrWhiteSpace(i.ImageUrl))
                        ? src.Images.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.ImageUrl)).ImageUrl
                        : (!string.IsNullOrWhiteSpace(src.Slug) ? $"/images/services/{src.Slug}-hero.webp" : "/images/hero.webp"));
        }
    }
}
