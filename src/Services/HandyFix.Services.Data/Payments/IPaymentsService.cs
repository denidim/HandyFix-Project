namespace HandyFix.Services.Data.Payments
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IPaymentsService
    {
        Task<Guid> CreatePaymentRecordAsync(Guid bookingId, decimal amount, string provider, string checkoutSessionId);

        Task ProcessPaymentSuccessAsync(string checkoutSessionId, string transactionId);

        Task CancelPaymentAsync(string checkoutSessionId);

        Task CancelPendingPaymentsForBookingsAsync(IEnumerable<Guid> bookingIds);

        Task<IEnumerable<T>> GetPaymentsForBookingAsync<T>(Guid bookingId);

        Task<IEnumerable<T>> GetAllPaymentsAsync<T>();
    }
}
