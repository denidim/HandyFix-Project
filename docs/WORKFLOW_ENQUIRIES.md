# ✉️ Enquiries — Contact-Form Submissions and Job Applications

The simplest admin area in the app: list, view details, delete. No status workflow, because the
underlying data has nothing to put a status on.

> The admin area is routed as `Administration`, not `Admin`. `/Admin/...` will 404.

---

## Naming: "Enquiry" and "Inquiry" are both real, on purpose

Read this before grepping for one and not finding the other.

- **The admin-facing layer — controller, route, views, list/detail view models — says
  "Enquiry" (British spelling)**: `EnquiriesController` → `/Administration/Enquiries`,
  `EnquiryViewModel`, `EnquiryListViewModel`.
- **The entity, service layer, and database say "Inquiry" (American spelling)**: the `Inquiry`
  entity, `IInquiriesService`/`InquiriesService`, `InquirySortField`.

`EnquiriesController` simply wraps `IInquiriesService` and maps `Inquiry` rows into `Enquiry*`
view models. Both spellings are intentional and this split is consistent throughout the code — it
isn't a typo waiting to be "fixed". This doc uses "Enquiries" when talking about the admin/URL
layer and "Inquiries" when naming the actual C# types, matching the code itself.

**Code:** `Areas/Administration/Controllers/EnquiriesController.cs`,
`src/Services/HandyFix.Services.Data/Inquiries/InquiriesService.cs`,
`src/Data/HandyFix.Data.Models/Inquiry.cs`.

---

## What an enquiry is

Created from two public forms: `Contact` (`HomeController.Contact()` POST) and Join Our Team
(`HomeController.JoinTeam()` POST), which saves a job application as an enquiry whose message
starts with `[Job Application]`, so applications reach this list without a table of their own.

| Field | Rule |
| --- | --- |
| Name | required, 2–100 chars, letters with spaces, hyphens and apostrophes (`[PersonName]`) |
| Email | required, a full address with a domain ending (`[StrictEmail]`), ≤255 chars |
| Phone Number | required, a UK mobile or landline (`[UkPhone]`), ≤20 chars |
| Message | required, 10–2900 chars on the form (3000 in the column, which leaves room for the category prefix), and not mostly web links (`[NotMostlyLinks]`) |
| Images | optional, uploaded to Cloudflare R2 (see below) |

The rules are the shared attributes in `HandyFix.Web.ViewModels/Validation/`, the same ones the
booking form uses; `WORKFLOW_BOOKINGS.md` Step 2 says how they reach the browser.

**There is no status, response, or "read" field on `Inquiry` at all** — confirmed directly from the
entity. This is exactly why the admin list has no status filter, unlike Bookings and Reviews: there
is genuinely nothing to filter on. `InquirySortField` only has two members, `CreatedOn` and `Name`.

---

## Admin: list, details, delete — `/Administration/Enquiries`

`EnquiriesController.Index(sortField, descending)` — sortable by submission date or name, same
clickable-header pattern as every other admin list. `Details(id)` shows the full message and any
uploaded photos.

No approve/respond/mark-read action exists. Responding to an enquiry happens outside the app
entirely (phone/email, using the contact details on the row).

### "Delete Permanent" is a real delete

Unlike almost everything else in the app, deleting an enquiry is **not** a soft delete. An enquiry
is a person's name, email, phone number and message, and the button has to mean what it says for a
request to erase someone's data to be met from the admin panel. `InquiriesService.DeleteAsync`:

1. removes the enquiry's photos from Cloudflare R2 (`IImageService.DeleteImagesAsync`),
2. then hard-deletes the `InquiryImage` rows and the `Inquiry` row in one save.

The photos go first on purpose. If storage cannot be reached, the delete stops there, nothing is
removed from the database, and the admin lands back on the enquiry with a message and can press the
button again; removing an object that is already gone succeeds, so a second try is safe. The other
order could leave photos in storage that no row points at any more.

The object to remove is worked out from the saved address (`CloudflareR2Service.GetObjectKey`):
whatever follows the bucket's public address. An address that is not a web address (an upload path
from before photos went to R2) is skipped.

Rows deleted before `PROJECT_STATE.md` Section 3cb were only soft-deleted and are still in the
table, hidden. They exist in development and staging databases only; production starts empty.

---

## Enquiry photos use the R2 pipeline, not the WebP one

Uploaded enquiry photos go through the same `ImageService`/`ICloudflareR2Service` pipeline as
booking problem-photos — raw upload to Cloudflare R2, **no** WebP conversion, capped at 5 files ×
15MB each. This is a different system from the local-disk `ImageStorageService` pipeline that
handles service hero images — see `WORKFLOW_SERVICES.md` Step 3 if you need the contrast in more
detail. Each accepted image becomes its own `InquiryImage` row (`ImageUrl` + the parent `InquiryId`).

---

## Quick troubleshooting

| Symptom | Cause |
| --- | --- |
| Can't find "Inquiry" anywhere in `Areas/Administration/` | Correct — the admin layer is named "Enquiries". See the naming note above. |
| Looking for a way to mark an enquiry as handled | Doesn't exist. `Inquiry` has no status field; track responses outside the app. |
| An enquiry has no photos even though the customer said they attached some | Check the upload actually succeeded against R2 — failures there don't block the enquiry from being created, they just leave it with fewer/no `InquiryImage` rows. |

---

## Related

- The other image-upload pipeline (local WebP, service heroes only): `WORKFLOW_SERVICES.md` Step 3.
- Same sortable-list pattern used elsewhere: `WORKFLOW_BOOKINGS.md`, `WORKFLOW_REVIEWS.md`.
