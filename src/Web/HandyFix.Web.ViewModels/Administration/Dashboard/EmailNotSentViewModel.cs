namespace HandyFix.Web.ViewModels.Administration.Dashboard
{
    using System;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    using Mapster;

    using IMapFromEmailNotSent = HandyFix.Services.Mapping.IMapFrom<HandyFix.Data.Models.EmailNotSent>;

    // One email the site could not send, as the dashboard lists it: what it was, when, and the job
    // or the enquiry to open (PROJECT_STATE.md Section 3cq).
    public class EmailNotSentViewModel : IMapFromEmailNotSent, IHaveCustomMappings
    {
        public DateTime CreatedOn { get; set; }

        public string What { get; set; }

        public Guid? BookingId { get; set; }

        public string CustomerFirstName { get; set; }

        public string CustomerLastName { get; set; }

        public Guid? InquiryId { get; set; }

        public string InquiryName { get; set; }

        public void CreateMappings(TypeAdapterConfig config)
        {
            config.NewConfig<EmailNotSent, EmailNotSentViewModel>()
                .Map(dest => dest.CustomerFirstName, src => src.Booking != null ? src.Booking.CustomerFirstName : null)
                .Map(dest => dest.CustomerLastName, src => src.Booking != null ? src.Booking.CustomerLastName : null)
                .Map(dest => dest.InquiryName, src => src.Inquiry != null ? src.Inquiry.Name : null);
        }
    }
}
