# 👷 Technician Roster — Admin CRUD

Managing *who exists*, as opposed to *who's doing which job* — that's a separate step, covered in
`WORKFLOW_BOOKINGS.md` Step 3. This doc is just the roster itself: `/Administration/Technicians`.

> The admin area is routed as `Administration`, not `Admin`. `/Admin/...` will 404.

---

## Create, edit, deactivate, delete

Fields: First Name, Last Name, Phone Number, Active flag.

| Field | Rule |
| --- | --- |
| First Name | required, 2–100 chars |
| Last Name | **optional**; 2–100 chars when given |
| Phone Number | **required at the form level**, `[Phone]` format, ≤20 chars |

**One name is enough.** A technician kept under a first name alone is shown, and named to the
customer, by that name: "Your technician for your visit on 13 Oct 2026 at 10:00 is Zapryan. You
can reach Zapryan on 020 3951 5915." The business
launches this way on purpose, so no surname goes into a customer's email. Every place that prints
a technician's name goes through `NameFormat.Full` (`HandyFix.Common`), which leaves no space
hanging after a name that stands alone. A last name box left empty is saved as no value, not as
an empty one.

The phone number is required on the form even though the underlying database column is nullable.
Reason, straight from the code comment: it's what the customer is emailed when the technician is
picked for their booking, so a roster entry without one isn't useful — this was tightened
deliberately and needed no migration.

### Deactivate vs. delete — these are not the same action

| Action | What happens | Message shown |
| --- | --- | --- |
| **Deactivate** (`IsActive` off) | The normal way to retire someone. Disappears from the assignment dropdown for *new* bookings; existing bookings keep them. | *"Technician "{name}" was deactivated. Existing bookings keep their assignment, but they can't be assigned to new ones."* |
| **Delete** | Refused outright if the technician has **any** booking referencing them — including soft-deleted ones. Button is hidden on the Index page in that case. | Refused: *"{name} has bookings assigned and can't be deleted - deactivate them instead so past jobs keep their history."*<br>Allowed: *"Technician "{name}" was deleted."* |

The refusal check (`TechniciansService.DeleteAsync`) queries `bookingsRepository.AllWithDeleted()`,
not just active bookings — a soft-deleted booking still counts as "has worked jobs" and blocks
deletion. Delete is genuinely only for a roster row created by mistake with zero history.

**Why this is the opposite of Service Areas' hard-delete rule**: `Booking.TechnicianId` is a live FK
pointing at real job history. Hard-deleting a technician who's worked jobs would leave those
bookings pointing at a row the global soft-delete query filter hides — every past booking's
assignment would silently read "Not Assigned". Service Areas hard-delete for the opposite reason
(a unique, unfiltered slug index) — see `WORKFLOW_SERVICE_AREAS.md` if the asymmetry is confusing.

**Code:** `Areas/Administration/Controllers/TechniciansController.cs`, `TechniciansService`
(`src/Services/HandyFix.Services.Data/Technicians/`).

---

## Assigning to a booking never silently drops an inactive technician

`TechniciansService.GetAssignableAsync` — the query behind the assignment dropdown on a booking's
Details page — returns every **active** technician, **plus** the technician already assigned to
*this* booking even if they've since been deactivated. Without this, opening a booking's edit form
after deactivating its assigned technician would silently unassign them the moment the form was
saved with no change made. In that dropdown they appear labelled **"(inactive)"**.

**Code:** `TechniciansService.GetAssignableAsync`, consumed by
`Areas/Administration/Controllers/BookingsController.cs`.

---

## The technician a new database starts with

`TechniciansSeeder` inserts exactly one row — `Zapryan`, no last name, the business's own number
(`GlobalConstants.BusinessPhone`), active — but **only when the Technicians table is completely
empty**, deleted rows counted (so deleting him does not bring him back at the next start). He is
the one technician the business launches with, so the live site's new database holds a real
person from its first start. This means:

- A fresh clone or newly stood-up environment always starts with this one technician.
- **You cannot add a second technician by editing the seeder.** It won't run again once the table
  has any row in it. The admin CRUD above is the only way to grow the roster.
- **A database made before 2026-10-08 still holds the old placeholder**, `John Doe` on a made-up
  number, because the seeder never touches a table that has a row. There the placeholder is edited
  into the real technician in this admin panel, which keeps its past bookings attached. Staging
  was done this way on 2026-10-08 (`PROJECT_STATE.md` Section 3cd).

---

## Quick troubleshooting

| Symptom | Cause |
| --- | --- |
| Can't delete a technician | They have bookings (even old/soft-deleted ones). Deactivate instead. |
| Assignment dropdown is empty | No active technicians exist. Add or reactivate one. |
| A booking's assigned technician shows "(inactive)" in the dropdown | Expected — they were deactivated after being assigned. Reassigning to someone else is fine; leaving them is fine too. |
| Confirmation email says nothing about a technician | Expected. The email that follows the deposit names nobody; the technician's name and number go out in an email of their own when one is picked on the booking's page — see `WORKFLOW_BOOKINGS.md` Step 3. |
| A booking's page has no assignment dropdown | Its deposit is not paid yet, or the booking is completed, cancelled or abandoned — see `WORKFLOW_BOOKINGS.md` Step 3. |
| A change to `TechniciansSeeder` did nothing | The seeder only fires on a completely empty table — edit the existing row in the admin panel instead. |
| The customer's email names the technician by first name only | Expected when the roster entry has no last name. Add one on the technician's Edit page if it should be there. |

---

## Related

- Assignment itself (which technician does which job) happens on the booking, not here: see
  `WORKFLOW_BOOKINGS.md` Step 3.
- Architectural history: `PROJECT_STATE.md` Section 3r (technicians decoupled from capacity slots,
  admin CRUD added) and Section 3cd (the last name made optional, the launch technician seeded).
