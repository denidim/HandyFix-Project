namespace HandyFix.Web.ViewModels.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

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

        public IEnumerable<ServiceViewModel> RelatedServices { get; set; }

        public void CreateMappings(TypeAdapterConfig config)
        {
            config.NewConfig<Service, ServiceDetailsViewModel>()
                .Map(dest => dest.ImageUrl, src =>
                    src.Images != null && src.Images.Any(i => !string.IsNullOrWhiteSpace(i.ImageUrl))
                        ? src.Images.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.ImageUrl)).ImageUrl
                        : (!string.IsNullOrWhiteSpace(src.Slug) ? $"/images/services/{src.Slug}-hero.webp" : "/images/hero.png"));
        }
    }
}
