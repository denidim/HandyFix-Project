namespace HandyFix.Services.Data.Invoices
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Web.ViewModels.Administration.Invoices;

    // A job's invoice, and the list of invoices for the accountant. One job has one invoice: it
    // is made from the job's own final price and payments, and given its number the first time.
    public interface IInvoicesService
    {
        // Makes the job's invoice, or brings it up to date: keeps the "work done" text and gives
        // the job its number the first time. Null when there is no such job, or it is not done.
        Task<InvoiceViewModel> MakeAsync(Guid bookingId, string workDone);

        // An invoice already made, as it stands now. Null when the job has none.
        Task<InvoiceViewModel> GetAsync(Guid bookingId);

        // Makes the invoice as MakeAsync does and emails it to the customer as a PDF.
        Task<InvoiceEmailResult> EmailAsync(Guid bookingId, string workDone);

        // Every invoice made in a month, by number.
        Task<IReadOnlyList<InvoiceViewModel>> GetMonthAsync(int year, int month);

        byte[] Pdf(InvoiceViewModel invoice);

        // The month's list as a PDF table and as a CSV file Excel opens.
        byte[] MonthPdf(IReadOnlyList<InvoiceViewModel> invoices, int year, int month);

        byte[] MonthCsv(IReadOnlyList<InvoiceViewModel> invoices);
    }
}
