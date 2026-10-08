namespace HandyFix.Services.Data.Inquiries
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Web.ViewModels.Administration.Enquiries;
    using HandyFix.Web.ViewModels.Home;

    public interface IInquiriesService
    {
        /// <summary>
        /// Whether an enquiry with this email and this exact message was saved in the last ten
        /// minutes, which makes this one the same enquiry sent again.
        /// </summary>
        Task<bool> IsRecentDuplicateAsync(ContactInputModel model);

        Task CreateInquiryAsync(ContactInputModel model, IReadOnlyList<string> imageUrls);

        Task<IEnumerable<T>> GetAllAsync<T>(
            InquirySortField sortField = InquirySortField.CreatedOn,
            bool descending = true);

        Task<T> GetByIdAsync<T>(Guid id);

        Task DeleteAsync(Guid id);

        Task<int> GetTotalCountAsync();
    }
}
