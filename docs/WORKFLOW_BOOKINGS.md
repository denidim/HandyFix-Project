# 📅 Jobs Workflow — Capacity, Booking, Writing In, Assignment, Money

How a job gets from "nothing exists yet" to "done, and paid in full".

This is the end-to-end reference for both sides of the system: the **admin user** who runs it day to
day, and the **developer** who has to change it. Every section states the business rule first, then
where it lives in the code.

**Every job the business does has a page**, at `/Administration/Bookings` (the admin reads "Jobs"
there; the address and the code keep the older word, "bookings"). A job starts one of two ways and
then goes the same way as any other:

```
  BOOKED ON THE WEBSITE                       WRITTEN IN BY THE ADMIN
  the customer picks an hour                  phone, WhatsApp, an enquiry, an agency
  and pays a £50 deposit                      a first name and a phone number are enough
  the hour comes off sale by itself           the admin blocks the hour by hand
  the site emails the customer                the site emails nobody
  the time is promised                        the time is not promised
            │                                             │
            └──────────────────────┬──────────────────────┘
                                   ▼
        the admin picks a technician  →  the job is done, with its final price
        →  each payment is written on the job  →  done, and paid in full

        on the way: its details can be put right, it can be moved, it can be
        cancelled (with the reason), notes can be kept on it, and every change
        leaves a line in its history
```

**Two labels on every job**, kept apart because they move separately (`HandyFix.Common/JobLabels.cs`):

| The job (where the work stands) | The money (where the payment stands) |
| --- | --- |
| **Booked**: it has a day and an hour | **Not paid**: nothing has come in |
| **Done**: finished, with its final price | **Deposit paid**: the £50 from the website, and nothing else |
| **Cancelled**: called off, with the reason | **Part paid**: money has come in and more is owed |
| **Abandoned**: a website booking whose deposit never came | **Paid in full**: the final price is covered |
| | **Deposit refunded**: a cancelled job's deposit went back |

The database keeps its own, older names for where the work stands (`Pending`, `Approved`,
`Completed`); `JobLabels` is the one place that turns them into the words above. "Booked" covers
a website booking still waiting for its deposit: its money label says "Not paid".

**There is no "approve" step.** A website booking is approved by its deposit being paid, and by
nothing else. Until `PROJECT_STATE.md` Section 3ce an admin's "Approve" button sent the email
naming the technician, but the button showed only on an unpaid booking, so for a paid one that
email could never be sent.

**What an admin may do to a job is decided in one place**, `HandyFix.Common/BookingRules.cs`. The
job's page asks it which buttons to show, and the service asks it again before it acts, so a page
left open while the job moved on cannot do what its buttons no longer offer.

---

## Step 1 — Capacity: an admin generates slots

**Nothing books until an admin has created capacity.** Go to
**`/Administration/Calendar`** → *Slot Generator* → pick a start and end date → **Generate Slots**.

> The admin area is routed as `Administration`, not `Admin`. `/Admin/...` will 404.

What generation produces, per day in the range:

| Rule | Value |
| --- | --- |
| Hours | 09:00–17:00, hourly — 8 slots per day |
| Sundays | Skipped entirely |
| Days that already have slots | Left alone (safe to re-run over a range) |
| Technician | **None.** Slots carry no technician — see step 3 |

Also on this page, each answering with a line that says what it did:

| Button | What it does |
| --- | --- |
| **Block** | Takes a free hour off sale. |
| **Unblock** | Opens a blocked hour again. |
| **Block Entire Day** | Blocks every hour of the day. Each is opened again with its own Unblock. |
| **Release** | Puts an hour a job holds back on sale. It asks first: the job keeps its day and time, so another customer could then book the same hour. |

**The day lists every job on it**, above the slots, written-in ones too. A written-in job holds no
slot, so the slots alone would show its hour as free; the slot at that hour says *"Still on sale,
though #A7C30F12 is at this hour. Block it if nobody else should be booked then."* **Write a job
in** on this page opens the form with the day filled in.

> "Unblock" did nothing until `PROJECT_STATE.md` Section 3ce: it called the method that frees a
> slot from its booking, which leaves the block where it is. A blocked hour could not be opened
> again.

### Why capacity is a deliberate act

