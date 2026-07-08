namespace HandyFix.Services.Data.Inquiries
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IInquiriesService
    {
        Task CreateInquiryAsync(string name, string email, string phoneNumber, string message, IEnumerable<string> imageUrls = null);

        Task<IEnumerable<T>> GetAllAsync<T>();

        Task<T> GetByIdAsync<T>(Guid id);

        Task DeleteAsync(Guid id);
    }
}
