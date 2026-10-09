namespace HandyFix.Web.ViewModels.Booking
{
    using System;
    using System.ComponentModel.DataAnnotations;

    using HandyFix.Data.Models;

    /// <summary>
    /// A job the admin writes in by hand: one that came by phone, WhatsApp, an enquiry or an
    /// agency. A first name, a phone number, a day and an hour are enough
    /// (PROJECT_STATE.md Section 3ce); the website's own form, <see cref="BookingInputModel"/>,
    /// asks a customer for a good deal more. The customer and the work are the boxes it shares
    /// with "Edit the details" (<see cref="JobDetailsInputModel"/>).
    /// </summary>
    public class JobInputModel : JobDetailsInputModel
    {
        [Required(ErrorMessage = "Pick the day.")]
        [DataType(DataType.Date)]
        public DateTime? Date { get; set; }

        [Required(ErrorMessage = "Pick the time.")]
        [DataType(DataType.Time)]
        public TimeSpan? Time { get; set; }

        [Required(ErrorMessage = "Pick where the job came from.")]
        [Display(Name = "Came from")]
        public BookingSource? Source { get; set; }

        /// <summary>
        /// For the page only: the enquiry the form was opened from, so "Back" goes back to it.
        /// </summary>
        public Guid? EnquiryId { get; set; }
    }
}