A slot is **business capacity — an hour you are willing to sell**. It is not a person's diary. This
matters because technicians here are fluid (largely self-employed contractors), and capacity should
not rise and fall with who happens to be on the roster this month.

Two consequences developers should not "helpfully" undo:

- **Read paths never generate.** Loading the public booking page, calling `/Booking/GetSlots`, or
  opening a day in the admin Calendar creates **nothing**. This used to be the opposite:
  `GetAvailableDatesAsync` generated 30 days of slots as a side effect of being read, which meant
  merely browsing the site created capacity nobody had decided to offer. Any "get" that mutates is
  a bug here, not a convenience.
- **Generation does not depend on a technician existing.** `GenerateSlotsForRangeAsync` runs
  happily against a database with zero technicians.

### Empty calendar? That's the system working

If no capacity exists for a date, the customer sees *"No availability on this date. Please pick
another date."* and the admin Calendar shows *"No slots generated for this date."* Neither is an
error — generate some slots.

**Outside production, you get 14 days for free.** `DevelopmentCapacitySeeder` seeds two weeks of
standard capacity at startup so a fresh clone or a newly stood-up staging box has a working booking
flow with no admin action. It is gated on `IWebHostEnvironment.IsProduction()` and only fires when
there is **no future capacity at all**, so it stays silent the moment an admin manages capacity for
real. In production it never runs.

**Code:** `AvailabilityService.GenerateSlotsForRangeAsync` / `UnblockSlotAsync`,
`BookingsService.GetJobsForDayAsync`, `Areas/Administration/Controllers/CalendarController.cs`,
`Web/HandyFix.Web/Services/DevelopmentCapacitySeeder.cs`.

---

## Step 2, the website's way — The customer books and pays

At **`/Booking`** the customer picks a service, picks a date, picks a slot, fills in their details,
optionally uploads photos of the problem, and submits.

1. `BookingsService.CreateBookingAsync` creates the `Booking` with status **`Pending`** and claims
   the slot — **both inside one transaction**. If the slot was taken in the meantime the whole
   thing rolls back and the customer is returned to the form with *"This slot was just taken by
   someone else, please pick another"*, keeping every other field and uploaded photo they entered.
2. The customer is redirected to Stripe Checkout for a **flat £50 deposit** (`Payment/Pay`).
3. On success, `PaymentsService.ProcessPaymentSuccessAsync` marks the payment `DepositPaid`, moves
   the booking to **`Approved`**, and emails the customer and the admin.

**That email is the first one the customer gets.** Nothing is sent when the form is submitted: a
booking that is never paid is abandoned fifteen minutes later (below), and "we have received your
booking" promised a visit that could be gone before it was read.

**The booking has no technician at this point, deliberately.** The deposit confirmation email says
*"We'll email you your technician's name and phone number as soon as one is assigned"* rather than
naming anyone — assignment is step 3. The page the customer lands on after paying says the same.

### What the form checks before any of that

The rules live as attributes on `BookingInputModel`, in `HandyFix.Web.ViewModels/Validation/`. Each
is a `RegularExpressionAttribute`, so ASP.NET Core writes the pattern into the page and the browser
shows the message as the customer types; the server checks the same pattern again on submit. The
Contact and Join Our Team forms use the same attributes.

| Field | Rule |
| --- | --- |
| First and last name | letters, with spaces, hyphens and apostrophes (`[PersonName]`) |
| Email | a full address with a domain ending, `name@example.com` (`[StrictEmail]`) |
| Phone | a UK mobile or landline, the UK way or with `+44` (`[UkPhone]`) |
| Address | the street and town, 5 to 280 characters |
| Postcode | a whole UK postcode, in a box of its own (`[UkPostcode]`) |
| Description | at least 20 characters, and not mostly web links (`[NotMostlyLinks]`, server only) |

A pattern has to mean the same in .NET and in JavaScript: no named groups, no look-behind, and
`[0-9]` instead of `\d`. `FormsWebTests` checks that each field's pattern reaches the page.

**The postcode decides whether the booking is taken at all.** Its district (the `KT9` of
`KT9 2QN`) must be one that some service area lists in its Postcode Districts field
(`/Administration/ServiceAreas`, see `WORKFLOW_SERVICE_AREAS.md`). Otherwise nothing is saved and
no deposit is asked for: the customer sees a notice with an enquiry link, the phone number and
WhatsApp. The page checks as the postcode is typed, so most people find out before filling in the
rest; `BookingController` checks again on submit, which is the one that counts. If **no** area
lists any district, the check is off and every postcode is accepted.

