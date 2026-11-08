namespace HandyFix.Services.Data.Invoices
{
    using HandyFix.Web.ViewModels.Administration.Invoices;

    public class InvoiceEmailResult
    {
        public InvoiceEmailOutcome Outcome { get; set; }

        // Null when the outcome is NotAllowed.
        public InvoiceViewModel Invoice { get; set; }
    }
}
