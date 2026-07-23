namespace HandyFix.Services.Data.Inquiries
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Web.ViewModels.Home;

    public interface IInquiriesService
    {
        Task CreateInquiryAsync(ContactInputModel model, IReadOnlyList<string> imageUrls);

        Task<IEnumerable<T>> GetAllAsync<T>();

        Task<T> GetByIdAsync<T>(Guid id);

        Task DeleteAsync(Guid id);
    }
}