The booking keeps one address: `BookingsService.JoinAddressAndPostcode` adds the postcode to the
street, written the standard way (`1 Ash Road, Chessington, KT9 2QN`).

### Things worth knowing

- **Double-booking is prevented at the database, not in the UI.** `AvailabilitySlot.RowVersion` is a
  real SQL Server `rowversion` concurrency token, and a unique filtered index enforces one booking
  per slot. A losing request gets `SlotUnavailableException`, never a silent overwrite.
- **Abandoned checkouts free their slot automatically.** `StaleBookingCleanupService` runs every
  **5 minutes**, finds `Pending` bookings older than **15 minutes** with no completed payment, flips
  them to **`Abandoned`** and releases their slots. Without this, a customer who opened Stripe and
  walked away would lock an hour forever.
- **Both Stripe paths are handled.** The webhook and the browser redirect can both fire for the same
  session; an idempotency guard ensures confirmation emails send strictly once.
- **An email that cannot be sent never undoes what it was about.** Each of the three booking
  emails (deposit paid to the customer, deposit paid to the company, technician picked) goes out
  after its save and through `TrySendEmailAsync`, which logs a failure (`Email not sent: deposit
  paid, to the customer`) and carries on. Before `PROJECT_STATE.md` Section 3cb a failed send
  showed the customer an error page on the way back from paying. The paid-deposit notice to the
  company has the customer as its Reply-To. When the technician email fails, the admin's page
  says so (step 3).
- **One reference, eight characters.** A booking's id is 36 characters; wherever a person reads
  it (both emails' "Booking Reference", the customer's confirmation page, the admin list and
  page, the Stripe payment line) it is the first eight in capitals, `#A7C30F12`, from
  `BookingReference.Short`. The search box on the admin list finds a booking by it.
- **Times an admin reads are UK times.** When a booking came in is saved in UTC and shown through
  `UkTime.FromUtc`. A slot's own start and end are saved as the UK clock time they were made for
  and are shown as they are.
- **A form that comes back opens where the customer left it.** Whatever sent it back (a field the
  rules refuse, a postcode we do not cover, a photo too large, the person check), the page opens on
  the day of the slot they had chosen and its script picks that slot again if it is still free
  (`IAvailabilityService.GetSlotDateAsync`, `keptSlotId` in the page). The one exception is a slot
  somebody else took meanwhile: it is cleared, and the customer picks another.
- **Photos never cost a booking.** A photo the site does not take (more than 5, over 15 MB, not
  JPEG/PNG/WEBP) brings the form back with the reason. If storage itself cannot be reached, the
  booking is taken without its photos and the failure is logged.
- **What the customer typed reaches an email as text.** The emails are HTML built as strings;
  names, the address and the rest go through `EmailText.Encode` first.

**Code:** `BookingsService.CreateBookingAsync`, `Controllers/BookingController.cs`,
`Controllers/PaymentController.cs`, `PaymentsService`,
`BackgroundServices/StaleBookingCleanupService.cs`.

---

## Step 2, the other way — The admin writes the job in

For a job that did not come through the website: by phone, WhatsApp, an enquiry or an agency.
**`/Administration/Bookings`** → **Write a job in** (the same button is on a day in the calendar).

| Field | Rule |
| --- | --- |
| First name, phone number | required |
| Day, time | required: a job has a day and an hour |
| Came from | Phone, WhatsApp, Enquiry, Agency or Other. Never "Website": the site makes those itself |
| Last name, email, address, service, what the job is | optional |

What makes it different from a website booking, each on purpose:

- **It is a job from the start**: "Booked", with no deposit to wait for. It is saved as
  `Approved`, never `Pending`, so the sweep that abandons unpaid website bookings after fifteen
  minutes cannot pick it up.
- **It takes no slot.** The hour stays on sale until the admin blocks it in the calendar. An hour
  is a start time; how long the job really takes, and so which further hours to block, is the
  admin's call.
- **The site emails nobody**, now or later. The admin is already speaking to the customer.
- **The time is not promised.** Only a deposit promises the time, and a deposit is only paid on
  the website. A customer who wants a promised time is guided through the booking page.
