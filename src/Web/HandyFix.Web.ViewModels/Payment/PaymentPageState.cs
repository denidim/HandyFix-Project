namespace HandyFix.Web.ViewModels.Payment
{
    // What the page a customer lands on without a paid booking has to tell them.
    public enum PaymentPageState
    {
        // The deposit was not paid and the time is still held: the payment can be tried again.
        NotPaid = 0,

        // The booking was dropped for want of its deposit and no deposit is held: book again.
        NoLongerHeld = 1,

        // The deposit arrived after the booking was dropped. The company rings to confirm.
        DepositReceived = 2,

        // The booking was cancelled, whatever became of its deposit.
        Cancelled = 3,
    }
}
