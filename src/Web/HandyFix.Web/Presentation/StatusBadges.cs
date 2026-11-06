namespace HandyFix.Web.Presentation
{
    using HandyFix.Common;

    /// <summary>
    /// The colour of the two labels every job carries (<see cref="JobLabels"/>): where the work
    /// stands and where the money stands. The job's page, the job list and the calendar each
    /// worked these out in the page; they read them from here now (PROJECT_STATE.md Section 3dj).
    /// </summary>
    public static class StatusBadges
    {
        public static string ForJob(string jobLabel) => jobLabel switch
        {
            JobLabels.Done => "tint-success",
            JobLabels.Cancelled => "tint-error",
            JobLabels.Abandoned => "tint-neutral",
            JobLabels.WaitingForDeposit => "tint-waiting",
            _ => "tint-secondary",
        };

        public static string ForMoney(string moneyLabel) => moneyLabel switch
        {
            JobLabels.PaidInFull => "tint-success",
            JobLabels.DepositPaid or JobLabels.PartPaid => "tint-secondary",
            _ => "tint-neutral",
        };
    }
}
