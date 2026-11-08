namespace HandyFix.Web.ViewModels.Administration.Invoices
{
    using System;

    using HandyFix.Common;

    // One payment towards a job, as its invoice lists it.
    public class InvoicePaymentLine
    {
        // The website deposit has no method of its own on the job; on the invoice it is its own.
        public const string DepositMethod = "Deposit";

        /// <summary>
        /// The day it was paid, on a UK clock.
        /// </summary>
        public DateTime PaidOn { get; set; }

        /// <summary>
        /// <see cref="DepositMethod"/> or one of <see cref="PaymentMethods.All"/>.
        /// </summary>
        public string Method { get; set; }

        public decimal Amount { get; set; }

        /// <summary>
        /// A tip added to an invoice paid online, for the technician. Not part of
        /// <see cref="Amount"/>: it pays nothing off the job.
        /// </summary>
        public decimal Tip { get; set; }

        public string Description => this.Method switch
        {
            DepositMethod => "Booking deposit, paid online by card",
            PaymentMethods.Cash => "Paid in cash",
            PaymentMethods.Card => "Paid by card",
            PaymentMethods.BankTransfer => "Paid by bank transfer",
            _ => "Paid",
        };
    }
}
