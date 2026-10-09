namespace HandyFix.Common
{
    // What an admin may do to a job, by where it stands. The job's page asks these to decide
    // which buttons to show, and the service asks them again before it acts: a page left open
    // while the job moved on cannot do what its buttons no longer offer
    // (PROJECT_STATE.md Section 3ce).
    //
    // "cameFromWebsite" matters because only a website booking has a deposit. One that is not
    // paid is dropped after about fifteen minutes, so until the deposit is in there is no job to
    // send anyone to. A job the admin wrote in never has a deposit and is a job from the start.
    public static class BookingRules
    {
        private const string Pending = "Pending";
        private const string Approved = "Approved";
        private const string InProgress = "InProgress";
        private const string Completed = "Completed";
        private const string Cancelled = "Cancelled";

        public static bool CanPickTechnician(string statusName, bool cameFromWebsite, bool depositPaid)
        {
            return IsOn(statusName, cameFromWebsite, depositPaid);
        }

        public static bool CanComplete(string statusName, bool cameFromWebsite, bool depositPaid)
        {
            return IsOn(statusName, cameFromWebsite, depositPaid);
        }

        public static bool CanMove(string statusName, bool cameFromWebsite, bool depositPaid)
        {
            return IsOn(statusName, cameFromWebsite, depositPaid);
        }

        // The deposit is paid once, on the website, by a booking that is still waiting for it.
        // Not by one that was dropped or cancelled, whose hour may be someone else's by now, and
        // not by a job the admin wrote in, which has no deposit (PROJECT_STATE.md Section 3cg).
        public static bool CanPayDeposit(string statusName, bool cameFromWebsite, bool depositPaid)
        {
            return cameFromWebsite && statusName == Pending && !depositPaid;
        }

        // What the customer's "Booking Confirmed" page stands on: a website booking with its
        // deposit in, that is on or done.
        public static bool IsConfirmed(string statusName, bool cameFromWebsite, bool depositPaid)
        {
            return cameFromWebsite && depositPaid && (IsUnderWay(statusName) || statusName == Completed);
        }

        // A booking still waiting for its deposit can be called off too. Cancelling one that is
        // already done, cancelled or abandoned would only rewrite what happened to it.
        public static bool CanCancel(string statusName)
        {
            return statusName == Pending || IsUnderWay(statusName);
        }

        // Money is written on a job that is on, and on one that is done: that is when most of it
        // comes in. Not on a cancelled or abandoned one, and not on a website booking before its
        // deposit, which is paid on the website and nowhere else.
        public static bool CanTakePayment(string statusName, bool cameFromWebsite, bool depositPaid)
        {
            return statusName == Completed || IsOn(statusName, cameFromWebsite, depositPaid);
        }

        // The final price is typed when the job is marked done, and can be put right afterwards.
        public static bool CanChangeFinalPrice(string statusName)
        {
            return statusName == Completed;
        }

        // The tick that says a deposit went back. Only a cancelled job's deposit is sent back, and
        // there has to have been one.
        public static bool CanMarkDepositRefunded(string statusName, bool depositPaidOrRefunded)
        {
            return statusName == Cancelled && depositPaidOrRefunded;
        }

        // Who the customer is, where the job is and what it is can be put right while the job is
        // booked and once it is done: a wrong phone number matters until the job is paid for, and
        // a wrong address after that. A cancelled or abandoned job is a record of what happened,
        // and keeps the details it ended with (PROJECT_STATE.md Section 3cf).
        public static bool CanEditDetails(string statusName)
        {
            return statusName == Pending || IsUnderWay(statusName) || statusName == Completed;
        }

        private static bool IsOn(string statusName, bool cameFromWebsite, bool depositPaid)
        {
            return IsUnderWay(statusName) && (!cameFromWebsite || depositPaid);
        }

        private static bool IsUnderWay(string statusName)
        {
            return statusName == Approved || statusName == InProgress;
        }
    }
}
