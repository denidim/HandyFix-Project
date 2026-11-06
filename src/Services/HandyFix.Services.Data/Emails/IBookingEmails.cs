namespace HandyFix.Services.Data.Emails
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using HandyFix.Data.Models;

    // The emails about a booking and its deposit: what each says, who it is from and who it goes
    // to. When one is sent, and what is written on the job if it fails, is decided by the
    // service that sends it. Each answers whether the email went, and none of them throws.
    public interface IBookingEmails
    {
        // To the customer, when an admin picks the technician for their job. The names of the
        // job's services are asked for by the caller and passed in.
        Task<bool> TechnicianPickedAsync(Booking booking, Technician technician, IReadOnlyList<string> serviceNames);

        // To the customer, when their deposit is paid: the booking is confirmed.
        Task<bool> DepositPaidToCustomerAsync(Booking booking, Payment payment);

        // To the company, when a deposit is paid: open the job and pick a technician.
        Task<bool> DepositPaidToCompanyAsync(Booking booking, Payment payment);

        // To the company, when a deposit is paid for a booking that was not waiting for one.
        Task<bool> DepositForBookingNotHeldAsync(Booking booking, Payment payment);
    }
}
