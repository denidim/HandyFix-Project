namespace HandyFix.Web.ViewModels.Administration.Enquiries
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    using Mapster;

    public class EnquiryViewModel : HandyFix.Services.Mapping.IMapFrom<Inquiry>, IHaveCustomMappings
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public string Email { get; set; }

        public string PhoneNumber { get; set; }

        public string Message { get; set; }

        public DateTime CreatedOn { get; set; }

        public IEnumerable<string> ImageUrls { get; set; }

        // The emails about this enquiry that the site could not send (PROJECT_STATE.md Section 3cq).
        public IEnumerable<string> EmailsNotSent { get; set; }

        public void CreateMappings(TypeAdapterConfig config)
        {
            config.NewConfig<Inquiry, EnquiryViewModel>()
                .Map(dest => dest.ImageUrls, src => src.Images != null ? src.Images.Select(x => x.ImageUrl) : new List<string>())
                .Map(dest => dest.EmailsNotSent, src => src.EmailsNotSent != null ? src.EmailsNotSent.OrderBy(x => x.CreatedOn).Select(x => x.What) : new List<string>());
        }
    }
}