- **A service is optional.** Picked, its price becomes the job's estimate; left out, the job has
  no price until the final one is typed in.

**From an enquiry**: the enquiry's page has **Make this a job**, which opens the form with the
name, phone number, email and message filled in and "Enquiry" picked. An enquiry has one name box;
its first word is taken as the first name. The enquiry itself stays where it is.

A box typed wrongly is put right afterwards with **Edit the Details** on the job's page (see
"Editing the details" below); the day and time, with **Move**.

**Code:** `BookingsService.CreateWrittenInJobAsync`, `JobInputModel`,
`BookingsController.Create`, `Areas/Administration/Views/Bookings/Create.cshtml`.

---

## Step 3 — The admin picks a technician

**This is the only place a technician is ever assigned.** Open the job at
**`/Administration/Bookings`** → **Open** → *Technician* → pick from the dropdown →
**Update Assignment**. The job's own details then show the technician and the number to reach
them on.

**On a website booking, saving a new technician emails the customer**: "Your technician for your
Plumbing Handyman Surrey booking", with the name, a tappable `tel:` link, the day and time, the
service and the address. The name is the first name alone when the roster entry has no last name
(`WORKFLOW_TECHNICIANS.md`). The page then says what happened, in a line at the top:

| What the admin did | What the page answers | Email to the customer |
| --- | --- | --- |
| Picked a technician | *"{name} is now the technician for this job. The customer has been emailed the name and phone number."* | sent |
| Picked one, and the email failed | *"{name} is now the technician for this job, but the email to the customer could not be sent. Please give them the name and phone number yourself."* | not sent; the technician is saved |
| Picked one on a written-in job | *"{name} is now the technician for this job. No email goes out for a job that was written in: please tell the customer yourself."* | none, ever |
| Picked a different technician | the first line again, with the new name | sent again, with the new name |
| Saved the form as it stood | *"Nothing was changed: {name} was already the technician. No email was sent."* | none |
| Chose **`-- Unassigned --`** | *"This job has no technician now. The customer has not been emailed about it."* | none |

**The dropdown is there only while the job is on.** For a website booking that means once the
deposit is paid: an unpaid one is dropped after fifteen minutes, so there is no job to send anyone
to, and the page shows *"A technician can be picked once the deposit is paid."* in its place. A
written-in job has the dropdown from the start. A job that is done, cancelled or abandoned has
none.

The list at `/Administration/Bookings` counts the jobs this step is waiting on: the middle card,
**Waiting for a Technician**, is every job that is on with nobody on it, and those rows read
"Needs one" in the Technician column.

The roster behind that dropdown is managed at **`/Administration/Technicians`** — add, edit,
activate/deactivate, delete. You can add a technician on the fly and assign them immediately.

### Roster rules

| Action | Behaviour |
| --- | --- |
| **Deactivate** (`IsActive` off) | The normal way to retire someone. They disappear from pickers for new assignments, but existing jobs keep them and their history is intact. |
| **Delete** | Allowed **only** for a technician with zero jobs — for a row created in error. The button is hidden and the action refused otherwise. |
| Assigned, then deactivated | Still shown in that job's dropdown, labelled **"(inactive)"**, and stays selected. Without this the form would quietly unassign them on the next save. |
| **`-- Unassigned --`** | Clears the assignment. The customer is not emailed. |

Phone number is required on the form even though the column is nullable — it is what the customer
is emailed when the technician is picked, so a roster entry without one is not useful.

### For developers: where the assignment lives

`Booking.TechnicianId` is the **single** home of "who does this job". `AvailabilitySlot` carries no
technician and must not be given one again. That duplication previously existed and caused two real
bugs: a stale second copy that drifted out of sync, and a reschedule path that silently wiped the
admin's assignment by copying from the destination slot. Moving a job explicitly preserves its
technician — a different hour does not change who is doing it.

**Code:** `Areas/Administration/Controllers/TechniciansController.cs`, `TechniciansService`,
`BookingsService.AssignTechnicianAsync`, `HandyFix.Common/BookingRules.cs`,
`Areas/Administration/Views/Bookings/Details.cshtml`.

---

## Step 4 — Done, and the money

