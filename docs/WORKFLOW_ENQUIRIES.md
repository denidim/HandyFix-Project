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

### The same enquiry sent twice is saved once

Two things stop a double click on Send from making two rows:

- **In the page**, `site.js` marks any POST form once it is sent, stops further submits of it until
  the next page arrives, and shows a spinner on its button. This covers every form on the site,
  admin included. A form can opt out with a `data-no-submit-lock` attribute.
- **On the server**, `InquiriesService.IsRecentDuplicateAsync` asks whether an enquiry with the
  same email and the exact same message was saved in the last ten minutes. If so the visitor gets
  the same thank-you and nothing is saved or uploaded again. `HomeController` asks before the
  photos go to storage.

A second enquiry that says anything different is always a new enquiry, however soon it comes:
people do send a correction straight after the first.

### Two emails go out for each enquiry

`InquiriesService.CreateInquiryAsync` saves the enquiry first and then sends:

1. **A notice to the company's inbox** (`Admin:NotificationEmail`, by default `info@` on the real
   domain; the same inbox the paid-deposit notice goes to). It carries the name, email, phone, the
   message and links to the photos. Its Reply-To is the sender's address, so **pressing Reply
   answers the customer**. A job application gets the subject "New job application - {name}".
2. **An acknowledgement to the sender**, from the bookings address: "we have received your
   enquiry and will be in touch". It repeats **nothing** the sender typed, not even their name.
   Anyone can type any address into a public form, and an email that echoed the form back would
   let a bot use the site to send a stranger whatever it liked, from the business's own domain.

Both are built as HTML, so everything the sender typed is encoded on its way in
(`EmailText.Encode`). Both go through `TrySendEmailAsync`: a send that fails is logged
(`Email not sent: enquiry notice to the company`) and does not throw, because the enquiry is
already in this list and the visitor should not see an error for something that was received.
What still fails loudly is a missing `Brevo:ApiKey` outside Development: the email sender cannot
be created at all, and since `HomeController` needs the enquiries service, every page it serves
fails until the key is set. That is deliberate, and a start-up check on a new environment.

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

**An enquiry that turns into work becomes a job.** The enquiry's page has **Make this a job**,
which opens the admin's "Write a job in" form with the name, phone number, email and message
already filled in and "Enquiry" picked as where it came from. The admin adds the day and the
time. The enquiry itself is not changed or removed. `WORKFLOW_BOOKINGS.md` has the rest ("Step 2,
the other way").

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
| An enquiry has no photos even though the customer said they attached some | Storage could not be reached when it was sent. The enquiry is saved without them, the notice email says "Photos attached but not saved: N", and the customer was told on the thank-you. Ask for them again. |
| A customer's photos were refused on the form | More than 5, one over 15 MB, or a file that is not JPEG, PNG or WEBP. The form comes back with the reason and everything they typed. |
| The company inbox got no notice for an enquiry that is in the list | The send failed. Look in the application log for `Email not sent`; the usual causes are a sender address Brevo has not verified, or a wrong API key. |

---

## Related

- The other image-upload pipeline (local WebP, service heroes only): `WORKFLOW_SERVICES.md` Step 3.
- Same sortable-list pattern used elsewhere: `WORKFLOW_BOOKINGS.md`, `WORKFLOW_REVIEWS.md`.
