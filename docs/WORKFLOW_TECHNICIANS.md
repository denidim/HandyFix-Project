# 👷 Technician Roster — Admin CRUD

Managing *who exists*, as opposed to *who's doing which job* — that's a separate step, covered in
`WORKFLOW_BOOKINGS.md` Step 3. This doc is just the roster itself: `/Administration/Technicians`.

> The admin area is routed as `Administration`, not `Admin`. `/Admin/...` will 404.

---

## Create, edit, deactivate, delete

Fields: First Name, Last Name, Phone Number, Active flag.

| Field | Rule |
| --- | --- |
| First / Last Name | required, 2–100 chars |
| Phone Number | **required at the form level**, `[Phone]` format, ≤20 chars |

The phone number is required on the form even though the underlying database column is nullable.
Reason, straight from the code comment: it's what the customer receives in the booking-confirmation
email, so a roster entry without one isn't useful — this was tightened deliberately and needed no
migration.

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

## The seeded placeholder

`TechniciansSeeder` inserts exactly one row — `John Doe`, `07123456789`, active — but **only when
the Technicians table is completely empty**. This means:

- A fresh clone or newly stood-up environment always starts with this one placeholder.
- **You cannot add a second technician by editing the seeder.** It won't run again once the table
  has any row in it. The admin CRUD above is the only way to grow the roster past the placeholder.
- Editing `John Doe` into a real person's details (rather than adding alongside them) is the
  intended path once real names are available — see `PROJECT_STATE.md` Tier 2 item 11. That's a
  data-entry task through this admin panel, not a code change.

---

## Quick troubleshooting

| Symptom | Cause |
| --- | --- |
| Can't delete a technician | They have bookings (even old/soft-deleted ones). Deactivate instead. |
| Assignment dropdown is empty | No active technicians exist. Add or reactivate one. |
| A booking's assigned technician shows "(inactive)" in the dropdown | Expected — they were deactivated after being assigned. Reassigning to someone else is fine; leaving them is fine too. |
| Confirmation email says nothing about a technician | The booking was approved *before* a technician was assigned — see `WORKFLOW_BOOKINGS.md`'s troubleshooting table. |
| "Adding" `John Doe` again did nothing | The seeder only fires on a completely empty table — edit the existing row instead. |

---

## Related

- Assignment itself (which technician does which job) happens on the booking, not here: see
  `WORKFLOW_BOOKINGS.md` Step 3.
- Architectural history: `PROJECT_STATE.md` Section 3r (technicians decoupled from capacity slots,
  admin CRUD added).
