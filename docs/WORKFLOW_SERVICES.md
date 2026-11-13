# 🛠️ Services & Categories — Admin CRUD and the Image Pipeline

How a service gets created, priced, and given a hero image — and the one part of this area that has
**no admin UI at all**: categories.

---

## Step 1 — Categories are seed-only, not admin-managed

> **There is no Categories admin page or controller.** `Areas/Administration/Controllers/` has no
> `CategoriesController` — only `ServicesController` exists, and it only lets you *pick* a category
> for a service via a dropdown.

The three categories that exist (`Plumbing`, `Handyman`, `Small Building & Refurbishments`) come from
`src/Data/HandyFix.Data/Seeding/ServiceCategoriesSeeder.cs`, which runs at every start: it adds a
category that is missing and writes its own name, description and slug over one that exists.
Changing a category's name, description, or adding a fourth category means **editing the seeder
and restarting the app**, not a form submission. (Services are different: their seeder fills an
empty table once and then leaves them to the admin panel, see Step 2.)

`ICategoriesService` only reads categories; there is no code path that creates one besides the
seeder.

**Code:** `Areas/Administration/Controllers/ServicesController.cs` (category dropdown only),
`src/Data/HandyFix.Data/Seeding/ServiceCategoriesSeeder.cs`, `ICategoriesService`.

---

## Step 2 — Services: full CRUD at `/Administration/Services`

> The admin area is routed as `Administration`, not `Admin`. `/Admin/...` will 404.

Standard Create / Edit / Delete via `ServicesController`. Fields: Name, Description, Base Price,
Estimated Duration, Category, Display Order, the Popular tick, the Quote only tick, the Emergency tick, and an optional
hero image upload. The Edit form also has the Active tick. Under the Description sits **The service page**: a short
summary, five list boxes and the page's own questions (see "The service page's own blocks" below).

| Rule | Value |
| --- | --- |
| Name | required, 3–100 chars, and not one another service has or a deleted one had |
| Description | required, 20–3000 chars |
| Short summary | optional, up to 200 chars |
| What we usually find, The visit step by step | optional, up to 3000 chars each |
| Before we arrive, What is included, What is not | optional, up to 2000 chars each |
| A question / its answer | 5–300 / 5–1000 chars; a row left empty is dropped |
| Base Price | 0.01–10,000.00 |
| Duration | 15–1440 minutes |
| Display Order | 0–999, starts at 100 (`Service.DefaultDisplayOrder`) |
| Image upload | optional, **20 MB max**, `.jpg`/`.jpeg`/`.png`/`.webp` only |

**Only a service no job has can be deleted** (`PROJECT_STATE.md` Section 3ef). The list shows a
lock in place of the delete button for a service that a job has, and `ServicesService.DeleteAsync`
refuses one sent anyway; the list then says why and what to do instead: switch it off (the Active
tick, below), which keeps its name on its jobs. The reason is the next paragraph: a deleted row is
left out by every query, so a job that had the service read "None picked" from then on, on its
page, in the job list and in the month's file for the accountant. A line that was taken off a job,
when the job's service was changed, does not count as a job. Technicians follow the same rule
(`WORKFLOW_TECHNICIANS.md`).

**Delete is a soft delete**: the row stays in the database with `IsDeleted` set, because past
bookings point at it, and every query leaves it out. `ServicesService.DeleteAsync` removes the
image file from disk *before* marking the row, so a failed image delete doesn't leave an orphaned
file behind silently. The slug of a deleted service stays taken: the unique index on `Slug` covers
deleted rows too. Because the slug is made from the name, the Add and Edit forms refuse a name whose
slug is already in the table (`ServicesService.NameIsTakenAsync`), with a message on the Name field.

**The slug is auto-regenerated from the Name on every save**, unlike Service Areas (explicit,
editable field — see `WORKFLOW_SERVICE_AREAS.md`). This matters because the slug is load-bearing for
the image filename (Step 3) — renaming a service's Name mid-life renames its image file too.

### Display Order: where a service sits in every list

Every service list sorts by Display Order, then by name: the booking page's list, the category
pages, a service page's related services, the area pages, the admin list. Lower comes first. Most
services share 100, so they stay alphabetical behind the few given a lower number.

**The booking page opens each category on the first service in its list.** The two general
services ("General Plumbing Maintenance", "General Handyman Call-Out") have 0 for that reason.
Giving another service in a category a lower number than its general one changes what a visitor
who just picks "Handyman" lands on.

### The Popular tick: the home page's four

The home page's Popular Services grid holds four. The ticked services come first, in list order.
If more than four are ticked, the first four show; if fewer, the next services in the list fill
the rest, so the grid is never part-empty.

### The Active tick: switching a service off

