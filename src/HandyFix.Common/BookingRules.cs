namespace HandyFix.Common
{
    // What an admin may do to a booking, by where it stands. The booking's page asks these to
    // decide which buttons to show, and the service asks them again before it acts: a page left
    // open while the booking moved on cannot do what its buttons no longer offer
    // (PROJECT_STATE.md Section 3ce).
    public static class BookingRules
    {
        private const string Pending = "Pending";
        private const string Approved = "Approved";
        private const string InProgress = "InProgress";

        // A booking that is not paid is dropped after about fifteen minutes, so until the deposit
        // is in there is no job to send anyone to. One that is completed, cancelled or abandoned
        // has nobody left to send.
        public static bool CanPickTechnician(string statusName, bool depositPaid)
        {
            return depositPaid && IsUnderWay(statusName);
        }

        public static bool CanComplete(string statusName, bool depositPaid)
        {
            return depositPaid && IsUnderWay(statusName);
        }

        // A booking still waiting for its deposit can be called off too. Cancelling one that is
        // already completed, cancelled or abandoned would only rewrite what happened to it.
        public static bool CanCancel(string statusName)
        {
            return statusName == Pending || IsUnderWay(statusName);
        }

        private static bool IsUnderWay(string statusName)
        {
            return statusName == Approved || statusName == InProgress;
        }
    }
}
