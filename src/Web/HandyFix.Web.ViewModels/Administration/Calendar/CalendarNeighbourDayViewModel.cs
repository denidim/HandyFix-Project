namespace HandyFix.Web.ViewModels.Administration.Calendar
{
    using System;

    /// <summary>
    /// The day before or after the one the calendar shows: where an arrow leads, and whether
    /// there is anything there to light it up for.
    /// </summary>
    public class CalendarNeighbourDayViewModel
    {
        public DateTime Date { get; set; }

        public bool HasJobs { get; set; }

        public bool HasSlots { get; set; }
    }
}
