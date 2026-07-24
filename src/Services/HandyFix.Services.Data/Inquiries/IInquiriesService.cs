namespace HandyFix.Services.Data.Inquiries
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Web.ViewModels.Administration.Enquiries;
    using HandyFix.Web.ViewModels.Home;

    public interface IInquiriesService
    {
        Task CreateInquiryAsync(ContactInputModel model, IReadOnlyList<string> imageUrls);

        Task<IEnumerable<T>> GetAllAsync<T>(
            InquirySortField sortField = InquirySortField.CreatedOn,
            bool descending = true);

        Task<T> GetByIdAsync<T>(Guid id);

        Task DeleteAsync(Guid id);
    }
}
