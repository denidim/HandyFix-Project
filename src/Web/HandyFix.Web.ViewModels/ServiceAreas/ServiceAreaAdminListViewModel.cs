namespace HandyFix.Web.ViewModels.ServiceAreas
{
    using System;
    using System.Linq;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    using Mapster;

    using IMapFromServiceArea = HandyFix.Services.Mapping.IMapFrom<HandyFix.Data.Models.ServiceArea>;

    public class ServiceAreaAdminListViewModel : IMapFromServiceArea, IHaveCustomMappings
    {
        public Guid Id { get; set; }

        public string Slug { get; set; }

        public string Name { get; set; }

        public string Region { get; set; }

        public int DriveTimeMinutes { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsFeatured { get; set; }

        public int FaqCount { get; set; }

        public void CreateMappings(TypeAdapterConfig config)
        {
            config.NewConfig<ServiceArea, ServiceAreaAdminListViewModel>()
                .Map(dest => dest.FaqCount, src => src.Faqs.Count(f => !f.IsDeleted));
        }
    }
}
