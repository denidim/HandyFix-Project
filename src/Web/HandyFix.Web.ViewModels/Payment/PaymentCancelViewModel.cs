namespace HandyFix.Web.ViewModels.Payment
{
    using System;

    public class PaymentCancelViewModel
    {
        /// <summary>
        /// Empty when the page was opened without a booking the site knows: it then says the
        /// payment was not made and offers nothing to retry.
        /// </summary>
        public Guid? BookingId { get; set; }

        public PaymentPageState State { get; set; }
    }
}
