namespace HandyFix.Web.Areas.Administration.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Services.Data.Invoices;
    using HandyFix.Web.ViewModels.Administration.Invoices;

    using Microsoft.AspNetCore.Mvc;

    // Every invoice the site has made, a month at a time: each one's PDF again, and the month's
    // list as a CSV file and as a PDF for the accountant. An invoice is made on its job's page
    // (BookingsController.Invoice); nothing here changes one.
    public class InvoicesController : AdministrationController
    {
        private const string PdfType = "application/pdf";

        private readonly IInvoicesService invoicesService;
        private readonly TimeProvider clock;

        public InvoicesController(IInvoicesService invoicesService, TimeProvider clock)
        {
            this.invoicesService = invoicesService;
            this.clock = clock;
        }

        public async Task<IActionResult> Index(string month = null)
        {
            DateTime firstDay = this.MonthOrThisOne(month);
            IReadOnlyList<InvoiceViewModel> invoices = await this.invoicesService.GetMonthAsync(firstDay.Year, firstDay.Month);

            return this.View(new InvoiceListViewModel
            {
                Year = firstDay.Year,
                Month = firstDay.Month,
                TodayUk = this.TodayUk(),
                Invoices = invoices,
            });
        }

        public async Task<IActionResult> Pdf(Guid id)
        {
            InvoiceViewModel invoice = await this.invoicesService.GetAsync(id);
            if (invoice == null)
            {
                return this.NotFound();
            }

            return this.File(this.invoicesService.Pdf(invoice), PdfType, $"Invoice {invoice.NumberText}.pdf");
        }

        public async Task<IActionResult> Csv(string month = null)
        {
            DateTime firstDay = this.MonthOrThisOne(month);
            IReadOnlyList<InvoiceViewModel> invoices = await this.invoicesService.GetMonthAsync(firstDay.Year, firstDay.Month);

            return this.File(this.invoicesService.MonthCsv(invoices), "text/csv", $"Invoices {InvoiceListViewModel.MonthOf(firstDay)}.csv");
        }

        public async Task<IActionResult> ListPdf(string month = null)
        {
            DateTime firstDay = this.MonthOrThisOne(month);
            IReadOnlyList<InvoiceViewModel> invoices = await this.invoicesService.GetMonthAsync(firstDay.Year, firstDay.Month);

            return this.File(this.invoicesService.MonthPdf(invoices, firstDay.Year, firstDay.Month), PdfType, $"Invoices {InvoiceListViewModel.MonthOf(firstDay)}.pdf");
        }

        // "2026-11" from the month box or a link, read as the first of that month; anything else,
        // or nothing, is this month.
        private DateTime MonthOrThisOne(string month)
        {
            return DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime firstDay)
                    && firstDay.Year >= 2020 && firstDay.Year <= 2100
                ? firstDay
                : new DateTime(this.TodayUk().Year, this.TodayUk().Month, 1);
        }

        private DateTime TodayUk()
        {
            return UkTime.FromUtc(this.clock.GetUtcNow().UtcDateTime).Date;
        }
    }
}
