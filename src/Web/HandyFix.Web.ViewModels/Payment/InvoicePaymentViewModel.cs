namespace HandyFix.Web.ViewModels.Payment
{
    using HandyFix.Web.ViewModels.Administration.Invoices;

    public class InvoicePaymentViewModel
    {
        public InvoiceViewModel Invoice { get; set; }

        public bool JustPaid { get; set; }
    }
}
