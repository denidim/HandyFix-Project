namespace HandyFix.Web.ViewModels.Administration.Services
{
    using System;
    using System.Linq;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    using Mapster;

    using IMapFromService = HandyFix.Services.Mapping.IMapFrom<HandyFix.Data.Models.Service>;

    // A row of the admin's list of services. A model of its own, not the public pages'
    // ServiceViewModel, so that counting a service's jobs costs the public lists nothing.
    public class ServiceAdminListViewModel : IMapFromService, IHaveCustomMappings
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public decimal BasePrice { get; set; }

        public int EstimatedDurationMinutes { get; set; }

        public string CategorySlug { get; set; }

        public bool IsActive { get; set; }

        public int DisplayOrder { get; set; }

        public bool IsPopular { get; set; }

        public bool IsQuoteOnly { get; set; }

        /// <summary>
        /// How many jobs have this service. With one or more the list shows a lock in place of the
        /// delete button: such a service is switched off, not deleted (PROJECT_STATE.md Section 3ef).
        /// </summary>
        public int JobCount { get; set; }

        public void CreateMappings(TypeAdapterConfig config)
        {
            config.NewConfig<Service, ServiceAdminListViewModel>()
                .Map(dest => dest.JobCount, src => src.BookingServices.Count(x => !x.IsDeleted));
        }
    }
}
