namespace HandyFix.Web.ViewModels.Booking
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    using Mapster;

    public class BookingDetailsViewModel : HandyFix.Services.Mapping.IMapFrom<Booking>, IHaveCustomMappings
    {
        public Guid Id { get; set; }

        public string CustomerFirstName { get; set; }

        public string CustomerLastName { get; set; }

        public string Email { get; set; }

        public string PhoneNumber { get; set; }

        public string Address { get; set; }

        public string ProblemDescription { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal DepositAmount { get; set; }

        public string StatusName { get; set; }

        public DateTime ScheduledTime { get; set; }

        public DateTime ScheduledEndTime { get; set; }

        public string TechnicianName { get; set; }

        public string PaymentStatus { get; set; }

        public IEnumerable<string> Services { get; set; }

        public IEnumerable<string> ImageUrls { get; set; }

        public void CreateMappings(TypeAdapterConfig config)
        {
            config.NewConfig<Booking, BookingDetailsViewModel>()
                .Map(dest => dest.StatusName, src => src.Status != null ? src.Status.Name : "Pending")
                .Map(dest => dest.ScheduledTime, src => src.AvailabilitySlot != null ? src.AvailabilitySlot.StartTime : default)
                .Map(dest => dest.ScheduledEndTime, src => src.AvailabilitySlot != null ? src.AvailabilitySlot.EndTime : default)
                .Map(dest => dest.TechnicianName, src => src.Technician != null ? $"{src.Technician.FirstName} {src.Technician.LastName}" : "Not Assigned")
                .Map(dest => dest.PaymentStatus, src => src.Payments != null && src.Payments.Any() ? src.Payments.OrderByDescending(p => p.CreatedOn).First().Status.Name : "Unpaid")
                .Map(dest => dest.Services, src => src.BookingServices != null ? src.BookingServices.Select(x => x.Service.Name) : new List<string>())
                .Map(dest => dest.ImageUrls, src => src.Images != null ? src.Images.Select(x => x.ImageUrl) : new List<string>());
        }
    }
}
