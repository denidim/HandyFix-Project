namespace HandyFix.Web.ViewModels.Payment
{
    using HandyFix.Web.ViewModels.Administration.Invoices;

    public class InvoicePaymentViewModel
    {
        public InvoiceViewModel Invoice { get; set; }

        /// <summary>
        /// Back from Stripe with this invoice paid, as Stripe itself says, not the address.
        /// </summary>
        public bool JustPaid { get; set; }
    }
}
