namespace HandyFix.Web.ViewModels.Administration.Technicians
{
    using System;
    using System.Linq;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    using Mapster;

    using IMapFromTechnician = HandyFix.Services.Mapping.IMapFrom<HandyFix.Data.Models.Technician>;

    public class TechnicianAdminListViewModel : IMapFromTechnician, IHaveCustomMappings
    {
        public Guid Id { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string PhoneNumber { get; set; }

        public bool IsActive { get; set; }

        public int BookingCount { get; set; }

        public string FullName => $"{this.FirstName} {this.LastName}";

        public void CreateMappings(TypeAdapterConfig config)
        {
            config.NewConfig<Technician, TechnicianAdminListViewModel>()
                .Map(dest => dest.BookingCount, src => src.Bookings.Count(b => !b.IsDeleted));
        }
    }
}