Unticking "Is Active Service (Visible to Public)" takes a service off sale without deleting it.
It leaves every public list and the booking page, its own page answers 404, the Pricing page
stops showing it among the typical jobs, and it no longer counts towards its category's "from"
price. It stays in the admin list with an Inactive badge, and bookings already made keep it.

### The Quote only tick: no hourly slot

"Quote only (no hourly slot booking)" takes a service out of the booking page's list. Its pages
show "Day Rate / Survey" and "Free Survey & Fixed Quote" in place of a duration and a slot, and
their buttons read "Request Free Estimate" or "Request Quote": they open the Contact form with the
service named, so the job starts as an enquiry (`WORKFLOW_ENQUIRIES.md`). A booking link that names
the service (`/Booking?selectedServiceId=...`) is sent to the same Contact form. The admin list
shows a Quote Only badge. Boiler Installation & Servicing has it, since a boiler is priced after a
survey (`PROJECT_STATE.md` Section 3dv).

Every service in Small Building & Refurbishments is quoted the same way whether ticked or not: the
pages decide it by the category as well as by the tick. The seeder and the `AddIsQuoteOnlyToService`
migration tick them anyway, so the badge in the admin list tells the truth.

### The Emergency tick: phone and WhatsApp only

"Emergency: phone and WhatsApp only, not booked online" is for work that cannot wait for the next
free slot in the 9 to 5 calendar. The phone is answered at any hour, so such a job is taken by
phone or WhatsApp, never booked online. A ticked service:

- is left out of the booking page's list, and a booking link that names it
  (`/Booking?selectedServiceId=...`) opens its own page instead; a booking form sent by hand for it
  is refused with a line that gives the phone number (`BookingController.Index`);
- has a page with the badge "24/7 by Phone & WhatsApp", "We Aim to Arrive In 60 to 90 Minutes",
  "Emergency Rate £X per Hour", and a box with Call and WhatsApp buttons in place of Book a Slot,
  under it a link to book online for anything that can wait; on a phone the floating button reads
  "Call Now" and rings the business;
- has cards (home, category, area and pricing pages) that read "24/7 by Phone" and open its page;
- sets the emergency rate that the FAQ and the Terms quote: its Base Price, so the rate is changed
  in one place (`IServicesService.GetEmergencyRateAsync`).

The admin list shows an Emergency badge. Emergency Plumbing has it, at £150 an hour, set by the
seeder on a new database and by the `AddIsEmergencyToService` migration on one that has services
(`PROJECT_STATE.md` Section 3eg). Every other plumbing page says "Pick a Time That Suits You" where
it used to say "24/7 Available"; "24/7" belongs to the emergency page alone.

### The Gas Safe mark: set by the site, not on the form

Gas work (`Service.IsGasSafeWork`) is not a field on the admin form, so an admin edit or a rename
keeps it. It is set for Boiler Installation & Servicing by the seeder on a new database and by the
`AddBoilerInstallationServicing` migration on one that has services. A marked service shows the
Gas Safe Register logo on its page (in place of "Pick a Time That Suits You", and in a feature box of its own)
and at the bottom right of its card in the category list; its quote box promises a "Gas Safe
registered engineer".

### A blank line in the Description starts a new paragraph

The service page splits the Description at each blank line (`Presentation/Paragraphs.cs`), so a
long description reads as paragraphs. A one-hour service reads "1 Hour", not "1 Hours"
(`Presentation/Durations.cs`).

### The service page's own blocks

`PROJECT_STATE.md` Section 3ep. Under the description the page draws, in this order: a line with
the price (made from Base Price and the ticks, never typed), **What we usually find**, **The
visit, step by step**, **Before we arrive**, **What is included** and **What is not**, the page's
own **questions**, **Worth doing in the same visit** and **Areas we cover**. A box left empty is
not drawn at all, heading included.

- **One point per line** in the five list boxes. The steps are numbered by the page.
- **An icon in square brackets** may start a line of "What we usually find" or "Before we arrive",
  for example `[water_drop]`. The names allowed are listed under the box (`PagePoints.Icons`). A
  name not on the list is refused when the form is saved: the icon font holds only the icons on
  `tools/fonts/icon-names.txt`, and a missing one would show on the page as the word itself. A line
  with no icon gets the box's own (`build` for findings, `info` for before we arrive).
- **The first sentence** of each "What we usually find" line is drawn in bold.
- **The questions** are only about this job. Deposits, cancelling and paying stay on `/FAQ`, which
  the page links to under them. They are also the page's question record for search engines; a
  page with none has no such record.
- **The short summary** is not on the page itself: it is the service's card text on the town pages
  (`Summaries.Of`). Left empty, the card shows the description's first sentence.
