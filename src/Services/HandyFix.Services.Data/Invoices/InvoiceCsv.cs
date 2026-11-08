namespace HandyFix.Services.Data.Invoices
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;

    using HandyFix.Web.ViewModels.Administration.Invoices;

    // A month's invoices as a CSV file, for the accountant. She works from the company's bank
    // account, where cash never arrives and Stripe pays the deposits in as one sum: the columns
    // say how each pound came in.
    public static class InvoiceCsv
    {
        private static readonly string[] Header =
        {
            "Invoice", "Date", "Customer", "Job", "Services", "Total", "Deposit (online card)", "Card", "Cash", "Bank transfer", "Paid", "Balance", "Tip (online card)", "Status",
        };

        public static byte[] For(IEnumerable<InvoiceViewModel> invoices, DateTime todayUk)
        {
            var csv = new StringBuilder();
            csv.AppendLine(string.Join(",", Header));

            foreach (InvoiceViewModel invoice in invoices)
            {
                var fields = new[]
                {
                    invoice.NumberText,
                    invoice.InvoicedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    invoice.CustomerName,
                    invoice.JobReference,
                    invoice.Services,
                    Amount(invoice.Total),
                    Amount(invoice.PaidDeposit),
                    Amount(invoice.PaidByCard),
                    Amount(invoice.PaidInCash),
                    Amount(invoice.PaidByBankTransfer),
                    Amount(invoice.Paid),
                    Amount(invoice.Balance),
                    Amount(invoice.Tips),
                    invoice.StatusOn(todayUk),
                };

                csv.AppendLine(string.Join(",", fields.Select(Field)));
            }

            // With the byte order mark, Excel reads the file as UTF-8 and a name with an accent
            // comes out as it was typed.
            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        }

        private static string Amount(decimal amount)
        {
            return amount.ToString("0.00", CultureInfo.InvariantCulture);
        }

        // A name typed on the website could start with "=", which Excel would run as a formula:
        // such a field gets an apostrophe in front, which Excel shows as text. A field with a
        // comma, a quote or a line break goes in quotes, its quotes doubled.
        private static string Field(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if ("=+-@".Contains(value[0]) && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            {
                value = "'" + value;
            }

            return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }
    }
}
