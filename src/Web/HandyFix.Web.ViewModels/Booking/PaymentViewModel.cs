namespace HandyFix.Web.ViewModels.Booking
{
    using System;

    using HandyFix.Data.Models;
    using HandyFix.Services.Mapping;

    public class PaymentViewModel : IMapFrom<Payment>
    {
        public Guid Id { get; set; }

        public decimal Amount { get; set; }

        public string Provider { get; set; }

        public string TransactionId { get; set; }

        public string CheckoutSessionId { get; set; }

        public Guid BookingId { get; set; }
    }
}
