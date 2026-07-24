namespace HandyFix.Web.ViewModels.Administration.Enquiries
{
    using System.Collections.Generic;

    public class EnquiryListViewModel
    {
        public IEnumerable<EnquiryViewModel> Inquiries { get; set; }

        public InquirySortField SortField { get; set; }

        public bool Descending { get; set; }
    }
}
