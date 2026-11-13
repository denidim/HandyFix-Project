namespace HandyFix.Services.Data.Inquiries
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;
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

        // How many enquiries sent since then gave each answer to "How did you hear about us?";
        // every enquiry kept when no day is given (PROJECT_STATE.md Section 3et). An answer no
        // enquiry gave is not in it.
        Task<IReadOnlyDictionary<HeardAbout, int>> GetHeardAboutCountsAsync(DateTime? since);
    }
}
