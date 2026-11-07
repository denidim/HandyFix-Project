namespace HandyFix.Services.Data.Emails
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data.Models;
    using HandyFix.Services.Messaging;
    using HandyFix.Web.ViewModels.Administration.Invoices;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    // Every word of the emails about a booking and its deposit. The services decide when each is
    // sent, and what goes on the job's history when one is not (PROJECT_STATE.md Section 3di).
    // What the customer typed is encoded before it goes into an email's HTML.
    public class BookingEmails : IBookingEmails
    {
        // On both notices to the company: their Reply-To is the customer's address.
        private const string ReplyTip = "<em>Tip: Pressing &quot;Reply&quot; in your email inbox replies directly to the customer.</em>";

        private readonly IEmailSender emailSender;
        private readonly EmailOptions settings;
        private readonly ILogger<BookingEmails> logger;

        public BookingEmails(
            IEmailSender emailSender,
            IOptions<EmailOptions> settings,
            ILogger<BookingEmails> logger)
        {
            this.emailSender = emailSender;
            this.settings = settings.Value;
            this.logger = logger;
        }

        // The one customer email that names the technician, sent when an admin picks one. It used
        // to hang off the "Approve" button, which a paid booking never showed, so no customer was
        // ever sent it (PROJECT_STATE.md Section 3ce). The assignment is saved by now: a send that
        // fails is logged and reported back, so the admin's page can say the customer still has
        // to be told.
        public async Task<bool> TechnicianPickedAsync(Booking booking, Technician technician, IReadOnlyList<string> serviceNames)
        {
            var technicianName = EmailText.Encode(NameFormat.Full(technician.FirstName, technician.LastName));
            DateTime? start = booking.ScheduledStart ?? booking.AvailabilitySlot?.StartTime;
            var visit = start.HasValue
                ? $" on <strong>{start.Value:dd MMM yyyy 'at' HH:mm}</strong>"
                : string.Empty;
            var contact = !string.IsNullOrWhiteSpace(technician.PhoneNumber)
                ? "Direct Contact: " + EmailLayout.Link("tel:" + EmailText.PhoneLink(technician.PhoneNumber), EmailText.Encode(technician.PhoneNumber))
                : null;

            var subject = "Your technician for your Plumbing Handyman Surrey booking";
            var body = EmailLayout.ForCustomer(
                EmailLayout.Badge(EmailColour.Blue, "&#128295; Technician Allocated")
                + EmailLayout.Heading($"Hi {EmailText.Encode(booking.CustomerFirstName)},")
                + EmailLayout.Lead($"Your technician for your visit{visit} is <strong>{technicianName}</strong>.")
                + EmailLayout.Spotlight("Assigned Specialist", technicianName, contact)
                + EmailLayout.Text($"<strong>{technicianName} will call you before the visit</strong> to ask for further job-specific information if needed.")
                + EmailLayout.Details(
                    EmailLayout.ReferenceRow(BookingReference.Short(booking.Id)),
                    EmailLayout.Row("Service(s)", EmailText.Encode(string.Join(", ", serviceNames))),
                    EmailLayout.PlainRow("Address", EmailText.Encode(booking.Address)))
                + EmailLayout.Text($"{technicianName} will arrive equipped with all required professional tools. If you have parking restrictions, gate codes, or need to send photos of the work area, please reply to this email or send them on WhatsApp.")
                + EmailLayout.WhatsAppButton("Message Us on WhatsApp"));

            return await this.emailSender.TrySendEmailAsync(
                this.logger,
                "technician picked, to the customer",
                this.settings.BookingsFromAddress,
                EmailOptions.CustomerFromName,
                booking.Email,
                subject,
                body);
        }

        // The first email a customer gets: nothing is sent before the deposit is paid
        // (BookingsService.CreateBookingAsync). The deposit is paid and the booking approved by
        // now, so the send is not allowed to throw: a failure here used to end the customer's
        // return from Stripe on an error page, straight after paying (PROJECT_STATE Section 3cb).
        //
        // No technician line here on purpose. A technician is picked by an admin after the
        // deposit clears, so this email would always read "Not yet assigned" - which looks
        // unfinished to the customer. The technician's name and number go out in an email of
        // their own, when one is picked (TechnicianPickedAsync).
        public async Task<bool> DepositPaidToCustomerAsync(Booking booking, Payment payment)
        {
            var deposit = $"&pound;{payment.Amount:F2}";
            var subject = "Your Plumbing Handyman Surrey Booking is Confirmed!";
            var body = EmailLayout.ForCustomer(
                EmailLayout.Badge(EmailColour.Green, "&#10003; Deposit Received &amp; Booking Confirmed")
                + EmailLayout.Heading($"Hi {EmailText.Encode(booking.CustomerFirstName)},")
                + EmailLayout.Lead($"Great news! Your deposit of <strong>{deposit}</strong> has been received via Stripe and your booking is now locked in our schedule.")
                + EmailLayout.Details(
                    EmailLayout.ReferenceRow(BookingReference.Short(booking.Id)),
                    EmailLayout.Row("Service(s)", ServiceNames(booking)),
                    EmailLayout.TimeRow("Scheduled Time", ScheduledTime(booking)),
                    EmailLayout.PlainRow("Address", EmailText.Encode(booking.Address)),
                    EmailLayout.Divider(),
                    EmailLayout.MoneyRow("Deposit Paid", $"{deposit} (Paid)"))
                + EmailLayout.Text("<strong>What happens next?</strong><br />We will send you a follow-up email with your assigned technician's name and direct contact number as soon as one is allocated. If you need to send photos of the job or additional directions, feel free to message us on WhatsApp below.")
                + EmailLayout.WhatsAppButton("Message Us on WhatsApp")
                + CancellationNote(deposit, this.settings.SiteUrl));

            return await this.emailSender.TrySendEmailAsync(
                this.logger,
                "deposit paid, to the customer",
                this.settings.BookingsFromAddress,
                EmailOptions.CustomerFromName,
                booking.Email,
                subject,
                body);
        }

        public async Task<bool> DepositPaidToCompanyAsync(Booking booking, Payment payment)
        {
            var deposit = $"&pound;{payment.Amount:F2}";
            var subject = $"New Confirmed Booking - {booking.CustomerFirstName} {booking.CustomerLastName}";
            var body = EmailLayout.ForCompany(
                EmailLayout.Badge(EmailColour.Green, "&#128276; New Customer Booking")
                + EmailLayout.Heading("A booking deposit has just been paid.")
                + EmailLayout.Lead("A customer has completed payment for their booking deposit via Stripe. The slot is confirmed in the schedule.")
                + EmailLayout.Details(
                    EmailLayout.ReferenceRow(BookingReference.Short(booking.Id)),
                    EmailLayout.Row("Customer", CustomerName(booking)),
                    EmailLayout.PlainRow("Customer Contact", ContactLinks(booking)),
                    EmailLayout.Row("Service(s)", ServiceNames(booking)),
                    EmailLayout.TimeRow("Scheduled Time", ScheduledTime(booking)),
                    EmailLayout.PlainRow("Service Address", EmailText.Encode(booking.Address)),
                    EmailLayout.Divider(),
                    EmailLayout.MoneyRow("Deposit Paid", $"{deposit} (Stripe)"))
                + EmailLayout.Advice($"<strong>Next Step:</strong> Open the booking in the admin panel and assign a technician. The customer will be sent an automated notification with the technician's name as soon as you save.<br /><br />{ReplyTip}")
                + EmailLayout.Button("&#9881;&#65039; Open Booking in Admin Panel", this.JobPage(booking)));

            // Reply-To is the customer, so pressing Reply on this notice answers them.
            return await this.emailSender.TrySendEmailAsync(
                this.logger,
                "deposit paid, notice to the company",
                this.settings.SystemFromAddress,
                EmailOptions.WebsiteFromName,
                this.settings.AdminNotificationAddress,
                subject,
                body,
                replyTo: booking.Email);
        }

        // To the company only. The customer is not told the booking is confirmed, because it is
        // not: someone has to ring them, and either find a new time or send the money back. The
        // time is the one the job was booked for, since its hour may have gone back on sale.
        public async Task<bool> DepositForBookingNotHeldAsync(Booking booking, Payment payment)
        {
            var scheduledTime = booking.ScheduledStart.HasValue
                ? booking.ScheduledStart.Value.ToString("dd MMM yyyy 'at' HH:mm")
                : "not recorded";

            var subject = $"Action needed: deposit paid for a booking that is not held - {booking.CustomerFirstName} {booking.CustomerLastName}";
            var body = EmailLayout.ForCompany(
                EmailLayout.Badge(EmailColour.Amber, "&#9888;&#65039; Action Needed")
                + EmailLayout.Heading("A deposit was paid for a booking that was not waiting for one.")
                + EmailLayout.Lead("The booking had been dropped or cancelled, or was already paid, when this money arrived. The site has written the payment on the job and changed nothing else. The customer has not been sent a confirmation.")
                + EmailLayout.Details(
                    EmailLayout.ReferenceRow(BookingReference.Short(booking.Id)),
                    EmailLayout.Row("Customer", CustomerName(booking)),
                    EmailLayout.PlainRow("Customer Contact", ContactLinks(booking)),
                    EmailLayout.TimeRow("It was booked for", scheduledTime),
                    EmailLayout.Divider(),
                    EmailLayout.MoneyRow("Deposit Paid", $"&pound;{payment.Amount:F2}"))
                + EmailLayout.Advice($"<strong>Next Step:</strong> Call the customer. The site has told them you will be in touch to confirm their visit. Either write the job in again for a time that is free, or send the deposit back in Stripe.<br /><br />{ReplyTip}")
                + EmailLayout.Button("&#9881;&#65039; Open Booking in Admin Panel", this.JobPage(booking)));

            return await this.emailSender.TrySendEmailAsync(
                this.logger,
                "deposit paid for a booking not held, notice to the company",
                this.settings.SystemFromAddress,
                EmailOptions.WebsiteFromName,
                this.settings.AdminNotificationAddress,
                subject,
                body,
                replyTo: booking.Email);
        }

        // Sent by the admin from the job's page, once the work is done; sent again after a later
        // payment, it reads "paid in full". The invoice itself is the PDF: the email says what
        // it holds in a few lines, for a customer who reads it on a phone.
        public async Task<bool> InvoiceAsync(string customerFirstName, InvoiceViewModel invoice, byte[] pdf, string paymentDetails)
        {
            var number = EmailText.Encode(invoice.NumberText);
            var balance = $"&pound;{invoice.Balance:F2}";
            var dueOn = invoice.DueOn.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

            var subject = $"Your invoice {invoice.NumberText} from Plumbing Handyman Surrey";
            var body = EmailLayout.ForCustomer(
                (invoice.IsPaid
                    ? EmailLayout.Badge(EmailColour.Green, "&#10003; Paid in Full")
                    : EmailLayout.Badge(EmailColour.Blue, "&#128196; Your Invoice"))
                + EmailLayout.Heading($"Hi {EmailText.Encode(customerFirstName)},")
                + EmailLayout.Lead(invoice.IsPaid
                    ? $"Thank you for choosing us. Your invoice <strong>{number}</strong> is attached, and it is paid in full."
                    : $"Thank you for choosing us. Your invoice <strong>{number}</strong> is attached. The balance of <strong>{balance}</strong> is due by <strong>{dueOn}</strong>.")
                + EmailLayout.Details(
                    EmailLayout.ReferenceRow(invoice.JobReference),
                    EmailLayout.Row("Invoice", number),
                    EmailLayout.Row("Service(s)", EmailText.Encode(invoice.Services)),
                    EmailLayout.Divider(),
                    EmailLayout.MoneyRow("Total", $"&pound;{invoice.Total:F2}"),
                    EmailLayout.MoneyRow("Paid", $"&pound;{invoice.Paid:F2}"),
                    EmailLayout.MoneyRow(invoice.IsPaid ? "Balance" : "Balance Due", invoice.IsPaid ? "&pound;0.00 (Paid)" : balance))
                + (invoice.IsPaid ? string.Empty : EmailLayout.Text($"<strong>How to pay:</strong> {EmailText.Encode(paymentDetails)}"))
                + EmailLayout.Text("Any questions about your invoice? Reply to this email or message us on WhatsApp.")
                + EmailLayout.WhatsAppButton("Message Us on WhatsApp"));

            var attachment = new EmailAttachment
            {
                Content = pdf,
                FileName = $"Invoice {invoice.NumberText}.pdf",
                MimeType = "application/pdf",
            };

            return await this.emailSender.TrySendEmailAsync(
                this.logger,
                "invoice, to the customer",
                this.settings.BookingsFromAddress,
                EmailOptions.CustomerFromName,
                invoice.Email,
                subject,
                body,
                attachments: new[] { attachment });
        }

        private static string CustomerName(Booking booking)
        {
            return EmailText.Encode($"{booking.CustomerFirstName} {booking.CustomerLastName}");
        }

        // The customer's phone number and email address, each as a link the company can press.
        private static string ContactLinks(Booking booking)
        {
            return EmailLayout.Link("tel:" + EmailText.PhoneLink(booking.PhoneNumber), EmailText.Encode(booking.PhoneNumber))
                + " &bull; "
                + EmailLayout.Link("mailto:" + EmailText.Encode(booking.Email), EmailText.Encode(booking.Email));
        }

        // The job's services in one line, as both deposit emails show them. The booking has to be
        // loaded with its services for the names to be there.
        private static string ServiceNames(Booking booking)
        {
            var names = booking.BookingServices != null
                ? string.Join(", ", booking.BookingServices.Select(x => x.Service?.Name).Where(name => !string.IsNullOrWhiteSpace(name)))
                : string.Empty;

            return EmailText.Encode(names);
        }

        // Both deposit emails give the hour the customer picked on the website.
        private static string ScheduledTime(Booking booking)
        {
            return booking.AvailabilitySlot != null
                ? booking.AvailabilitySlot.StartTime.ToString("dd MMM yyyy 'at' HH:mm")
                : "To be confirmed";
        }

        // Section 4 of the Terms & Conditions page (Views/Home/Terms.cshtml), word for word, so the
        // customer has the cancellation rules in writing the moment the deposit is taken. The page
        // and this note say the same thing: change them together.
        private static string CancellationNote(string deposit, string siteUrl)
        {
            const string Free = "<span style=\"color: #16a34a; font-weight: 700;\">&#10003;</span>";
            const string Forfeited = "<span style=\"color: #dc2626; font-weight: 700;\">&#10007;</span>";

            var rules = "To cancel or reschedule, call or email us."
                + $"<div style=\"margin-top: 10px;\">{Free} <strong>More than 24 hours before:</strong> cancelling or rescheduling is free. Your deposit is refunded in full to your card, or moved to the new appointment.</div>"
                + $"<div style=\"margin-top: 8px;\">{Forfeited} <strong>Within 24 hours of the appointment:</strong> the {deposit} deposit is forfeited.</div>"
                + $"<div style=\"margin-top: 8px;\">{Free} <strong>If we have to cancel:</strong> we'll offer you a new time or refund your deposit in full.</div>"
                + $"<div style=\"margin-top: 10px;\">Full terms: {EmailLayout.Link(siteUrl + "/TermsAndConditions", "Terms &amp; Conditions")}</div>";

            return EmailLayout.Note("Cancellations &amp; Rescheduling", rules);
        }

        // The job's own page in the admin panel of the site that sent the notice: staging's
        // notices open staging, the live site's open the live site.
        private string JobPage(Booking booking)
        {
            return $"{this.settings.SiteUrl}/Administration/Bookings/Details/{booking.Id}";
        }
    }
}
