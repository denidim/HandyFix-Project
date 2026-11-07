namespace HandyFix.Services.Data.Invoices
{
    public enum InvoiceEmailOutcome
    {
        // There is no such job, or it is not done.
        NotAllowed,

        Sent,

        // The invoice is made, but the job has no email address to send it to.
        NoEmailAddress,

        // The invoice is made, but the email could not be sent.
        NotSent,
    }
}
