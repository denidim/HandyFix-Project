namespace HandyFix.Web.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    using HandyFix.Common;
    using HandyFix.Data;
    using HandyFix.Data.Models;
    using HandyFix.Services.Messaging;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    using Xunit;

    // The Job Book through the whole stack, on a relational database as the live site has: a job
    // booked on the website and a job written in by the admin, from the day it arrives to the
    // day it is paid for (PROJECT_STATE.md Section 3ce). Which emails go out and when, what the
    // job's page offers and answers, and what each step leaves in the calendar. Each test has a
    // site and a database of its own, because each one takes slots and counts the emails sent.
    public class AdminBookingsWebTests
    {
        private const string TechnicianEmailSubject = "Your technician for your Plumbing Handyman Surrey booking";

        private const string JobsPage = "/Administration/Bookings";

        // The email that used to go out before paying promised a visit that was dropped fifteen
        // minutes later if the deposit never came. Followed here one step at a time, as a browser
        // goes: the form, the payment, the way back.
        [Fact]
        public async Task ACustomerIsEmailedNothingUntilTheDepositIsPaidAndThenOnce()
        {
            using var site = new Site();
            HttpClient customer = site.Browser(followRedirects: false);
            AvailabilitySlot slot = site.AFreeSlot();

            HttpResponseMessage sent = await FormsWebTests.PostFormAsync(customer, "/Booking", site.BookingFormFor(slot));

            Assert.Equal(HttpStatusCode.Redirect, sent.StatusCode);
            Assert.StartsWith("/Payment/Pay?bookingId=", sent.Headers.Location.OriginalString);
            Assert.Equal("Pending", site.Booking().Status.Name);
            Assert.Empty(site.Emails.Sent);

            HttpResponseMessage paid = await customer.GetAsync(sent.Headers.Location);
            HttpResponseMessage back = await customer.GetAsync(paid.Headers.Location);

            Booking booking = site.Booking();
            Assert.Equal("/Booking/Confirmed/" + booking.Id, back.Headers.Location.OriginalString);
            Assert.Equal("Approved", booking.Status.Name);
            Assert.Equal(2, site.Emails.Sent.Count);

            RecordingEmailSender.Email toCustomer = site.Emails.Sent.Single(e => e.To == "ada@example.com");
            Assert.Equal("Your Plumbing Handyman Surrey Booking is Confirmed!", toCustomer.Subject);
            Assert.Contains(">#" + BookingReference.Short(booking.Id) + "</td>", toCustomer.Body);
            Assert.DoesNotContain(booking.Id.ToString(), toCustomer.Body);

            RecordingEmailSender.Email toCompany = site.Emails.Sent.Single(e => e.To == GlobalConstants.BusinessEmail);
            Assert.Equal("New Confirmed Booking - Ada Lovelace", toCompany.Subject);
        }

        // The page the customer lands on after paying. It showed all 36 characters of the
        // booking's id, a two-hour window for a one-hour slot, and a promise of a text message
        // from a line that cannot send one.
        [Fact]
        public async Task TheCustomersConfirmationPageShowsTheShortReferenceAndTheSlotsOwnHour()
        {
            using var site = new Site();
            AvailabilitySlot slot = site.AFreeSlot();

            Booking booking = await site.BookAndPayAsync(slot);

            var page = WebUtility.HtmlDecode(await (await site.Browser().GetAsync("/Booking/Confirmed/" + booking.Id)).Content.ReadAsStringAsync());
            Assert.Contains(">#" + BookingReference.Short(booking.Id) + "</p>", page);
            Assert.DoesNotContain(booking.Id.ToString(), page);
            Assert.Contains(">" + slot.StartTime.ToString("hh:mm tt") + " - " + slot.EndTime.ToString("hh:mm tt") + "</p>", page);
            Assert.DoesNotContain(slot.StartTime.AddHours(2).ToString("hh:mm tt"), page);
            Assert.DoesNotContain("text you", page);
            Assert.Contains("We'll email you your technician's name and phone number as soon as one is assigned.", page);
        }

        // The email naming the technician used to hang off the "Approve" button, which a paid
        // booking never showed, so it could not be sent. It goes out when the admin picks one,
        // and the page says it went.
        [Fact]
        public async Task PickingATechnicianOnAPaidBookingEmailsTheCustomerAndSaysSo()
        {
            using var site = new Site();
            Booking booking = await site.BookAndPayAsync(site.AFreeSlot());
            HttpClient admin = await site.SignedInAdminAsync();
            var jobPage = JobPage(booking.Id);

            var opened = WebUtility.HtmlDecode(await (await admin.GetAsync(jobPage)).Content.ReadAsStringAsync());
            Assert.Contains("<select name=\"technicianId\"", opened);
            Assert.Contains("Saving a new technician emails the customer that technician's name and phone number.", opened);
            Assert.Equal(("Booked", "Deposit paid"), LabelsOn(opened));
            Assert.Contains("Job #" + BookingReference.Short(booking.Id), opened);
            Assert.Contains("<span>" + UkTime.FromUtc(booking.CreatedOn).ToString("hh:mm tt") + "</span>", opened);
            Assert.DoesNotContain("Approve Booking", opened);
            Assert.DoesNotContain("/Bookings/Approve", opened);

            HttpResponseMessage picked = await PostFromAsync(admin, jobPage, JobsPage + "/AssignTechnician", new Dictionary<string, string>
            {
                ["id"] = booking.Id.ToString(),
                ["technicianId"] = site.LaunchTechnicianId().ToString(),
            });

            var answered = WebUtility.HtmlDecode(await picked.Content.ReadAsStringAsync());
            Assert.Equal(jobPage, picked.RequestMessage.RequestUri.AbsolutePath);
            Assert.Contains("Zapryan is now the technician for this job. The customer has been emailed the name and phone number.", answered);

            // The technician is read in the job's own details, with the number to reach them on,
            // and not only in the picker that changes it.
            Assert.Matches("id=\"booking-technician\">\\s*<label[^>]*>Technician</label>\\s*<p[^>]*>\\s*<span[^>]*>engineering</span>\\s*<span>Zapryan</span>", answered);
            Assert.Matches("id=\"booking-technician\">[\\s\\S]*?<span>" + Regex.Escape(GlobalConstants.BusinessPhone) + "</span>", answered);
            Assert.Matches("id=\"booking-technician\">[\\s\\S]*?<span>Not picked yet</span>", opened);

            RecordingEmailSender.Email email = Assert.Single(site.Emails.Sent, e => e.Subject == TechnicianEmailSubject);
            Assert.Equal("ada@example.com", email.To);
            Assert.Contains("<strong>Zapryan</strong>", email.Body);
            Assert.Matches("<a href=\"tel:" + GlobalConstants.BusinessPhone.Replace(" ", string.Empty) + "\"[^>]*>" + Regex.Escape(GlobalConstants.BusinessPhone) + "</a>", email.Body);
            Assert.Contains(BookingReference.Short(booking.Id), email.Body);
            Assert.Contains(site.ServiceName, email.Body);
            Assert.Contains("1 Analytical Engine Way, Chessington, KT9 1AA", email.Body);

            // Saved again as it stands: nothing to tell the customer a second time.
            HttpResponseMessage again = await PostFromAsync(admin, jobPage, JobsPage + "/AssignTechnician", new Dictionary<string, string>
            {
                ["id"] = booking.Id.ToString(),
                ["technicianId"] = site.LaunchTechnicianId().ToString(),
            });

            Assert.Contains("Nothing was changed: Zapryan was already the technician. No email was sent.", WebUtility.HtmlDecode(await again.Content.ReadAsStringAsync()));
            Assert.Single(site.Emails.Sent, e => e.Subject == TechnicianEmailSubject);
        }

        // An unpaid booking is dropped after about fifteen minutes, so there is no job to send
        // anyone to. The page offers no picker, no move, no "done" and no payment; any of them
        // sent from a page that was opened earlier is refused, and the customer is not emailed.
        [Fact]
        public async Task ABookingWaitingForItsDepositOffersOnlyCancelAndRefusesTheRestSentAnyway()
        {
            using var site = new Site();
            HttpClient customer = site.Browser(followRedirects: false);
            await FormsWebTests.PostFormAsync(customer, "/Booking", site.BookingFormFor(site.AFreeSlot()));
            Booking booking = site.Booking();
            HttpClient admin = await site.SignedInAdminAsync();
            var jobPage = JobPage(booking.Id);
            var id = new Dictionary<string, string> { ["id"] = booking.Id.ToString() };

            var opened = WebUtility.HtmlDecode(await (await admin.GetAsync(jobPage)).Content.ReadAsStringAsync());
            Assert.Equal(("Waiting for deposit", "Not paid"), LabelsOn(opened));
            Assert.Matches("<dt>Still owed</dt>\\s*<dd>£", opened);
            Assert.DoesNotContain("name=\"technicianId\"", opened);
            Assert.Contains("A technician can be picked once the deposit is paid.", opened);
            Assert.DoesNotContain("id=\"job-complete\"", opened);
            Assert.DoesNotContain("id=\"job-move\"", opened);
            Assert.DoesNotContain("name=\"amount\"", opened);
            Assert.Contains("It can be moved, or marked as done, once its deposit is paid.", opened);
            Assert.Contains("A payment can be written here once the deposit is paid on the website.", opened);
            Assert.Contains("id=\"job-cancel\"", opened);
            Assert.Contains("Not yet, deposit not paid", opened);

            // It read "Booked" and "Not paid", exactly as a job the admin had written in does,
            // and the two could not be told apart in the list. It has its own label now, its own
            // choice in the filter, and the hour it holds says so in the calendar
            // (PROJECT_STATE.md Section 3ch).
            var reference = "#" + BookingReference.Short(booking.Id);
            var list = WebUtility.HtmlDecode(await (await admin.GetAsync(JobsPage)).Content.ReadAsStringAsync());
            Assert.Matches("job-badge-two-lines tint-waiting\">Waiting for deposit</span>", list);

            var waiting = WebUtility.HtmlDecode(await (await admin.GetAsync(JobsPage + "?status=Waiting%20for%20deposit")).Content.ReadAsStringAsync());
            Assert.Contains(reference, waiting);

            var booked = WebUtility.HtmlDecode(await (await admin.GetAsync(JobsPage + "?status=Booked")).Content.ReadAsStringAsync());
            Assert.DoesNotContain(reference, booked);

            var day = WebUtility.HtmlDecode(await (await admin.GetAsync("/Administration/Calendar?date=" + booking.ScheduledStart.Value.ToString("yyyy-MM-dd"))).Content.ReadAsStringAsync());
            Assert.Matches("slot-tag tint-waiting mb-1\">Waiting for deposit</span>", day);
            Assert.Matches("status-badge tint-waiting\">Waiting for deposit</span>", day);

            HttpResponseMessage picked = await PostFromAsync(admin, jobPage, JobsPage + "/AssignTechnician", With(id, "technicianId", site.LaunchTechnicianId().ToString()));
            Assert.Contains("Nothing was changed. A technician can be picked while the job is booked, and on a website booking only once its deposit is paid.", WebUtility.HtmlDecode(await picked.Content.ReadAsStringAsync()));
            Assert.Null(site.Booking().TechnicianId);

            HttpResponseMessage done = await PostFromAsync(admin, jobPage, JobsPage + "/Complete", With(id, "finalPrice", "135.00"));
            Assert.Contains("Nothing was changed. A job can be marked as done while it is booked, and a website booking only once its deposit is paid.", WebUtility.HtmlDecode(await done.Content.ReadAsStringAsync()));
            Assert.Equal("Pending", site.Booking().Status.Name);

            HttpResponseMessage paid = await PostFromAsync(admin, jobPage, JobsPage + "/AddPayment", With(With(id, "amount", "50.00"), "method", "Cash"));
            Assert.Contains("Nothing was changed. A payment can be written on a job that is booked or done", WebUtility.HtmlDecode(await paid.Content.ReadAsStringAsync()));

            HttpResponseMessage moved = await PostFromAsync(admin, jobPage, JobsPage + "/Move", With(With(id, "date", "2030-01-07"), "time", "10:00"));
            Assert.Contains("Nothing was changed. A job can be moved while it is booked", WebUtility.HtmlDecode(await moved.Content.ReadAsStringAsync()));

            Assert.Empty(site.Emails.Sent);
        }

        // The whole of a website booking's life on its page: the deposit, the technician, the
        // final price, the rest of the money, and the line each one leaves at the foot.
        [Fact]
        public async Task AWebsiteBookingGoesFromBookedToDoneToPaidInFullOnItsPage()
        {
            using var site = new Site();
            Booking booking = await site.BookAndPayAsync(site.AFreeSlot());
            HttpClient admin = await site.SignedInAdminAsync();
            var jobPage = JobPage(booking.Id);
            var id = new Dictionary<string, string> { ["id"] = booking.Id.ToString() };

            await PostFromAsync(admin, jobPage, JobsPage + "/AssignTechnician", With(id, "technicianId", site.LaunchTechnicianId().ToString()));

            // Done, with what it came to: an hour and one 30-minute block at £90 an hour.
            HttpResponseMessage done = await PostFromAsync(admin, jobPage, JobsPage + "/Complete", With(id, "finalPrice", "135.00"));
            var page = WebUtility.HtmlDecode(await done.Content.ReadAsStringAsync());
            Assert.Contains("The job is marked as done, at a final price of £135.00.", page);
            Assert.Equal(("Done", "Deposit paid"), LabelsOn(page));
            Assert.Matches("<dt>Final price</dt>\\s*<dd>£135.00</dd>", page);
            Assert.Matches("<dt>Paid so far</dt>\\s*<dd>£50.00</dd>", page);
            Assert.Matches("<dt>Still owed</dt>\\s*<dd>£85.00</dd>", page);
            Assert.Contains("Deposit, card on the website", page);

            // The box for the next payment starts at what is owed.
            Assert.Matches("name=\"amount\"[^>]*value=\"85.00\"", page);
            Assert.DoesNotContain("id=\"job-complete\"", page);
            Assert.DoesNotContain("id=\"job-cancel\"", page);
            Assert.DoesNotContain("id=\"job-move\"", page);
            Assert.DoesNotContain("name=\"technicianId\"", page);
            Assert.Contains("id=\"job-final-price\"", page);

            // Part of the rest in cash: more is still owed, and the label says so.
            HttpResponseMessage part = await PostFromAsync(admin, jobPage, JobsPage + "/AddPayment", With(With(id, "amount", "40.00"), "method", "Cash"));
            page = WebUtility.HtmlDecode(await part.Content.ReadAsStringAsync());
            Assert.Contains("£40.00 is written on the job, paid by cash.", page);
            Assert.Equal(("Done", "Part paid"), LabelsOn(page));
            Assert.Matches("<dt>Still owed</dt>\\s*<dd>£45.00</dd>", page);

            // The rest by card: nothing is owed.
            HttpResponseMessage rest = await PostFromAsync(admin, jobPage, JobsPage + "/AddPayment", With(With(id, "amount", "45.00"), "method", "Card"));
            page = WebUtility.HtmlDecode(await rest.Content.ReadAsStringAsync());
            Assert.Equal(("Done", "Paid in full"), LabelsOn(page));
            Assert.Matches("<dt>Paid so far</dt>\\s*<dd>£135.00</dd>", page);
            Assert.Matches("<dt>Still owed</dt>\\s*<dd>£0.00</dd>", page);

            // The history at the foot of the page, oldest first.
            var history = Regex.Matches(page.Substring(page.IndexOf("id=\"job-history\"", StringComparison.Ordinal)), "<span class=\"job-ledger-what text-primary\">([^<]*)</span>")
                .Select(m => m.Groups[1].Value)
                .ToList();
            Assert.Equal(
                new[]
                {
                    "Booked on the website.",
                    "Deposit of £50.00 paid on the website.",
                    "Technician picked: Zapryan. The customer was emailed.",
                    "Marked done. Final price £135.00.",
                    "Payment of £40.00 written on the job: Cash.",
                    "Payment of £45.00 written on the job: Card.",
                },
                history);

            // The list says the same in a line.
            var list = WebUtility.HtmlDecode(await (await admin.GetAsync(JobsPage)).Content.ReadAsStringAsync());
            Assert.Contains(">Done</span>", list);
            Assert.Contains("<span>Paid in full</span>", list);
            Assert.Contains("<span>£135.00</span>", list);

            // The cash line was typed wrongly: it comes off, and the job owes that much again.
            Guid cash = site.Read(dbContext => dbContext.Payments.Single(x => x.Method == "Cash").Id);
            HttpResponseMessage off = await PostFromAsync(admin, jobPage, JobsPage + "/RemovePayment", With(id, "paymentId", cash.ToString()));
            page = WebUtility.HtmlDecode(await off.Content.ReadAsStringAsync());
            Assert.Contains("The payment is taken off the job.", page);
            Assert.Equal(("Done", "Part paid"), LabelsOn(page));
            Assert.Matches("<dt>Still owed</dt>\\s*<dd>£40.00</dd>", page);

            // The final price was typed wrongly too.
            HttpResponseMessage price = await PostFromAsync(admin, jobPage, JobsPage + "/FinalPrice", With(id, "finalPrice", "95.00"));
            page = WebUtility.HtmlDecode(await price.Content.ReadAsStringAsync());
            Assert.Contains("The final price is now £95.00.", page);
            Assert.Equal(("Done", "Paid in full"), LabelsOn(page));

            // Notes, for the admin alone.
            HttpResponseMessage noted = await PostFromAsync(admin, jobPage, JobsPage + "/Notes", With(id, "notes", "Stopcock is under the kitchen sink."));
            page = WebUtility.HtmlDecode(await noted.Content.ReadAsStringAsync());
            Assert.Contains("The notes are saved. Only the admin sees them.", page);
            Assert.Matches("<textarea name=\"notes\"[^>]*>\\s*Stopcock is under the kitchen sink.</textarea>", page);
            var confirmation = await (await site.Browser().GetAsync("/Booking/Confirmed/" + booking.Id)).Content.ReadAsStringAsync();
            Assert.DoesNotContain("Stopcock", confirmation);
        }

        // "Cancel Booking" used to be offered on a completed booking. Pressed there, it would
        // have relabelled a finished job and put its hour back on sale.
        [Fact]
        public async Task ADoneJobCannotBeCancelledFromAPageOpenedBeforeItWasDone()
        {
            using var site = new Site();
            AvailabilitySlot slot = site.AFreeSlot();
            Booking booking = await site.BookAndPayAsync(slot);
            HttpClient admin = await site.SignedInAdminAsync();
            var openedBefore = await (await admin.GetAsync(JobPage(booking.Id))).Content.ReadAsStringAsync();
            var id = new Dictionary<string, string> { ["id"] = booking.Id.ToString() };

            await PostToAsync(admin, openedBefore, JobsPage + "/Complete", With(id, "finalPrice", "90.00"));
            Assert.Equal("Completed", site.Booking().Status.Name);

            // The page no longer has the button, so its form is sent from the copy of the page
            // that was opened before the job was done, as a second tab would send it.
            HttpResponseMessage cancelled = await PostToAsync(admin, openedBefore, JobsPage + "/Cancel", With(id, "reason", "Pressed in the other tab."));

            Assert.Contains("Nothing was changed. This job is already done, cancelled or abandoned.", WebUtility.HtmlDecode(await cancelled.Content.ReadAsStringAsync()));
            Assert.Equal("Completed", site.Booking().Status.Name);
            Assert.True(site.Slot(slot.Id).IsBooked);
        }

        // A cancelled booking used to lose its date: the list showed "Jan 01, 0001" for it. It
        // keeps the day it was for and the reason, and its deposit can be ticked as refunded.
        [Fact]
        public async Task ACancelledJobKeepsItsDateAndReasonAndItsDepositCanBeTickedAsRefunded()
        {
            using var site = new Site();
            AvailabilitySlot slot = site.AFreeSlot();
            Booking booking = await site.BookAndPayAsync(slot);
            HttpClient admin = await site.SignedInAdminAsync();
            var jobPage = JobPage(booking.Id);
            var id = new Dictionary<string, string> { ["id"] = booking.Id.ToString() };
            var emailsBefore = site.Emails.Sent.Count;

            // No reason, no cancelling.
            HttpResponseMessage noReason = await PostFromAsync(admin, jobPage, JobsPage + "/Cancel", With(id, "reason", "   "));
            Assert.Contains("Nothing was changed. Write the reason for cancelling: it is kept on the job.", WebUtility.HtmlDecode(await noReason.Content.ReadAsStringAsync()));
            Assert.Equal("Approved", site.Booking().Status.Name);

            HttpResponseMessage cancelled = await PostFromAsync(admin, jobPage, JobsPage + "/Cancel", With(id, "reason", "The customer rang: a neighbour fixed the tap."));

            var page = WebUtility.HtmlDecode(await cancelled.Content.ReadAsStringAsync());
            Assert.Contains("The job is cancelled. It keeps its date and the reason, and an hour it held in the calendar can be booked again. The customer has not been emailed: please tell them yourself.", page);
            Assert.Contains("refunded by hand in Stripe", page);
            Assert.Equal(("Cancelled", "Deposit paid"), LabelsOn(page));
            Assert.Contains("The customer rang: a neighbour fixed the tap.", page);
            Assert.Contains("<span>" + slot.StartTime.ToString("dddd, MMMM dd, yyyy") + "</span>", page);
            Assert.DoesNotContain("No date kept", page);
            Assert.False(site.Slot(slot.Id).IsBooked);
            Assert.Equal(emailsBefore, site.Emails.Sent.Count);

            // Nothing else can be done here.
            Assert.DoesNotContain("name=\"technicianId\"", page);
            Assert.DoesNotContain("name=\"amount\"", page);
            Assert.Contains("Nothing more can be done on this job.", page);

            // The list shows the day it was for.
            var list = WebUtility.HtmlDecode(await (await admin.GetAsync(JobsPage)).Content.ReadAsStringAsync());
            Assert.Contains(slot.StartTime.ToString("ddd dd MMM yyyy"), list);
            Assert.DoesNotContain("Jan 01, 0001", list);

            // The tick: unticked until the money has gone back.
            Assert.Matches("<input type=\"checkbox\" name=\"refunded\" value=\"true\"(?![^>]*checked)", page);
            HttpResponseMessage ticked = await PostFromAsync(admin, jobPage, JobsPage + "/DepositRefunded", With(id, "refunded", "true"));
            page = WebUtility.HtmlDecode(await ticked.Content.ReadAsStringAsync());
            Assert.Contains("The deposit is marked as refunded. It no longer counts as money in.", page);
            Assert.Equal(("Cancelled", "Deposit refunded"), LabelsOn(page));
            Assert.Matches("<input type=\"checkbox\" name=\"refunded\" value=\"true\"[^>]*checked", page);
            Assert.Contains("Deposit, card on the website (refunded)", page);
            Assert.Matches("<dt>Paid so far</dt>\\s*<dd>£0.00</dd>", page);

            // A browser sends nothing for a box that is not ticked.
            HttpResponseMessage unticked = await PostFromAsync(admin, jobPage, JobsPage + "/DepositRefunded", id);
            page = WebUtility.HtmlDecode(await unticked.Content.ReadAsStringAsync());
            Assert.Contains("The deposit is marked as not refunded.", page);
            Assert.Equal(("Cancelled", "Deposit paid"), LabelsOn(page));
        }

        // A job that came by phone: a first name, a number, a day and a time. It is a job from
        // the start, with no deposit, no slot and no email, and it is finished and paid for on
        // the same page as any other.
        [Fact]
        public async Task AJobWrittenInByTheAdminIsAJobFromTheStartAndEmailsNobody()
        {
            using var site = new Site();
            HttpClient admin = await site.SignedInAdminAsync();
            AvailabilitySlot slot = site.AFreeSlot();

            // The list has the way in.
            var list = WebUtility.HtmlDecode(await (await admin.GetAsync(JobsPage)).Content.ReadAsStringAsync());
            Assert.Contains("id=\"write-a-job-in\"", list);
            Assert.Contains("No jobs yet.", list);

            // A phone number is asked for; the rest of what the website's form demands is not.
            HttpResponseMessage refused = await FormsWebTests.PostFormAsync(admin, JobsPage + "/Create", new Dictionary<string, string>
            {
                ["CustomerFirstName"] = "Grace",
                ["PhoneNumber"] = string.Empty,
                ["Date"] = slot.StartTime.ToString("yyyy-MM-dd"),
                ["Time"] = slot.StartTime.ToString("HH:mm"),
                ["Source"] = "Phone",
            });
            Assert.Contains("Phone number is required.", WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync()));
            Assert.Equal(0, site.Read(dbContext => dbContext.Bookings.Count()));

            HttpResponseMessage written = await FormsWebTests.PostFormAsync(admin, JobsPage + "/Create", new Dictionary<string, string>
            {
                ["CustomerFirstName"] = "Grace",
                ["PhoneNumber"] = "07700 900456",
                ["Email"] = "grace@example.com",
                ["Date"] = slot.StartTime.ToString("yyyy-MM-dd"),
                ["Time"] = slot.StartTime.ToString("HH:mm"),
                ["Source"] = "Phone",
            });

            Booking job = site.Booking();
            var jobPage = JobPage(job.Id);
            var id = new Dictionary<string, string> { ["id"] = job.Id.ToString() };
            var page = WebUtility.HtmlDecode(await written.Content.ReadAsStringAsync());
            Assert.Equal(jobPage, written.RequestMessage.RequestUri.AbsolutePath);
            Assert.Contains("The job is written in. Its hour is still on sale on the website", page);
            Assert.Equal(("Booked", "Not paid"), LabelsOn(page));
            Assert.Contains("Grace · came from: Phone", page);
            Assert.Contains("No, no deposit", page);
            Assert.Contains("<span>" + slot.StartTime.ToString("hh:mm tt") + " - " + slot.StartTime.AddHours(1).ToString("hh:mm tt") + "</span>", page);
            Assert.Contains("Not written down", page);
            Assert.Contains("None picked", page);

            // The hour it is at stays on sale: the admin blocks it by hand.
            Assert.False(site.Slot(slot.Id).IsBooked);
            Assert.Null(site.Slot(slot.Id).BookingId);

            // A technician can be picked at once, and nobody is emailed about it.
            Assert.Contains("<select name=\"technicianId\"", page);
            Assert.Contains("No email goes out for a job that was written in.", page);
            HttpResponseMessage picked = await PostFromAsync(admin, jobPage, JobsPage + "/AssignTechnician", With(id, "technicianId", site.LaunchTechnicianId().ToString()));
            Assert.Contains("Zapryan is now the technician for this job. No email goes out for a job that was written in: please tell the customer yourself.", WebUtility.HtmlDecode(await picked.Content.ReadAsStringAsync()));

            // The calendar shows it on its day, though it holds no slot there.
            var day = WebUtility.HtmlDecode(await (await admin.GetAsync("/Administration/Calendar?date=" + slot.StartTime.ToString("yyyy-MM-dd"))).Content.ReadAsStringAsync());
            var jobsThatDay = day.Substring(day.IndexOf("id=\"calendar-jobs\"", StringComparison.Ordinal));
            Assert.Contains("Grace", jobsThatDay);
            Assert.Contains("#" + BookingReference.Short(job.Id), day);
            Assert.Contains("Zapryan · Phone", day);
            Assert.Contains(JobPage(job.Id), day);

            // Done and paid in cash: no deposit ever came into it.
            await PostFromAsync(admin, jobPage, JobsPage + "/Complete", With(id, "finalPrice", "60.00"));
            HttpResponseMessage paid = await PostFromAsync(admin, jobPage, JobsPage + "/AddPayment", With(With(id, "amount", "60.00"), "method", "Cash"));
            page = WebUtility.HtmlDecode(await paid.Content.ReadAsStringAsync());
            Assert.Equal(("Done", "Paid in full"), LabelsOn(page));

            Assert.Empty(site.Emails.Sent);
        }

        // An enquiry that turns into work. The button opens the form with what the enquiry
        // holds, so the admin types the day and the time and nothing twice.
        [Fact]
        public async Task MakeThisAJobOpensTheFormWithTheEnquirysNamePhoneAndMessage()
        {
            using var site = new Site();
            HttpClient admin = await site.SignedInAdminAsync();
            Guid enquiryId = site.Write(dbContext =>
            {
                var enquiry = new Inquiry
                {
                    Name = "Jane Mary Doe",
                    Email = "jane.doe@example.com",
                    PhoneNumber = "07700 900123",
                    Message = "The stopcock under the sink will not turn at all.",
                };
                dbContext.Inquiries.Add(enquiry);
                return enquiry.Id;
            });

            var enquiryPage = WebUtility.HtmlDecode(await (await admin.GetAsync("/Administration/Enquiries/Details/" + enquiryId)).Content.ReadAsStringAsync());
            var button = Regex.Match(enquiryPage, "<a[^>]*id=\"make-this-a-job\"[^>]*>").Value;
            Assert.Contains("href=\"/Administration/Bookings/Create?enquiryId=" + enquiryId + "\"", button);

            var form = WebUtility.HtmlDecode(await (await admin.GetAsync(JobsPage + "/Create?enquiryId=" + enquiryId)).Content.ReadAsStringAsync());
            Assert.Matches("name=\"CustomerFirstName\"[^>]*value=\"Jane\"", form);
            Assert.Matches("name=\"CustomerLastName\"[^>]*value=\"Mary Doe\"", form);
            Assert.Matches("name=\"PhoneNumber\"[^>]*value=\"07700 900123\"", form);
            Assert.Matches("name=\"Email\"[^>]*value=\"jane.doe@example.com\"", form);
            Assert.Matches("<textarea[^>]*name=\"ProblemDescription\"[^>]*>\\s*The stopcock under the sink will not turn at all.</textarea>", form);
            Assert.Matches("<option value=\"Enquiry\" selected=\"selected\">Enquiry</option>", form);
            Assert.DoesNotContain("<option value=\"Website\"", form);
            Assert.Contains("/Administration/Enquiries/Details/" + enquiryId, form);

            // Written in from there, it says where it came from, and the enquiry is still there.
            // The boxes are sent as the page filled them in, with the day and the time added.
            HttpResponseMessage written = await FormsWebTests.PostFormAsync(admin, JobsPage + "/Create?enquiryId=" + enquiryId, new Dictionary<string, string>
            {
                ["CustomerFirstName"] = "Jane",
                ["CustomerLastName"] = "Mary Doe",
                ["PhoneNumber"] = "07700 900123",
                ["Email"] = "jane.doe@example.com",
                ["ProblemDescription"] = "The stopcock under the sink will not turn at all.",
                ["Source"] = "Enquiry",
                ["Date"] = DateTime.Today.AddDays(3).ToString("yyyy-MM-dd"),
                ["Time"] = "11:30",
            });

            var page = WebUtility.HtmlDecode(await written.Content.ReadAsStringAsync());
            Assert.Contains("Jane Mary Doe · came from: Enquiry", page);
            Assert.Contains("The stopcock under the sink will not turn at all.", page);
            Assert.Equal(1, site.Read(dbContext => dbContext.Inquiries.Count()));
        }

        // A form sent without the page can name the website. A "website" job made by hand would
        // have no deposit and would look, in the list, like a booking somebody had paid for.
        [Fact]
        public async Task AJobCannotBeWrittenInAsIfItCameFromTheWebsite()
        {
            using var site = new Site();
            HttpClient admin = await site.SignedInAdminAsync();

            HttpResponseMessage refused = await FormsWebTests.PostFormAsync(admin, JobsPage + "/Create", new Dictionary<string, string>
            {
                ["CustomerFirstName"] = "Grace",
                ["PhoneNumber"] = "07700 900456",
                ["Date"] = DateTime.Today.AddDays(3).ToString("yyyy-MM-dd"),
                ["Time"] = "11:30",
                ["Source"] = "Website",
            });

            Assert.Contains("Pick where the job came from.", WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync()));
            Assert.Equal(0, site.Read(dbContext => dbContext.Bookings.Count()));
        }

        // A website booking's deposit promised the time, so it carries its hour in the calendar
        // with it when it moves. A written-in job holds none and leaves the calendar alone.
        [Fact]
        public async Task MovingAJobSaysWhatHappenedToItsHours()
        {
            using var site = new Site();
            AvailabilitySlot oldSlot = site.AFreeSlot();
            Booking booking = await site.BookAndPayAsync(oldSlot);
            AvailabilitySlot newSlot = site.AFreeSlot();
            HttpClient admin = await site.SignedInAdminAsync();
            var jobPage = JobPage(booking.Id);
            var id = new Dictionary<string, string> { ["id"] = booking.Id.ToString() };
            var emailsBefore = site.Emails.Sent.Count;

            HttpResponseMessage moved = await PostFromAsync(admin, jobPage, JobsPage + "/Move", With(With(id, "date", newSlot.StartTime.ToString("yyyy-MM-dd")), "time", newSlot.StartTime.ToString("HH:mm")));

            var page = WebUtility.HtmlDecode(await moved.Content.ReadAsStringAsync());
            Assert.Contains("The job is moved to " + newSlot.StartTime.ToString("dddd d MMMM yyyy 'at' HH:mm", CultureInfo.InvariantCulture) + ". The hour it had is back on sale. The new hour is taken off sale. The customer has not been emailed: please tell them yourself.", page);
            Assert.Contains("<span>" + newSlot.StartTime.ToString("hh:mm tt") + " - " + newSlot.EndTime.ToString("hh:mm tt") + "</span>", page);
            Assert.False(site.Slot(oldSlot.Id).IsBooked);
            Assert.Equal(booking.Id, site.Slot(newSlot.Id).BookingId);
            Assert.Equal(emailsBefore, site.Emails.Sent.Count);

            // To six in the evening, where the calendar has no slot: it moves and holds nothing.
            DateTime evening = newSlot.StartTime.Date.AddHours(18);
            HttpResponseMessage late = await PostFromAsync(admin, jobPage, JobsPage + "/Move", With(With(id, "date", evening.ToString("yyyy-MM-dd")), "time", "18:00"));
            page = WebUtility.HtmlDecode(await late.Content.ReadAsStringAsync());
            Assert.Contains("The hour it had is back on sale. The new hour is not free in the calendar, so nothing was taken off sale", page);
            Assert.False(site.Slot(newSlot.Id).IsBooked);
            Assert.Equal(evening, site.Booking().ScheduledStart);

            // Sent again as it stands.
            HttpResponseMessage same = await PostFromAsync(admin, jobPage, JobsPage + "/Move", With(With(id, "date", evening.ToString("yyyy-MM-dd")), "time", "18:00"));
            Assert.Contains("Nothing was changed: the job is already at that day and time.", WebUtility.HtmlDecode(await same.Content.ReadAsStringAsync()));
        }

        [Fact]
        public async Task MovingAWrittenInJobLeavesTheCalendarAlone()
        {
            using var site = new Site();
            HttpClient admin = await site.SignedInAdminAsync();
            AvailabilitySlot free = site.AFreeSlot();
            Guid jobId = await site.WriteAJobInAsync(admin, free.StartTime.AddDays(1));
            var id = new Dictionary<string, string> { ["id"] = jobId.ToString() };

            HttpResponseMessage moved = await PostFromAsync(admin, JobPage(jobId), JobsPage + "/Move", With(With(id, "date", free.StartTime.ToString("yyyy-MM-dd")), "time", free.StartTime.ToString("HH:mm")));

            var page = WebUtility.HtmlDecode(await moved.Content.ReadAsStringAsync());
            Assert.Contains("Nothing was changed in the calendar: block the new hour by hand if nobody else should be booked then.", page);
            Assert.False(site.Slot(free.Id).IsBooked);
            Assert.Equal(free.StartTime, site.Booking().ScheduledStart);
        }

        // The one thing the Job Book left out: a phone number typed wrongly meant cancelling the
        // job and writing it in again. The job's page has a button, and the form behind it opens
        // with what the job holds (PROJECT_STATE.md Section 3cf).
        [Fact]
        public async Task AWrittenInJobsDetailsArePutRightOnAFormThatOpensFilledIn()
        {
            using var site = new Site();
            HttpClient admin = await site.SignedInAdminAsync();
            DateTime start = DateTime.Today.AddDays(4).AddHours(15);
            Guid jobId = await site.WriteAJobInAsync(admin, start);
            var jobPage = JobPage(jobId);
            var editPage = EditPage(jobId);
            Service service = site.Read(dbContext => dbContext.Services.OrderBy(x => x.Name).First());

            var opened = WebUtility.HtmlDecode(await (await admin.GetAsync(jobPage)).Content.ReadAsStringAsync());
            Assert.Contains("href=\"" + editPage + "\"", Regex.Match(opened, "<a[^>]*id=\"job-edit-details\"[^>]*>").Value);

            var form = WebUtility.HtmlDecode(await (await admin.GetAsync(editPage)).Content.ReadAsStringAsync());
            Assert.Contains("Edit the Details of Job #" + BookingReference.Short(jobId), form);
            Assert.Matches("name=\"CustomerFirstName\"[^>]*value=\"Grace\"", form);
            Assert.Matches("name=\"PhoneNumber\"[^>]*value=\"07700 900456\"", form);
            Assert.Matches("<option value=\"Agency\" selected=\"selected\">Agency</option>", form);
            Assert.DoesNotContain("<option value=\"Website\"", form);

            // The day and the time are "Move"'s to change, not this form's.
            Assert.DoesNotContain("name=\"Date\"", form);
            Assert.DoesNotContain("name=\"Time\"", form);

            var fields = new Dictionary<string, string>
            {
                ["Id"] = jobId.ToString(),
                ["CustomerFirstName"] = "Grace",
                ["CustomerLastName"] = "Hopper",
                ["PhoneNumber"] = "07700 900999",
                ["Address"] = "3 Navy Row, Epsom, KT17 1AA",
                ["ServiceId"] = service.Id.ToString(),
                ["Source"] = "Phone",
                ["ProblemDescription"] = "Fit a new kitchen tap.",
            };

            HttpResponseMessage saved = await PostFromAsync(admin, editPage, editPage, fields);

            var page = WebUtility.HtmlDecode(await saved.Content.ReadAsStringAsync());
            Assert.Equal(jobPage, saved.RequestMessage.RequestUri.AbsolutePath);
            Assert.Contains("The details are saved. Changed: last name, phone number, address, service, where it came from, what the job is.", page);
            Assert.Contains("Grace Hopper · came from: Phone", page);
            Assert.Contains("<span>07700 900999</span>", page);
            Assert.Contains("3 Navy Row, Epsom, KT17 1AA", page);
            Assert.Contains(service.Name, page);
            Assert.Contains("Fit a new kitchen tap.", page);
            Assert.Matches("<dt>Estimate</dt>\\s*<dd>£" + service.BasePrice.ToString("0.00", CultureInfo.InvariantCulture) + "</dd>", page);
            Assert.Contains(
                "<span class=\"job-ledger-what text-primary\">Details changed. Last name was empty. Phone number was 07700 900456. Address was not written down. Service was not picked. Where it came from was Agency. The job had no description.</span>",
                page);

            // Still the same job at the same time, and nobody was emailed about any of it.
            Assert.Equal(start, site.Booking().ScheduledStart);
            Assert.Equal(("Booked", "Not paid"), LabelsOn(page));
            Assert.Empty(site.Emails.Sent);

            // Saved again as it stands.
            HttpResponseMessage again = await PostFromAsync(admin, editPage, editPage, fields);
            Assert.Contains("Nothing was changed: the details are as they were.", WebUtility.HtmlDecode(await again.Content.ReadAsStringAsync()));

            // A job needs a phone number when it is put right, as when it is written in. The
            // form comes back with what was typed, and the job keeps the number it had.
            HttpResponseMessage refused = await PostFromAsync(admin, editPage, editPage, With(With(fields, "PhoneNumber", string.Empty), "CustomerLastName", "Murray"));
            var refusedPage = WebUtility.HtmlDecode(await refused.Content.ReadAsStringAsync());
            Assert.Contains("Phone number is required.", refusedPage);
            Assert.Matches("name=\"CustomerLastName\"[^>]*value=\"Murray\"", refusedPage);
            Assert.Equal("07700 900999", site.Booking().PhoneNumber);
            Assert.Equal("Hopper", site.Booking().CustomerLastName);
        }

        // A customer who booked on the website rings to say the email was typed wrongly. The
        // booking's details are put right like any other job's, with two differences: it stays
        // a website booking, and it keeps an email, because the site emails this customer.
        [Fact]
        public async Task AWebsiteBookingsDetailsArePutRightButItKeepsAnEmailAndStaysAWebsiteBooking()
        {
            using var site = new Site();
            Booking booking = await site.BookAndPayAsync(site.AFreeSlot());
            HttpClient admin = await site.SignedInAdminAsync();
            var jobPage = JobPage(booking.Id);
            var editPage = EditPage(booking.Id);
            Guid serviceId = site.Read(dbContext => dbContext.BookingServices.Single().ServiceId);
            await PostFromAsync(admin, jobPage, JobsPage + "/AssignTechnician", new Dictionary<string, string>
            {
                ["id"] = booking.Id.ToString(),
                ["technicianId"] = site.LaunchTechnicianId().ToString(),
            });
            var emailsBefore = site.Emails.Sent.Count;

            var form = WebUtility.HtmlDecode(await (await admin.GetAsync(editPage)).Content.ReadAsStringAsync());
            Assert.DoesNotContain("name=\"Source\"", form);
            Assert.Contains("The site emails this customer at this address.", form);
            var emailBox = Regex.Match(form, "<input[^>]*name=\"Email\"[^>]*>").Value;
            Assert.Contains("value=\"ada@example.com\"", emailBox);
            Assert.Contains("required", emailBox);
            Assert.Matches("<option value=\"" + serviceId + "\" selected=\"selected\">", form);
            Assert.Matches("name=\"Address\"[^>]*value=\"1 Analytical Engine Way, Chessington, KT9 1AA\"", form);

            var fields = new Dictionary<string, string>
            {
                ["Id"] = booking.Id.ToString(),
                ["CustomerFirstName"] = "Ada",
                ["CustomerLastName"] = "Lovelace",
                ["PhoneNumber"] = "07700 900123",
                ["Email"] = string.Empty,
                ["Address"] = "1 Analytical Engine Way, Chessington, KT9 1AA",
                ["ServiceId"] = serviceId.ToString(),
                ["ProblemDescription"] = "The kitchen tap has been dripping for a week.",
            };

            HttpResponseMessage noEmail = await PostFromAsync(admin, editPage, editPage, fields);
            Assert.Contains("A website booking keeps an email address: the site emails this customer.", WebUtility.HtmlDecode(await noEmail.Content.ReadAsStringAsync()));
            Assert.Equal("ada@example.com", site.Booking().Email);

            // A form sent without the page can name somewhere else it came from.
            HttpResponseMessage saved = await PostFromAsync(admin, editPage, editPage, With(With(fields, "Email", "ada.lovelace@example.com"), "Source", "Phone"));

            var page = WebUtility.HtmlDecode(await saved.Content.ReadAsStringAsync());
            Assert.Equal(jobPage, saved.RequestMessage.RequestUri.AbsolutePath);
            Assert.Contains(
                "The details are saved. Changed: email. The emails the site sent before went to the old address, and nothing was sent to the new one. To send the technician's name and number to the new address, set the technician to \"Unassigned\", save, then pick Zapryan again.",
                page);
            Assert.Contains("<span>ada.lovelace@example.com</span>", page);
            Assert.Contains("Ada Lovelace · came from: Website", page);
            Assert.Equal(("Booked", "Deposit paid"), LabelsOn(page));
            Assert.Equal(BookingSource.Website, site.Booking().Source);
            Assert.Equal(emailsBefore, site.Emails.Sent.Count);

            // What the line says to do does send the email, to the new address.
            var id = new Dictionary<string, string> { ["id"] = booking.Id.ToString() };
            await PostFromAsync(admin, jobPage, JobsPage + "/AssignTechnician", With(id, "technicianId", string.Empty));
            await PostFromAsync(admin, jobPage, JobsPage + "/AssignTechnician", With(id, "technicianId", site.LaunchTechnicianId().ToString()));
            Assert.Equal("ada.lovelace@example.com", site.Emails.Sent.Last(e => e.Subject == TechnicianEmailSubject).To);
        }

        // A service deleted in the admin panel is no longer on the form's list. The job that was
        // booked with it gets an option of its own, already picked: without it the list would
        // open on "Not picked", and correcting a phone number would take the service off the job.
        [Fact]
        public async Task AJobKeepsAServiceNoLongerOfferedWhenItsOtherDetailsAreSaved()
        {
            using var site = new Site();
            Booking booking = await site.BookAndPayAsync(site.AFreeSlot());
            HttpClient admin = await site.SignedInAdminAsync();
            var editPage = EditPage(booking.Id);
            Guid serviceId = site.Read(dbContext => dbContext.BookingServices.Single().ServiceId);
            site.Write(dbContext =>
            {
                Service service = dbContext.Services.Single(x => x.Id == serviceId);
                service.IsDeleted = true;
                service.DeletedOn = DateTime.UtcNow;
                return service.Id;
            });

            var form = WebUtility.HtmlDecode(await (await admin.GetAsync(editPage)).Content.ReadAsStringAsync());
            Assert.Contains("<option value=\"" + serviceId + "\" selected=\"selected\">The service it has now (no longer offered)</option>", form);

            HttpResponseMessage saved = await PostFromAsync(admin, editPage, editPage, new Dictionary<string, string>
            {
                ["Id"] = booking.Id.ToString(),
                ["CustomerFirstName"] = "Ada",
                ["CustomerLastName"] = "Lovelace",
                ["PhoneNumber"] = "07700 900999",
                ["Email"] = "ada@example.com",
                ["Address"] = "1 Analytical Engine Way, Chessington, KT9 1AA",
                ["ServiceId"] = serviceId.ToString(),
                ["ProblemDescription"] = "The kitchen tap has been dripping for a week.",
            });

            Assert.Contains("The details are saved. Changed: phone number.", WebUtility.HtmlDecode(await saved.Content.ReadAsStringAsync()));
            Assert.Equal(serviceId, site.Read(dbContext => dbContext.BookingServices.Single().ServiceId));
            Assert.NotNull(site.Booking().TotalAmount);
        }

        // A cancelled job is a record of what happened. Its page has no button, the form does
        // not open, and one that was opened before the job was cancelled changes nothing.
        [Fact]
        public async Task ACancelledJobsDetailsCannotBeEditedEvenFromAFormOpenedBefore()
        {
            using var site = new Site();
            HttpClient admin = await site.SignedInAdminAsync();
            Guid jobId = await site.WriteAJobInAsync(admin, DateTime.Today.AddDays(4).AddHours(15));
            var jobPage = JobPage(jobId);
            var editPage = EditPage(jobId);
            var refusal = "Nothing was changed. A job's details can be put right while it is booked or done, not once it is cancelled or abandoned.";
            var openedBefore = await (await admin.GetAsync(editPage)).Content.ReadAsStringAsync();

            HttpResponseMessage cancelled = await PostFromAsync(admin, jobPage, JobsPage + "/Cancel", new Dictionary<string, string>
            {
                ["id"] = jobId.ToString(),
                ["reason"] = "The agency withdrew the job.",
            });
            Assert.DoesNotContain("id=\"job-edit-details\"", await cancelled.Content.ReadAsStringAsync());

            HttpResponseMessage reopened = await admin.GetAsync(editPage);
            Assert.Equal(jobPage, reopened.RequestMessage.RequestUri.AbsolutePath);
            Assert.Contains(refusal, WebUtility.HtmlDecode(await reopened.Content.ReadAsStringAsync()));

            HttpResponseMessage sent = await PostToAsync(admin, openedBefore, editPage, new Dictionary<string, string>
            {
                ["Id"] = jobId.ToString(),
                ["CustomerFirstName"] = "Grace",
                ["PhoneNumber"] = "07700 900999",
                ["Source"] = "Agency",
            });

            Assert.Contains(refusal, WebUtility.HtmlDecode(await sent.Content.ReadAsStringAsync()));
            Assert.Equal("07700 900456", site.Booking().PhoneNumber);
        }

        // The address the "Approve" button posted to. A booking is approved by its deposit.
        [Fact]
        public async Task TheApproveAddressIsGone()
        {
            using var site = new Site();
            Booking booking = await site.BookAndPayAsync(site.AFreeSlot());
            HttpClient admin = await site.SignedInAdminAsync();

            HttpResponseMessage approved = await PostFromAsync(admin, JobPage(booking.Id), JobsPage + "/Approve", new Dictionary<string, string>
            {
                ["id"] = booking.Id.ToString(),
            });

            Assert.Equal(HttpStatusCode.NotFound, approved.StatusCode);
        }

        // The list: one row per job with its two labels, a card that counts the jobs nobody is
        // on yet (it used to count "pending approval", a step that no longer exists), and a
        // filter by the job's label.
        [Fact]
        public async Task TheListShowsEachJobsLabelsCountsTheOnesWithNoTechnicianAndFiltersByLabel()
        {
            using var site = new Site();
            Booking booking = await site.BookAndPayAsync(site.AFreeSlot());
            HttpClient admin = await site.SignedInAdminAsync();
            Guid writtenIn = await site.WriteAJobInAsync(admin, DateTime.Today.AddDays(4).AddHours(15));

            var list = WebUtility.HtmlDecode(await (await admin.GetAsync(JobsPage)).Content.ReadAsStringAsync());
            Assert.Matches("Waiting for a Technician</p>\\s*<h3[^>]*>2</h3>", list);
            Assert.DoesNotContain("Pending Approval", list);
            Assert.Contains("#" + BookingReference.Short(booking.Id), list);
            Assert.Contains("#" + BookingReference.Short(writtenIn), list);
            Assert.Equal(2, Regex.Matches(list, ">Needs one</span>").Count);
            Assert.Contains("<span>Deposit paid</span>", list);
            Assert.Contains("<span>Not paid</span>", list);
            Assert.Matches("<option value=\"Waiting for deposit\"[^>]*>Waiting for deposit</option>\\s*<option value=\"Booked\"[^>]*>Booked</option>\\s*<option value=\"Done\"[^>]*>Done</option>\\s*<option value=\"Cancelled\"[^>]*>Cancelled</option>\\s*<option value=\"Abandoned\"[^>]*>Abandoned</option>", list);

            await PostFromAsync(admin, JobPage(booking.Id), JobsPage + "/AssignTechnician", new Dictionary<string, string>
            {
                ["id"] = booking.Id.ToString(),
                ["technicianId"] = site.LaunchTechnicianId().ToString(),
            });
            await PostFromAsync(admin, JobPage(writtenIn), JobsPage + "/Cancel", new Dictionary<string, string>
            {
                ["id"] = writtenIn.ToString(),
                ["reason"] = "The agency withdrew the job.",
            });

            list = WebUtility.HtmlDecode(await (await admin.GetAsync(JobsPage)).Content.ReadAsStringAsync());
            Assert.Matches("Waiting for a Technician</p>\\s*<h3[^>]*>0</h3>", list);
            Assert.DoesNotContain(">Needs one</span>", list);
            Assert.Contains("<span class=\"text-primary\">Zapryan</span>", list);

            var booked = WebUtility.HtmlDecode(await (await admin.GetAsync(JobsPage + "?status=Booked")).Content.ReadAsStringAsync());
            Assert.Contains("#" + BookingReference.Short(booking.Id), booked);
            Assert.DoesNotContain("#" + BookingReference.Short(writtenIn), booked);

            var cancelled = WebUtility.HtmlDecode(await (await admin.GetAsync(JobsPage + "?status=Cancelled")).Content.ReadAsStringAsync());
            Assert.Contains("#" + BookingReference.Short(writtenIn), cancelled);
            Assert.DoesNotContain("#" + BookingReference.Short(booking.Id), cancelled);

            var done = WebUtility.HtmlDecode(await (await admin.GetAsync(JobsPage + "?status=Done")).Content.ReadAsStringAsync());
            Assert.Contains("No jobs with this label.", done);

            // The cards describe the whole business whatever the filter: the card still says 0.
            Assert.Matches("Waiting for a Technician</p>\\s*<h3[^>]*>0</h3>", cancelled);
        }

        // Most of the dashboard was the design mock-up's own text: "+12.5%" beside the revenue,
        // "System Status: Optimal", a server-capacity bar, an "Add Widget" tile, a graph that
        // "is generating", and a card counting reviews that have had no way in since the public
        // form was removed. Every figure on it is counted now, each card opens the list it
        // counts, and the menu no longer leads to the reviews page, which is still there for the
        // day reviews are brought in from Google (PROJECT_STATE.md Section 3ch).
        [Fact]
        public async Task TheDashboardShowsCountedFiguresThatOpenTheirListsAndNoMockUpText()
        {
            using var site = new Site();
            await site.BookAndPayAsync(site.AFreeSlot());
            HttpClient customer = site.Browser(followRedirects: false);
            await FormsWebTests.PostFormAsync(customer, "/Booking", site.BookingFormFor(site.AFreeSlot()));
            HttpClient admin = await site.SignedInAdminAsync();

            var dashboard = WebUtility.HtmlDecode(await (await admin.GetAsync("/Administration/Dashboard")).Content.ReadAsStringAsync());

            var mockUpText = new[]
            {
                "+12.5%", "System Status", "notifications_active", "New Leads", "Add Widget", "Activity Visualization",
                "Platform Health", "Server Capacity", "Inquiry Response Rate", "Administrator Mode", "Unapproved Reviews", "Moderate Reviews",
            };
            Assert.All(mockUpText, text => Assert.DoesNotContain(text, dashboard));

            // One booking paid and one still waiting for its deposit.
            Assert.Matches("Total Revenue</p>\\s*<h2[^>]*>£50.00</h2>", dashboard);
            Assert.Matches("Total Jobs</p>\\s*<h2[^>]*>2</h2>", dashboard);
            Assert.Contains(">1 waiting for deposit</span>", dashboard);
            Assert.Matches("Waiting for a Technician</p>\\s*<h2[^>]*>1</h2>", dashboard);
            Assert.Matches("Total Enquiries</p>\\s*<h2[^>]*>0</h2>", dashboard);

            // The same to-do figure as the card on the Jobs page.
            var list = WebUtility.HtmlDecode(await (await admin.GetAsync(JobsPage)).Content.ReadAsStringAsync());
            Assert.Matches("Waiting for a Technician</p>\\s*<h3[^>]*>1</h3>", list);

            Assert.Equal(3, Regex.Matches(dashboard, "<a\\b(?=[^>]*class=\"dashboard-stat-card)(?=[^>]*href=\"" + JobsPage + "\")[^>]*>").Count);
            Assert.Single(Regex.Matches(dashboard, "<a\\b(?=[^>]*class=\"dashboard-stat-card)(?=[^>]*href=\"/Administration/Enquiries\")[^>]*>"));
            Assert.Contains("href=\"" + JobsPage + "/Create\"", dashboard);

            Assert.DoesNotContain("href=\"/Administration/Reviews\"", dashboard);
            Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/Administration/Reviews")).StatusCode);
        }

        // The calendar had only a date box to get from one day to another. Each of its two lists
        // now has an arrow a day back and a day on, lit when the day it leads to has something
        // for that list, and the menu has the calendar straight after the jobs
        // (PROJECT_STATE.md Section 3ch).
        [Fact]
        public async Task TheCalendarsArrowsStepADayAndLightUpForADayThatHasJobsOrSlots()
        {
            using var site = new Site();
            HttpClient admin = await site.SignedInAdminAsync();

            // A day years away, where nothing is seeded: a job is written in on the day after it.
            DateTime day = DateTime.Today.AddYears(3);
            await site.WriteAJobInAsync(admin, day.AddDays(1).AddHours(15));

            var page = WebUtility.HtmlDecode(await (await admin.GetAsync(CalendarDay(day))).Content.ReadAsStringAsync());
            Assert.Matches(Arrow("jobs-day-after", lit: true, day.AddDays(1), "has jobs"), page);
            Assert.Matches(Arrow("jobs-day-before", lit: false, day.AddDays(-1), "no jobs"), page);
            Assert.Matches(Arrow("slots-day-after", lit: false, day.AddDays(1), "no slots"), page);
            Assert.Matches(Arrow("slots-day-before", lit: false, day.AddDays(-1), "no slots"), page);

            // The day before one that has slots, and the day after it.
            DateTime slotDay = site.AFreeSlot().StartTime.Date;
            page = WebUtility.HtmlDecode(await (await admin.GetAsync(CalendarDay(slotDay.AddDays(-1)))).Content.ReadAsStringAsync());
            Assert.Matches(Arrow("slots-day-after", lit: true, slotDay, "has slots"), page);
            page = WebUtility.HtmlDecode(await (await admin.GetAsync(CalendarDay(slotDay.AddDays(1)))).Content.ReadAsStringAsync());
            Assert.Matches(Arrow("slots-day-before", lit: true, slotDay, "has slots"), page);

            // On a phone the button beside the arrow has no plus, which reached the arrow.
            Assert.Matches("d-none d-md-inline-block\">add</span>\\s*<span>Write a job in</span>", page);

            Assert.Matches("Jobs\\s*</a>\\s*<a\\b[^>]*href=\"/Administration/Calendar\"", page);
        }

        // "Unblock" in the admin calendar did nothing: it called the method that frees a slot
        // from its booking, which leaves the block where it is. Each calendar button also
        // answers now with what it did.
        [Fact]
        public async Task ABlockedHourCanBeOpenedAgainFromTheCalendar()
        {
            using var site = new Site();
            HttpClient admin = await site.SignedInAdminAsync();
            AvailabilitySlot slot = site.AFreeSlot();
            var dayPage = "/Administration/Calendar?date=" + slot.StartTime.ToString("yyyy-MM-dd");
            var fields = new Dictionary<string, string>
            {
                ["id"] = slot.Id.ToString(),
                ["returnDate"] = slot.StartTime.ToString("yyyy-MM-dd"),
            };

            HttpResponseMessage blocked = await PostFromAsync(admin, dayPage, "/Administration/Calendar/BlockSlot", fields);
            var page = WebUtility.HtmlDecode(await blocked.Content.ReadAsStringAsync());
            Assert.Contains("The hour is blocked. It cannot be booked on the website.", page);
            Assert.True(site.Slot(slot.Id).IsBlocked);
            Assert.Contains("/Administration/Calendar/UnblockSlot", page);

            HttpResponseMessage opened = await PostFromAsync(admin, dayPage, "/Administration/Calendar/UnblockSlot", fields);
            page = WebUtility.HtmlDecode(await opened.Content.ReadAsStringAsync());
            Assert.Contains("The hour is open again. It can be booked on the website.", page);
            Assert.False(site.Slot(slot.Id).IsBlocked);

            // The whole day, then one of its hours back.
            HttpResponseMessage day = await PostFromAsync(admin, dayPage, "/Administration/Calendar/BlockDate", new Dictionary<string, string>
            {
                ["date"] = slot.StartTime.ToString("yyyy-MM-dd"),
            });
            Assert.Contains("is blocked. Nothing on that day can be booked on the website.", WebUtility.HtmlDecode(await day.Content.ReadAsStringAsync()));
            Assert.True(site.Slot(slot.Id).IsBlocked);

            await PostFromAsync(admin, dayPage, "/Administration/Calendar/UnblockSlot", fields);
            Assert.False(site.Slot(slot.Id).IsBlocked);
        }

        private static string JobPage(Guid id) => JobsPage + "/Details/" + id;

        private static string EditPage(Guid id) => JobsPage + "/Edit/" + id;

        private static string CalendarDay(DateTime day) => "/Administration/Calendar?date=" + day.ToString("yyyy-MM-dd");

        // One of the calendar's four day arrows, as it should stand in the page: where it leads,
        // whether it is lit, and what it says when pointed at.
        private static string Arrow(string id, bool lit, DateTime leadsTo, string says)
        {
            return "<a\\b(?=[^>]*id=\"" + id + "\")"
                + "(?=[^>]*class=\"day-arrow" + (lit ? " day-arrow-lit" : "\\s*") + "\")"
                + "(?=[^>]*href=\"" + Regex.Escape(CalendarDay(leadsTo)) + "\")"
                + "(?=[^>]*title=\"[^\"]*: " + says + "\")[^>]*>";
        }

        // The two labels at the top of a job's page: where the work stands, and the money.
        private static (string Job, string Money) LabelsOn(string page)
        {
            Match labels = Regex.Match(page, "id=\"job-labels\">\\s*<span[^>]*>([^<]*)</span>\\s*<span[^>]*>([^<]*)</span>");
            Assert.True(labels.Success, "The page has no job labels.");
            return (labels.Groups[1].Value, labels.Groups[2].Value);
        }

        private static Dictionary<string, string> With(Dictionary<string, string> fields, string name, string value)
        {
            return new Dictionary<string, string>(fields) { [name] = value };
        }

        // Sends one of the forms on a page to its own address, with the page's hidden fields
        // (the antiforgery token among them) as a browser would send them back.
        private static async Task<HttpResponseMessage> PostFromAsync(HttpClient browser, string page, string action, Dictionary<string, string> fields)
        {
            var html = await (await browser.GetAsync(page)).Content.ReadAsStringAsync();
            return await PostToAsync(browser, html, action, fields);
        }

        // The same, from a page already in hand. Only the token is taken from the page: a job's
        // page has several forms, and the hidden fields of one are not another's to send.
        private static async Task<HttpResponseMessage> PostToAsync(HttpClient browser, string html, string action, Dictionary<string, string> fields)
        {
            Dictionary<string, string> hidden = FormsWebTests.HiddenFieldsOf(html);
            Assert.Contains("__RequestVerificationToken", hidden.Keys);

            var form = new Dictionary<string, string>(fields)
            {
                ["__RequestVerificationToken"] = hidden["__RequestVerificationToken"],
            };

            return await browser.PostAsync(action, new FormUrlEncodedContent(form));
        }

        // A site of its own for one test: its own database, an email sender that keeps what it
        // is given, and the pretend payment a machine with no Stripe key uses.
        private sealed class Site : IDisposable
        {
            private readonly SqliteWebApplicationFactory root = new SqliteWebApplicationFactory();
            private readonly WebApplicationFactory<Program> host;

            public Site()
            {
                this.host = this.root.WithWebHostBuilder(builder =>
                {
                    // A Development host also reads the developer's own user secrets. A Stripe
                    // key kept there would send these tests to Stripe itself.
                    builder.ConfigureAppConfiguration((context, configuration) => configuration.AddInMemoryCollection(
                        new Dictionary<string, string> { ["Stripe:SecretKey"] = string.Empty }));

                    builder.ConfigureServices(services => services.AddSingleton<IEmailSender>(this.Emails));
                });
            }

            public RecordingEmailSender Emails { get; } = new RecordingEmailSender();

            public string ServiceName { get; private set; }

            // A browser of its own: it keeps its cookies and asks for pages.
            public HttpClient Browser(bool followRedirects = true)
            {
                HttpClient client = this.host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = followRedirects });
                client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
                return client;
            }

            public async Task<HttpClient> SignedInAdminAsync()
            {
                HttpClient browser = this.Browser();

                await FormsWebTests.PostFormAsync(browser, "/Identity/Account/Login", new Dictionary<string, string>
                {
                    ["Input.Email"] = SqliteWebApplicationFactory.AdminEmail,
                    ["Input.Password"] = SqliteWebApplicationFactory.AdminPassword,
                });

                return browser;
            }

            // The first free hour from the day after tomorrow on, out of the two weeks a new
            // site outside production starts with.
            public AvailabilitySlot AFreeSlot()
            {
                return this.Read(dbContext => dbContext.AvailabilitySlots
                    .Where(x => !x.IsBooked && !x.IsBlocked && x.StartTime >= DateTime.Today.AddDays(2))
                    .OrderBy(x => x.StartTime)
                    .First());
            }

            public AvailabilitySlot Slot(Guid id)
            {
                return this.Read(dbContext => dbContext.AvailabilitySlots.Single(x => x.Id == id));
            }

            // The one job this site has, read afresh.
            public Booking Booking()
            {
                return this.Read(dbContext => dbContext.Bookings.Include(x => x.Status).Single());
            }

            public Guid LaunchTechnicianId()
            {
                return this.Read(dbContext => dbContext.Technicians.Single(x => x.FirstName == "Zapryan").Id);
            }

            // The booking form as a customer in a district we cover fills it in.
            public Dictionary<string, string> BookingFormFor(AvailabilitySlot slot)
            {
                Service service = this.Read(dbContext => dbContext.Services
                    .Where(x => x.Category.Slug == "plumbing")
                    .OrderBy(x => x.Name)
                    .First());
                this.ServiceName = service.Name;

                return new Dictionary<string, string>
                {
                    ["CustomerFirstName"] = "Ada",
                    ["CustomerLastName"] = "Lovelace",
                    ["Email"] = "ada@example.com",
                    ["PhoneNumber"] = "07700 900123",
                    ["Address"] = "1 Analytical Engine Way, Chessington",
                    ["Postcode"] = "KT9 1AA",
                    ["ProblemDescription"] = "The kitchen tap has been dripping for a week.",
                    ["SlotId"] = slot.Id.ToString(),
                    ["ServiceId"] = service.Id.ToString(),
                };
            }

            // Books the slot and pays its deposit as a customer's browser does, redirects and all.
            public async Task<Booking> BookAndPayAsync(AvailabilitySlot slot)
            {
                HttpResponseMessage landed = await FormsWebTests.PostFormAsync(this.Browser(), "/Booking", this.BookingFormFor(slot));

                Booking booking = this.Booking();
                Assert.Equal("/Booking/Confirmed/" + booking.Id, landed.RequestMessage.RequestUri.AbsolutePath);
                Assert.Equal("Approved", booking.Status.Name);
                return booking;
            }

            // Writes a job in through the admin's form, with as little as the form asks for.
            public async Task<Guid> WriteAJobInAsync(HttpClient admin, DateTime start)
            {
                HttpResponseMessage written = await FormsWebTests.PostFormAsync(admin, JobsPage + "/Create", new Dictionary<string, string>
                {
                    ["CustomerFirstName"] = "Grace",
                    ["PhoneNumber"] = "07700 900456",
                    ["Date"] = start.ToString("yyyy-MM-dd"),
                    ["Time"] = start.ToString("HH:mm"),
                    ["Source"] = "Agency",
                });

                var path = written.RequestMessage.RequestUri.AbsolutePath;
                Assert.StartsWith(JobsPage + "/Details/", path);
                return Guid.Parse(path.Substring((JobsPage + "/Details/").Length));
            }

            public void Dispose()
            {
                this.host.Dispose();
                this.root.Dispose();
            }

            public T Read<T>(Func<ApplicationDbContext, T> query)
            {
                using IServiceScope scope = this.host.Services.CreateScope();
                return query(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
            }

            // Puts something into the database the way no page of the site can.
            public T Write<T>(Func<ApplicationDbContext, T> change)
            {
                using IServiceScope scope = this.host.Services.CreateScope();
                ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                T result = change(dbContext);
                dbContext.SaveChanges();
                return result;
            }
        }
    }
}
