# 📅 Booking Workflow — Capacity, Booking, Payment, Assignment

How a job gets from "nothing exists yet" to "a named technician is turning up on Tuesday".

This is the end-to-end reference for both sides of the system: the **admin user** who runs it day to
day, and the **developer** who has to change it. Every section states the business rule first, then
where it lives in the code.

**The one-line version:** an admin creates capacity → a customer books a slot and pays a deposit →
an admin assigns a technician to the resulting booking → an admin approves it, which emails the
customer their technician's name and number.

```
     ADMIN                    CUSTOMER                  ADMIN                   ADMIN
 ┌───────────┐            ┌──────────────┐        ┌──────────────┐       ┌───────────────┐
 │ Generate  │            │ Pick slot,   │        │ Assign a     │       │ Approve       │
 │ capacity  │──slots──▶  │ fill form,   │──────▶ │ technician   │─────▶ │ the booking   │
 │ (Calendar)│            │ pay deposit  │        │ (Bookings)   │       │               │
 └───────────┘            └──────────────┘        └──────────────┘       └───────┬───────┘
       ▲                          │                                              │
       │                          ▼                                              ▼
  nothing else            Booking = Pending                              Booking = Approved
  creates slots           → Approved on payment                          + "CONFIRMED" email
                                                                           naming the technician
```

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

Also on this page: **Block** an individual slot, **Block Entire Day**, and **Release** a booked or
blocked slot back to available.

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

**Code:** `AvailabilityService.GenerateSlotsForRangeAsync`,
`Areas/Administration/Controllers/CalendarController.cs`,
`Web/HandyFix.Web/Services/DevelopmentCapacitySeeder.cs`.

---

## Step 2 — The customer books and pays

At **`/Booking`** the customer picks a service, picks a date, picks a slot, fills in their details,
optionally uploads photos of the problem, and submits.

1. `BookingsService.CreateBookingAsync` creates the `Booking` with status **`Pending`** and claims
   the slot — **both inside one transaction**. If the slot was taken in the meantime the whole
   thing rolls back and the customer is returned to the form with *"This slot was just taken by
   someone else, please pick another"*, keeping every other field and uploaded photo they entered.
2. The customer is redirected to Stripe Checkout for a **flat £50 deposit** (`Payment/Pay`).
3. On success, `PaymentsService.ProcessPaymentSuccessAsync` marks the payment `DepositPaid`, moves
   the booking to **`Approved`**, and emails the customer and the admin.

**The booking has no technician at this point, deliberately.** The deposit confirmation email says
*"We'll confirm your assigned technician shortly"* rather than naming anyone — assignment is step 3.

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

**Code:** `BookingsService.CreateBookingAsync`, `Controllers/BookingController.cs`,
`Controllers/PaymentController.cs`, `PaymentsService`,
`BackgroundServices/StaleBookingCleanupService.cs`.

---

## Step 3 — An admin assigns a technician

**This is the only place a technician is ever assigned.** Open the booking at
**`/Administration/Bookings`** → **Details** → *Status & Assignment* → pick from the dropdown →
**Update Assignment**.

The roster behind that dropdown is managed at **`/Administration/Technicians`** — add, edit,
activate/deactivate, delete. You can add a technician on the fly and assign them immediately.

### Roster rules

| Action | Behaviour |
| --- | --- |
| **Deactivate** (`IsActive` off) | The normal way to retire someone. They disappear from pickers for new assignments, but existing bookings keep them and their history is intact. |
| **Delete** | Allowed **only** for a technician with zero bookings — for a row created in error. The button is hidden and the action refused otherwise. |
| Assigned, then deactivated | Still shown in that booking's dropdown, labelled **"(inactive)"**, and stays selected. Without this the form would quietly unassign them on the next save. |
| **`-- Unassigned --`** | Clears the assignment. |

Phone number is required on the form even though the column is nullable — it is what the customer
receives on confirmation, so a roster entry without one is not useful.

### For developers: where the assignment lives

`Booking.TechnicianId` is the **single** home of "who does this job". `AvailabilitySlot` carries no
technician and must not be given one again. That duplication previously existed and caused two real
bugs: a stale second copy that drifted out of sync, and a reschedule path that silently wiped the
admin's assignment by copying from the destination slot. Rescheduling a booking now explicitly
preserves its technician — moving a job to a different hour does not change who is doing it.

**Code:** `Areas/Administration/Controllers/TechniciansController.cs`, `TechniciansService`,
`BookingsService.AssignTechnicianAsync`, `Areas/Administration/Views/Bookings/Details.cshtml`.

---

## Step 4 — Approve, complete, cancel

From the same booking Details page:

- **Approve** → status `Approved`, and sends the **"Your HandyFix Booking is CONFIRMED!"** email.
  **This is the one customer email that names the technician** (name + a tappable `tel:` link),
  falling back to a generic line if none is assigned — so assign in step 3 *before* approving.
- **Complete** → status `Completed`, once the job is done.
- **Cancel** → status `Cancelled` and releases the slot back to available.

Seeded statuses: `Pending`, `Approved`, `InProgress`, `Completed`, `Cancelled`, `Abandoned`.

**Code:** `BookingsService.UpdateStatusAsync` / `CancelBookingAsync`.

---

## Quick troubleshooting

| Symptom | Cause |
| --- | --- |
| Customers see no available dates at all | No capacity generated. Go to the Calendar and generate a range. |
| A date shows nothing but neighbouring dates work | It's a Sunday (never generated), or every slot is booked/blocked. |
| The technician dropdown is empty | No active technicians. Add one at `/Administration/Technicians`. |
| A technician can't be deleted | They have bookings. Deactivate instead — that's the intended retire path. |
| The confirmation email didn't name a technician | The booking was approved before a technician was assigned. Assign first, then approve. |
| A slot is stuck as booked with no real customer | Wait up to 5 minutes for the cleanup sweep, or Release it manually on the Calendar. |
| Admin success banners don't appear | Known pre-existing quirk: `CheckConsentNeeded` withholds the TempData cookie until cookie consent is accepted. Accept the banner. |

---

## Related

- Architectural history and the reasoning behind these decisions: `PROJECT_STATE.md` Section 3r, plus Section 5
  ("Capacity and assignment are separate concerns", "Read paths must not write").
- Adding a service area: `docs/WORKFLOW_SERVICE_AREAS.md`.
