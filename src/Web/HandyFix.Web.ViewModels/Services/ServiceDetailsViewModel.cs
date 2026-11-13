namespace HandyFix.Web.ViewModels.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;
    using HandyFix.Web.ViewModels.ServiceAreas;

    using Mapster;

    using IMapFromService = HandyFix.Services.Mapping.IMapFrom<HandyFix.Data.Models.Service>;

    public class ServiceDetailsViewModel : IMapFromService, IHaveCustomMappings
    {
        public Guid Id { get; set; }

        public string Slug { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public decimal BasePrice { get; set; }

        public int EstimatedDurationMinutes { get; set; }

        public string CategoryName { get; set; }

        public string CategorySlug { get; set; }

        public string ImageUrl { get; set; }

        public bool IsActive { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsPopular { get; set; }

        public bool IsGasSafeWork { get; set; }

        public bool IsQuoteOnly { get; set; }

        public bool IsEmergency { get; set; }

        // The page's own blocks, as the admin form saves them (PROJECT_STATE.md Section 3ep).
        public string Summary { get; set; }

        public string CommonFindings { get; set; }

        public string VisitSteps { get; set; }

        public string BeforeWeArrive { get; set; }

        public string Included { get; set; }

        public string NotIncluded { get; set; }

        // Mapped from Service.Faqs by convention. The mapper does not promise their order, so
        // whoever reads them orders them by DisplayOrder, as the town pages do.
        public IEnumerable<ServiceFaqViewModel> Faqs { get; set; }

        // "You Might Also Need" at the foot of the page: the first three of the same division,
        // for a service with no "Worth doing in the same visit" of its own.
        public IEnumerable<ServiceViewModel> RelatedServices { get; set; }

        // "Worth doing in the same visit", chosen with the service's text (SameVisitServices).
        public IEnumerable<ServiceViewModel> SameVisitServices { get; set; }

        // "Areas we cover": every town page, by name.
        public IEnumerable<ServiceAreaViewModel> Towns { get; set; }

        public void CreateMappings(TypeAdapterConfig config)
        {
            config.NewConfig<Service, ServiceDetailsViewModel>()
                .Map(dest => dest.ImageUrl, src =>
                    src.Images != null && src.Images.Any(i => !string.IsNullOrWhiteSpace(i.ImageUrl))
                        ? src.Images.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.ImageUrl)).ImageUrl
                        : (!string.IsNullOrWhiteSpace(src.Slug) ? $"/images/services/{src.Slug}-hero.webp" : "/images/hero.webp"));
        }
    }
}
