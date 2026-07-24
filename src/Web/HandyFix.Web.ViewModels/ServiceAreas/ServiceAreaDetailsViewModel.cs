namespace HandyFix.Web.ViewModels.ServiceAreas
{
    using System;
    using System.Collections.Generic;

    using HandyFix.Data.Models;

    using IMapFromServiceArea = HandyFix.Services.Mapping.IMapFrom<HandyFix.Data.Models.ServiceArea>;

    public class ServiceAreaDetailsViewModel : IMapFromServiceArea
    {
        public Guid Id { get; set; }

        public string Slug { get; set; }

        public string Name { get; set; }

        public string Region { get; set; }

        public int DriveTimeMinutes { get; set; }

        public string IntroCopy { get; set; }

        public string LocalNeighbourhoodsCopy { get; set; }

        // Mapped from ServiceArea.Faqs by convention; the collection is not
        // guaranteed to arrive in DisplayOrder from the mapper alone (Mapster's
        // ProjectToType does not reliably honour an OrderBy applied inside a
        // custom collection mapping under the EF Core InMemory provider), so the
        // controller re-orders it explicitly by DisplayOrder after mapping.
        public IEnumerable<ServiceAreaFaqViewModel> Faqs { get; set; }

        public IEnumerable<ServiceAreaViewModel> NearbyAreas { get; set; }
    }
}