**Mark as Done** asks for the **final price**: what the job came to. One hour is the least a
customer pays for, then 30-minute blocks at the same rate. Until it is typed in the page shows an
estimate (the service's price) and no job can be "Paid in full".

**The money list** has one line per payment, and under it the final price, what has been paid and
what is still owed:

| Line | Where it comes from |
| --- | --- |
| *Deposit, card on the website* | Written by the site when the deposit is paid. It cannot be taken off. |
| *Card*, *Cash*, *Bank transfer* | Written by the admin: **Add Payment**, with the amount and how it was paid. The box starts at what is still owed. A line typed by mistake has a **Take off** button. |

A payment can be written on a job that is on or done, not on one that is cancelled or abandoned,
and not on a website booking before its deposit, which is paid on the website and nowhere else.
The final price of a done job can be put right afterwards (**Change the Final Price**).

**Code:** `BookingsService.CompleteBookingAsync` / `ChangeFinalPriceAsync`,
`PaymentsService.AddPaymentAsync` / `RemovePaymentAsync` / `GetMoneyListAsync`,
`HandyFix.Common/PaymentMethods.cs`.

---

## On the way — editing the details, cancelling, moving, notes, history

### Editing the details

**Edit the Details**, in the job's own details card, opens a form filled in with what the job
holds now: the first and last name, the phone number, the email, the address, the service and
what the job is. The admin changes the box that is wrong and saves.

| Rule | Why |
| --- | --- |
| **The day and time are not on the form.** | "Move" changes them and looks after the calendar. A second way to change the time would let the job and the calendar disagree. |
| **A job that is booked or done can be edited; a cancelled or abandoned one cannot.** | A wrong phone number matters until the job is paid for, and a wrong address after that. A job that fell away is a record of what happened. |
| **A website booking keeps an email, and stays a website booking.** | The site emails that customer, so the email cannot be left empty. Its form has no "Came from" box, and one sent anyway is ignored. A written-in job's email stays optional, and where it came from can be changed to anything but "Website". |
| **Saving sends no email**, whatever changed. | When it is a website booking's email that changed, the line the admin is left with says the earlier emails went to the old address. If a technician is already picked it says how to send their name again: set the technician to "Unassigned", save, then pick them again. Picking the same technician a second time sends nothing. |
| **A new service changes the estimate** to that service's price. Taking the service off takes the estimate off. | The service's price is what the estimate is. The final price and the payments were typed in or paid for the job itself, and are left alone. |
| **A service no longer on the list stays on the job** when other boxes are saved. | A job booked with a service that was deleted or switched off since shows it as *"The service it has now (no longer offered)"*, already picked. Without that the list would open on "Not picked" and correcting a phone number would take the service off the job. |
| **Saved as it stands, nothing changes** and no history line is written. | The page says *"Nothing was changed: the details are as they were."* A box counts as changed only when its words do: spaces at its end, or line breaks written another way, are not a change, and a description that was not changed stays exactly as the customer typed it. |

**The history line keeps what each box held before**, because the page shows what it holds now:
*"Details changed. Phone number was 07700 900456. Address was not written down."* A correction
that was itself wrong can be undone from it. A name, a number or an address is kept up to 80
characters; a rewritten description gets what is left of the line, which holds 700.

**The boxes and their rules are shared with "Write a job in"**: `JobDetailsInputModel` holds them
for both forms, and the two partial views `_JobCustomerFields` and `_JobWorkFields` draw them for
both pages, so the two cannot come to disagree about what a name or a phone number may be.

**Code:** `BookingsService.EditDetailsAsync`, `BookingRules.CanEditDetails`,
`JobEditInputModel`, `BookingsController.Edit`,
`Areas/Administration/Views/Bookings/Edit.cshtml`, `JobHistory.Was`.

### Cancelling

**Cancel the Job** opens a box for the reason, which is required. The job is then "Cancelled" and:

- **keeps its day and hour and the reason.** A cancelled booking used to lose its date, because
  the time lived on the slot it gave back; the list showed "Jan 01, 0001" for it. A job now keeps
  its own copy (`Booking.ScheduledStart`). A booking cancelled or abandoned before
  `PROJECT_STATE.md` Section 3ce reads "No date kept": its date is gone.
- **gives its hour back**, if it held one.
- **sends no email and refunds nothing.** The line the admin is left with says so. The 24-hour
  rule (Terms) decides whether the deposit goes back; the refund is made by hand in Stripe.
- **gets a "Deposit refunded" tick**, where a deposit was paid. Ticked once the money has gone
  back, it turns the money label to "Deposit refunded" and takes the deposit out of the money in.

A job that is done, cancelled or abandoned cannot be cancelled; a "Cancel" sent from a page opened
before then changes nothing and says so.

### Moving

**Move to Another Day or Time** takes a new day and a new time. What happens in the calendar
depends on where the job came from, and the line the page answers with says which:

| The job | Its old hour | Its new hour |
| --- | --- | --- |
| A website booking | goes back on sale | comes off sale, if the calendar has it free |
| A website booking, to an hour that is blocked, taken, or not in the calendar | goes back on sale | nothing is taken off sale; the page says to check that day |
| A written-in job | it held none | nothing changes; the admin blocks by hand |

The two steps happen together or not at all. The technician stays. The customer is not emailed.
Only a job that is on can be moved.

### Notes

A box on every job for anything worth knowing next time. **Only the admin sees it**: it is on no
page a customer can open and in no email.

### History

Every change adds a line at the foot of the job's page, oldest first: booked or written in, the
deposit, the technician, details changed, moved, done, each payment, cancelled, the refund tick. A line is written
by the same code that makes the change (`JobHistory`), and nothing ever edits or deletes one. A
job made before this has no lines for what happened before.

**Code:** `BookingsService.CancelBookingAsync` / `MoveBookingAsync` / `SaveNotesAsync` /
`GetHistoryAsync`, `PaymentsService.SetDepositRefundedAsync`,
`Services/Data/Common/JobHistory.cs`, `BookingHistoryEntry`.

---

## Quick troubleshooting

| Symptom | Cause |
| --- | --- |
| Customers see no available dates at all | No capacity generated. Go to the Calendar and generate a range. |
| A date shows nothing but neighbouring dates work | It's a Sunday (never generated), or every slot is booked/blocked. |
| The technician dropdown is empty | No active technicians. Add one at `/Administration/Technicians`. |
| A technician can't be deleted | They have bookings. Deactivate instead — that's the intended retire path. |
| A job's page has no technician dropdown | It is a website booking whose deposit is not paid yet, or the job is done, cancelled or abandoned. The line under "Technician" says which. |
| An hour is still on sale though a job is written in at it | Expected: a written-in job takes no slot. Block the hour in the calendar; the slot's own line says which job is at it. |
| A phone number, a name or an address on a job is wrong | **Edit the Details** on the job's page. The history keeps what it was before. |
| A job's page has no "Edit the Details" button | The job is cancelled or abandoned, and keeps the details it ended with. |
| The edit form's service list shows "The service it has now (no longer offered)" | The job was booked with a service that was deleted or switched off since. Leave it picked to keep it; pick another to change it. |
| A cancelled job reads "No date kept" | It was cancelled or abandoned before jobs kept their own date. Nothing can bring it back. |
| A job says "Part paid" | Money has come in and more is owed, or the final price has not been typed in yet. "Paid in full" needs a final price. |
| The customer of a written-in job got no email | None is ever sent for a written-in job, whatever is on it. |
| A moved website booking's new hour is still on sale | The calendar had no free slot at that time (none exists there, or it was blocked or taken). The page said so when it was moved. Block it by hand if it should be. |
| The customer says no email named their technician | None is sent until a technician is picked on the booking's page. If one was picked and the page answered "the email to the customer could not be sent", tell the customer yourself and see the next row. |
| A customer says no email arrived | Look in the application log for `Email not sent`. The booking itself is unaffected; the usual causes are a sender address Brevo has not verified, or a wrong API key. |
| A slot is stuck as booked with no real customer | Wait up to 5 minutes for the cleanup sweep, or Release it manually on the Calendar. |
| A customer in our area is told "we don't take online bookings for KT21 yet" | No service area lists that district. Add it to the nearest area's Postcode Districts at `/Administration/ServiceAreas`. |
| The Proceed to Payment button stays grey | A field is empty, the description is under 20 characters, or the postcode is outside the districts we cover (its notice says so). |

---

## Related

- Architectural history and the reasoning behind these decisions: `PROJECT_STATE.md` Section 3r, plus Section 5
  ("Capacity and assignment are separate concerns", "Read paths must not write").
- Adding a service area: `docs/WORKFLOW_SERVICE_AREAS.md`.
