namespace HandyFix.Common
{
    using System;
    using System.Globalization;

    // How an invoice is numbered and how long the customer has to pay what is left. Written once
    // here so the invoice, its email, the job's page and the list of invoices say the same.
    public static class InvoiceTerms
    {
        // The remaining balance is due immediately upon completion of the work, on the same date
        // as the invoice itself.
        public const int DaysToPay = 0;

        public const string PaymentTerms = "Due on Completion";

        // In front of every number the site gives. The owner's own invoices, written by hand
        // before the site made them, had numbers of their own: the prefix keeps the two apart.
        public const string NumberPrefix = "PHS-";

        // "PHS-0001": four figures, so the numbers sort as they were given.
        public static string Number(int number)
        {
            return NumberPrefix + number.ToString("0000", CultureInfo.InvariantCulture);
        }

        // The last day to pay, from the invoice's date as the UK reads it.
        public static DateTime DueOn(DateTime invoicedOnUk)
        {
            return invoicedOnUk.Date.AddDays(DaysToPay);
        }
    }
}
