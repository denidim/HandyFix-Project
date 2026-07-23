namespace HandyFix.Web.ViewModels.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    public class CategoryViewModel : IMapFrom<ServiceCategory>, IHaveCustomMappings
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string Slug => this.Name.Replace(" ", "-").ToLower();

        public decimal BasePrice { get; set; }

        public virtual ICollection<ServiceViewModel> Services { get; set; }

        public void CreateMappings(Mapster.TypeAdapterConfig config)
        {
            config.NewConfig<ServiceCategory, CategoryViewModel>()
                .Map(dest => dest.BasePrice, src => src.Services.Any() ? src.Services.Min(s => s.BasePrice) : 0m);
        }
    }
}
