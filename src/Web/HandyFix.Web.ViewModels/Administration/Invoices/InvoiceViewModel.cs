namespace HandyFix.Web.ViewModels.Administration.Invoices
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using HandyFix.Common;

    // One job's invoice as it stands: what the work came to, what has been paid towards it and
    // what is left. Built from the job each time it is shown, so a payment written on the job
    // later shows on the invoice the next time it is downloaded or sent.
    public class InvoiceViewModel
    {
        public Guid BookingId { get; set; }

        public int Number { get; set; }

        /// <summary>
        /// The invoice's date, on a UK clock.
        /// </summary>
        public DateTime InvoicedOn { get; set; }

        public string CustomerName { get; set; }

        public string Address { get; set; }

        public string Email { get; set; }

        public string PhoneNumber { get; set; }

        public string JobReference { get; set; }

        public string Services { get; set; }

        public string WorkDone { get; set; }

        /// <summary>
        /// The day of the visit, when the job has one.
        /// </summary>
        public DateTime? WorkDate { get; set; }

        /// <summary>
        /// The job's final price: what the work came to.
        /// </summary>
        public decimal Total { get; set; }

        public IReadOnlyList<InvoicePaymentLine> Payments { get; set; } = new List<InvoicePaymentLine>();

        public string NumberText => InvoiceTerms.Number(this.Number);

        public DateTime DueOn => InvoiceTerms.DueOn(this.InvoicedOn);

        public decimal Paid => this.Payments.Sum(x => x.Amount);

        /// <summary>
        /// What is left to pay. Never below nothing: money paid over the price is a matter for
        /// the admin, not a line on the invoice.
        /// </summary>
        public decimal Balance => Math.Max(0m, this.Total - this.Paid);

        public bool IsPaid => this.Balance == 0m;

        // What came in each way, for the list the accountant gets: money in cash never reaches
        // the bank account, and Stripe pays the deposits in as one sum.
        public decimal PaidDeposit => this.PaidBy(InvoicePaymentLine.DepositMethod);

        public decimal PaidByCard => this.PaidBy(PaymentMethods.Card);

        public decimal PaidInCash => this.PaidBy(PaymentMethods.Cash);

        public decimal PaidByBankTransfer => this.PaidBy(PaymentMethods.BankTransfer);

        // Tips added when the invoice was paid online: in Stripe's payout with the rest, but
        // owed to the technician, not to the job.
        public decimal Tips => this.Payments.Sum(x => x.Tip);

        // Whether the day to pay has gone by, on the UK day given.
        public bool IsOverdueOn(DateTime todayUk) => !this.IsPaid && todayUk.Date > this.DueOn;

        // "Paid", "Overdue" or "Due 10 Nov", as the list of invoices and the job's page show it.
        public string StatusOn(DateTime todayUk) => this.IsPaid ? "Paid"
            : this.IsOverdueOn(todayUk) ? "Overdue"
            : "Due " + this.DueOn.ToString("d MMM", CultureInfo.InvariantCulture);

        private decimal PaidBy(string method) => this.Payments.Where(x => x.Method == method).Sum(x => x.Amount);
    }
}
