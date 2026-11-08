namespace HandyFix.Services.Data.Payments
{
    using HandyFix.Services.Data.Invoices;

    // What came of a payment the admin wrote on a job.
    public class PaymentAddResult
    {
        public bool Added { get; set; }

        /// <summary>
        /// Set when this payment paid a done job in full: what became of the invoice marked paid
        /// in full, which then goes to the customer by itself.
        /// </summary>
        public InvoiceEmailOutcome? PaidInvoice { get; set; }
    }
}
