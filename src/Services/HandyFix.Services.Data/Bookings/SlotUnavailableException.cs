namespace HandyFix.Services.Data.Bookings
{
    using System;

    public class SlotUnavailableException : Exception
    {
        public SlotUnavailableException(string message)
            : base(message)
        {
        }
    }
}