- **Worth doing in the same visit** is chosen in code with each page's text
  (`Presentation/SameVisitServices.cs`, by the service's address), and a page with a choice drops
  "You Might Also Need", the three cards of its division at its foot. **Areas we cover** lists
  every town page, so a new town appears by itself.
- The written text of each page is in the database. Pages written so far reached it by a data
  migration per batch that updates by the service's address (`AddServicePageTextsBatch1`, Section
  3eq; `AddServicePageTextsBatch2`, Section 3er; `AddServicePageTextsBatch3`, Section 3es), and
  `ServicePageTexts` holds the same text for the seeder.

### The service page's buttons

A bookable service's page leads with **Book a Slot** (the one filled, dark green button), which
opens `/Booking` with that service chosen on its category's tab. **Request Your Booking** comes
second and opens the Contact form with the service named. On a phone, the floating **Book Now** on
that page goes to the same chosen-service booking; on every other page it opens plain `/Booking`
(`ViewData["BookNowUrl"]`, `_MobileStickyCta.cshtml`). A quote-only service's page has no slot
button, only the quote button, the phone and WhatsApp.

### The seeder fills an empty table, once

`ServicesSeeder` puts its list of services into the Services table **only when the table is
empty**, deleted rows counted. That is a new database: production's first start, a fresh clone, a
test. From then on the catalogue belongs to the admin panel, and the seeder never adds, changes or
restores a service. What an admin sets, renames or deletes survives every deploy.

- **A change to the seeder's list does not reach a database that already has services.** Make it
  in the admin panel on each environment, or write a migration, as
  `AddDisplayOrderAndIsPopularToService` did for the starting Display Order and Popular values.
- **Why it is this strict** (`PROJECT_STATE.md` Section 3cb): the seeder used to look each service
  up by name at every start. A renamed service came back under its old name, a deleted one made
  the insert hit the unique index on `Slug` and stopped the site from starting, and price,
  duration and category were written over whatever an admin had set.
- One part still runs at every start: a service with no `ServiceImage` row gets one, pointing at
  the path its picture would have (Step 3).
- A service in `ServicePageTexts` is seeded with its page's own blocks and questions as well.

### The admin controller names its controller and area when it redirects

Two controllers are called `ServicesController`: the public one, with a fixed address at
`/Services`, and this one. `RedirectToAction("Index")` alone resolved to the public one, so every
save left the admin on the public page. `RedirectToList()` passes the controller and the area. Any
new admin controller that shares a name with a public one needs the same.

**Code:** `Areas/Administration/Controllers/ServicesController.cs`,
`Areas/Administration/Views/Services/{Index,Create,Edit}.cshtml`, `ServicesService`.

---

## Step 3 — A service's picture: built in, or uploaded to the photo bucket

A service's picture is one of two kinds (`PROJECT_STATE.md` Section 3cq):

- **Built in.** The forty pictures that come with the site are files in
  `wwwroot/images/services/`, named `{slug}-hero.webp`, with a 320px copy beside each
  (`{slug}-hero-320.webp`, made by `tools/image-gen/make-phone-copies.mjs`; a test fails if one has
  none). They are each service's default. The code reads that folder and never writes to it.
- **Uploaded** in the admin panel, on Create or Edit. `ImageStorageService` (SkiaSharp) makes it no
  wider than 1920px and WebP at quality 80, with a 320px copy, and puts both in the photo bucket
  (Cloudflare R2, through `ICloudflareR2Service.PutFileAsync`) as
  `services/{random}_{slug}.webp` and `services/{random}_{slug}-320.webp`. The service's
  `ServiceImage` row keeps the picture's address.

Each service's address for its picture is in the database, in its `ServiceImage` row, and the pages
use that. So:

| The admin… | What happens to the picture |
| --- | --- |
| uploads a picture | the new pair goes up under a new name, the service points at it, and only then does the previous upload leave the bucket (`ServicesService.SetServiceImageAsync`); a built-in picture is never deleted |
| renames the service | nothing: the address does not change, whatever the slug becomes |
| deletes the service | after the service is gone, its upload leaves the bucket; a built-in picture stays |

A delete the bucket refuses is logged, not thrown: an object left there costs nothing, and the
admin's save should not fail for it. A new name for every upload also means a browser that kept
the last picture can never show it in place of the new one.

**A picture that cannot be saved.** The service is saved first, so its other changes are kept. A
file the site does not take (not JPEG, PNG or WebP, or not a picture at all) shows its reason under
the picture box; anything else (the bucket out of reach) is logged and reads "The picture could not
be saved. The service itself is saved: try the picture again." A new service whose picture fails
opens on its Edit page with that line, since the Create form sent again would only say the name is
taken (`PROJECT_STATE.md` Section 3dh).

