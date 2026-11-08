namespace HandyFix.Web.ViewModels.Administration.Invoices
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    // The invoices made in one month, as the admin's Invoices page lists them, with the month's
    // sums and the months either side of it.
    public class InvoiceListViewModel
    {
        public int Year { get; set; }

        public int Month { get; set; }

        /// <summary>
        /// Today on a UK clock, against which "Overdue" is read.
        /// </summary>
        public DateTime TodayUk { get; set; }

        public IReadOnlyList<InvoiceViewModel> Invoices { get; set; } = new List<InvoiceViewModel>();

        // "2026-11": what the month box holds and what the links carry.
        public string MonthValue => MonthOf(this.FirstDay);

        public string MonthName => this.FirstDay.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

        public string PreviousMonth => MonthOf(this.FirstDay.AddMonths(-1));

        public string NextMonth => MonthOf(this.FirstDay.AddMonths(1));

        // No arrow into a month that has not started.
        public bool HasNextMonth => this.FirstDay.AddMonths(1) <= this.TodayUk.Date;

        public decimal Total => this.Invoices.Sum(x => x.Total);

        public decimal Paid => this.Invoices.Sum(x => x.Paid);

        public decimal Balance => this.Invoices.Sum(x => x.Balance);

        public int UnpaidCount => this.Invoices.Count(x => !x.IsPaid);

        private DateTime FirstDay => new DateTime(this.Year, this.Month, 1);

        public static string MonthOf(DateTime day) => day.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        // "Paid", "Overdue" or "Due 10 Nov" in the colour the job pages use for money.
        public string BadgeFor(InvoiceViewModel invoice) => invoice.IsPaid ? "tint-success"
            : invoice.IsOverdueOn(this.TodayUk) ? "tint-error"
            : "tint-secondary";
    }
}
