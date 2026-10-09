namespace HandyFix.Common
{
    using System;
    using System.Collections.Generic;

    // The two labels every job carries, as a person reads them (PROJECT_STATE.md Section 3ce).
    // One says where the work stands, the other where the money stands. They are kept apart
    // because they move separately: a job can be done and still not paid, and a job taken by
    // phone never has a deposit.
    //
    // The database keeps its own names for where the work stands ("Approved", "Completed"), from
    // before the business had settled on its words. This is the one place that turns them into
    // those words, and back again for the list's filter.
    public static class JobLabels
    {
        public const string Booked = "Booked";

        public const string Done = "Done";

        public const string Cancelled = "Cancelled";

        public const string Abandoned = "Abandoned";

        public const string NotPaid = "Not paid";

        public const string DepositPaid = "Deposit paid";

        public const string PartPaid = "Part paid";

        public const string PaidInFull = "Paid in full";

        public const string DepositRefunded = "Deposit refunded";

        // In the order a job goes through them, for the list's filter.
        public static readonly IReadOnlyList<string> JobOptions = new[] { Booked, Done, Cancelled, Abandoned };

        // "Booked" covers a website booking still waiting for its deposit too: it has a day and an
        // hour, and the money label beside it says "Not paid".
        public static string Job(string statusName)
        {
            return statusName switch
            {
                "Completed" => Done,
                "Cancelled" => Cancelled,
                "Abandoned" => Abandoned,
                _ => Booked,
            };
        }

        // The database's names behind one label. Anything that is not a label gives none.
        public static IReadOnlyList<string> StatusNames(string jobLabel)
        {
            return jobLabel switch
            {
                Booked => new[] { "Pending", "Approved", "InProgress" },
                Done => new[] { "Completed" },
                Cancelled => new[] { "Cancelled" },
                Abandoned => new[] { "Abandoned" },
                _ => Array.Empty<string>(),
            };
        }

        // "paid" is everything that has come in and stayed: the website deposit and whatever the
        // admin wrote on the job. "Paid in full" needs a final price to be full against, so a job
        // that is not done yet is never that, however much has come in.
        public static string Money(decimal paid, decimal? finalPrice, bool depositPaid, bool otherPayments, bool depositRefunded)
        {
            if (paid <= 0)
            {
                return depositRefunded ? DepositRefunded : NotPaid;
            }

            if (finalPrice.HasValue && paid >= finalPrice.Value)
            {
                return PaidInFull;
            }

            return depositPaid && !otherPayments ? DepositPaid : PartPaid;
        }
    }
}