**Why not the site's own folder, as before.** Until Section 3cq an upload was written over
`wwwroot/images/services/{slug}-hero.webp` inside the running container. A deploy starts a new
container from what git holds, so the old picture came back, a new service lost its picture, and a
rename (which moved the file inside the container) left the database pointing at a name the next
container did not have. An upload never reaches git, nor should it. The photo bucket was chosen
over a Docker volume because it ties nothing to one server and the enquiry photos already use it.

The customer photo uploads of bookings and enquiries are a different path
(`ImageService`, capped at 5 files × 15MB, no conversion), into the same bucket under their own
folders.

### Where the `{slug}-hero.webp` convention is still written

For a service with no `ServiceImage` row, by convention only:

1. `src/Data/HandyFix.Data/Seeding/ServicesSeeder.cs`, its last loop — back-fills `ServiceImage` rows
   for services that don't have one yet. `HandyFix.Data` doesn't (and architecturally shouldn't)
   depend on `HandyFix.Services`.
2. `ServiceViewModel`'s Mapster fallback.
3. `ServiceDetailsViewModel`'s Mapster fallback — same pattern, separate file.

A service made in the admin panel without a picture therefore points at a built-in name that does
not exist, and its pages show the general hero picture in its place (each `<img>` has an
`onerror`). The admin's Edit form shows the picture the service has, from its row, and hides the
preview if it cannot be loaded.

**Category hero tiles break this pattern entirely**: they use `{slug}-category-hero.webp` (note
`-category-` inserted), and there is **no admin upload path for them at all** — `ServiceCategory` has
no image column, so category images are a pure file-drop-by-convention, same as area hero images.

---

## Slug generation is centralized in `SlugGenerator`

`ServicesService` and `CategoriesService` used to each carry their own byte-for-byte-identical
private `Slugify` method, with a bug where `.Replace("--", "-")` ran once instead of repeatedly, so
a name producing three or more consecutive hyphens (e.g. `"Walton-on-Thames & Weybridge"`) didn't
fully collapse. Both now call a single shared
`HandyFix.Services.Data.Common.SlugGenerator.Slugify(name)`, which collapses **any** run of hyphens
via `Regex.Replace(slug, "-{2,}", "-")` instead of one non-recursive `Replace`.

`CategoryViewModel.Slug` used to be a *third*, independently-computed slug
(`Name.Replace(" ", "-").ToLower()`, no `&`/`/` handling at all) recalculated on every access. It
now maps straight from the entity's persisted `ServiceCategory.Slug` column instead, so there's a
single source of truth for what a category's slug actually is.

`ServiceAreasService` still deliberately does **not** use `SlugGenerator` — its slug is an explicit,
admin-typed field rather than derived from the name at all (see `WORKFLOW_SERVICE_AREAS.md`), which
was always the safer design for a value that also has to match a hero-image filename.

---

## Quick troubleshooting

| Symptom | Cause |
| --- | --- |
| "I need a new category" has no obvious button | Correct — there isn't one. Edit `ServiceCategoriesSeeder.cs` and restart. |
| A service's image doesn't update after upload | Check the upload succeeded (`ModelState` errors re-render the form); if it did, the browser may be caching the old WebP — hard refresh. |
| An uploaded file was rejected outright | Only `.jpg`/`.jpeg`/`.png`/`.webp` under 20MB are accepted. |
| A service's slug still looks wrong after a name edit | `SlugGenerator` collapses hyphen runs correctly now — if something still looks off, it's a new bug, not the old known one. |
| A new service sits in the wrong place in a list | Its Display Order. 100 is the shared default; a lower number moves it up. |
| The home page shows a service nobody ticked as popular | Fewer than four are ticked, so the list fills the grid. Tick four. |
| A service's page answers 404 | It is switched off (Active unticked) or deleted. |
| A service has a lock where its delete button was | A job has it, so it cannot be deleted. Untick "Is Active Service" on its Edit page: it leaves the website and its jobs keep its name. |
| A service added to `ServicesSeeder.cs` never appears | The table already has services, so the seeder does nothing. Add it in the admin panel. |
| "Another service already has this name" on a name that is nowhere in the list | A deleted service had it. Its row is kept for past bookings, and its slug with it. Use a different name. |
| Category tile image doesn't show | It's a manual file drop (`{slug}-category-hero.webp`), not an admin upload — confirm the file actually exists on disk. |

---

## Related

- Adding/editing a service area (a separate, explicit-slug pattern deliberately different from
  this one): `docs/WORKFLOW_SERVICE_AREAS.md`.
- Booking/enquiry photo uploads (the *other* image pipeline, R2-backed): see `ImageService` in
  `src/Services/HandyFix.Services/ImageService.cs`.
- Full architectural history: `PROJECT_STATE.md` Section 3j (hero WebP migration), 3k (legacy sweep
  removal), Section 4 Tier 1 item 1 (bulk image generation for launch), Section 3ca (display order,
  the Popular tick, and what switching a service off does).
