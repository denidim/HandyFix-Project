namespace HandyFix.Web.ViewModels.Administration.Calendar
{
    using System;

    /// <summary>
    /// One of the calendar's four arrows: the day it leads to, and whether it is lit. A lit arrow
    /// leads to the nearest day that way with something for its list, however far away; with
    /// nothing that way it is grey and leads to the next day (PROJECT_STATE.md Section 3cs).
    /// </summary>
    public class CalendarArrowViewModel
    {
        public DateTime Date { get; set; }

        public bool Lit { get; set; }
    }
}
