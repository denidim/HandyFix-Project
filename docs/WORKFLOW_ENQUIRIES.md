# ✉️ Enquiries — Contact-Form Submissions

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

Created only from the public `Contact` form (`HomeController.Contact()` POST) — there is no other
way an `Inquiry` row gets created.

| Field | Rule |
| --- | --- |
| Name | required, 2–100 chars |
| Email | required, valid email, ≤255 chars |
| Phone Number | required, `[Phone]`, ≤20 chars |
| Message | required, 10–3000 chars |
| Images | optional, uploaded to Cloudflare R2 (see below) |

**There is no status, response, or "read" field on `Inquiry` at all** — confirmed directly from the
entity. This is exactly why the admin list has no status filter, unlike Bookings and Reviews: there
is genuinely nothing to filter on. `InquirySortField` only has two members, `CreatedOn` and `Name`.

---

## Admin: list, details, delete — `/Administration/Enquiries`

`EnquiriesController.Index(sortField, descending)` — sortable by submission date or name, same
clickable-header pattern as every other admin list. `Details(id)` shows the full message and any
uploaded photos. `Delete(id)` is a soft delete.

No approve/respond/mark-read action exists. Responding to an enquiry happens outside the app
entirely (phone/email, using the contact details on the row).

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
