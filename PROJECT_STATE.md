# HandyFix — Project State & Architecture Roadmap

> **Purpose**: This is the permanent architectural memory for HandyFix. It records what the system actually is (not aspirational template boilerplate), what's been built and verified, and what's left. Update it at the close of each sprint rather than letting it drift out of sync with the code.
>
> **Last updated**: 2026-09-10 — **Mobile UX, Join Our Team form & pricing consistency pass.** Fixed the mobile menu opening off-screen (a Section 3aq regression); Zapryan as the Local Expert on every service page; compact mobile category cards; thumbnails on recommendation and area cards; a dedicated `/JoinOurTeam` application form; a three-action booking CTA; About page button/icon polish; and every price statement found contradicting the flat £90/£60 rates or the £50 deposit. Same-day follow-ups: the seeder now syncs service category (two Handyman services were listed under Plumbing at £60), client-side validation works again on every form, and form confirmations show without cookie consent. The Contact form no longer accepts an empty message, and its category options now come from the real service categories. Legal pages and data protection added to the roadmap (Tier 3 item 24). Full suite 158/158 green. See Sections 3av–3ax.

---

## 1. Tech Stack & Core Architecture

### Backend
- **ASP.NET Core MVC on .NET 10**, structured as Clean Architecture with strict layer separation:
  - `Data/` — `HandyFix.Data.Models` (entities), `HandyFix.Data.Common` (repository interfaces, base model classes), `HandyFix.Data` (EF Core `ApplicationDbContext`, migrations, repositories, seeders).
  - `Services/` — `HandyFix.Services.Data` (business logic services, one per domain area: Bookings, Availability, Payments, Services, Reviews, Inquiries, Categories), `HandyFix.Services.Mapping` (object mapping), `HandyFix.Services.Messaging` (email), `HandyFix.Services` (cross-cutting: image upload, R2 storage).
  - `Web/` — `HandyFix.Web` (MVC controllers, views, `Program.cs` composition root, background workers), `HandyFix.Web.ViewModels` (DTOs/view models, kept out of the Data layer).
  - `Tests/` — `HandyFix.Services.Data.Tests` (service-layer unit tests), `HandyFix.Web.Tests` (full-stack `WebApplicationFactory` integration tests).
- **Object mapping**: [Mapster](https://github.com/MapsterMapper/Mapster), via a custom `IMapFrom<T>`/`IMapTo<T>`/`IHaveCustomMappings` convention registered once at startup (`MappingConfig.RegisterMappings`). Services are plain classes with direct async methods, not CQRS command/query handlers — there is no AutoMapper and no MediatR anywhere in the codebase. *(The README used to claim both; corrected 2026-08-01, see Section 3s.)*
- **Repository pattern**: `IRepository<T>` / `IDeletableEntityRepository<T>`, generic EF Core-backed implementations (`EfRepository<T>`, `EfDeletableEntityRepository<T>`). Soft-delete (`IsDeleted`/`DeletedOn`) and audit fields (`CreatedOn`/`ModifiedOn`) are applied globally via `ApplicationDbContext.OnModelCreating`/`SaveChanges` overrides, not per-entity boilerplate.

### Database
- **SQL Server**, run locally via a Docker container (`MSSQLServer`, port 1433) for development.
- **Entity Framework Core 10.0.5**, code-first migrations under `src/Data/HandyFix.Data/Migrations/`.
- All entities use **`Guid` primary keys** (`BaseModel<Guid>` / `BaseDeletableModel<Guid>`), generated client-side in entity constructors.
- **Centralized package management**: `src/Directory.Packages.props` (all NuGet package versions pinned in one place, `ManagePackageVersionsCentrally=true`) and `src/Directory.Build.props` (shared `TargetFramework=net10.0`, StyleCop analyzers, language version) — individual `.csproj` files only declare `<PackageReference Include="..." />` with no version.

### Storage & Static Assets
- **Cloudflare R2** (S3-compatible object storage) via `CloudflareR2Service`/`ICloudflareR2Service`, used for *user-uploaded* content: booking problem photos and inquiry photos (`ImageService.UploadImagesAsync`, capped at 5 files / 15MB each, JPEG/PNG/WebP only).
- **Local optimized WebP assets** under `wwwroot/images/`, used for *admin-curated* content: service category hero images. Pipeline is `ImageStorageService` (SkiaSharp-based): resizes uploads >1920px wide, re-encodes to WebP at quality 80, deletes legacy JPG/PNG on save. **This pipeline is scoped only to `images/services/`** and only runs on an actual admin upload — it does not touch the homepage hero image or any area/marketing images. There is no longer any startup sweep over the images directory (see Section 3k). Assets produced outside the app are expected to arrive already WebP-encoded and correctly named; nothing converts them on boot.

### Frontend
- Razor views + Bootstrap 5 + vanilla JS. CSS is organized under `wwwroot/css/` as `base/`, `components/`, `pages/` partials imported into a thin `site.css` (not a single monolithic stylesheet). A shared `page-container` class provides a consistent boxed hero/content width across every public page. WebOptimizer is registered for CSS/JS bundling.
- Design language documented separately in `DESIGN.md` (color palette, typography, spacing, animation conventions).

### Testing
- **xUnit + Moq**, at two levels: service-layer tests (`HandyFix.Services.Data.Tests`) and controller tests (`HandyFix.Web.Tests/Controllers/`, added Section 3t — controllers constructed directly with mocked services, asserting on the returned `IActionResult`, no database).
- **`HandyFix.Web.Tests` also holds full-stack integration tests** driven through `SqliteWebApplicationFactory`, which boots the real `Program` against Sqlite in-memory rather than the configured SQL Server. Do not replace it with a bare `WebApplicationFactory<Program>`: that runs migrations and seeding against the developer's actual dev database and cannot run in CI at all (Section 3t).
- Tests run against **EF Core InMemory** for simple CRUD-style assertions, and **EF Core Sqlite (`:memory:`)** for anything that depends on real transactions or concurrency-token enforcement — InMemory silently no-ops both `BeginTransaction`/`Commit`/`Rollback` and optimistic-concurrency checks, so it cannot verify rollback or race-condition behavior. This distinction is load-bearing; don't "simplify" a Sqlite-backed test back to InMemory without checking why it was Sqlite first.

### Hosting & Infrastructure (provisioned 2026-07-30; staging half wired up 2026-08-04, see Section 3w)

> Real IPs are deliberately not in this file — see `docs/private/INFRASTRUCTURE.md` (gitignored) for the actual values behind the placeholders below.

- **Provider**: Hetzner Cloud, Helsinki region. **Three isolated VPS instances** (CX23 — 2 vCPU / 4 GB RAM / 40 GB NVMe each), one per tier, all attached to a private network (`handyfix-internal`, `10.0.0.0/16` — see the private file for real octets):
  - Database host (`<DB_PUBLIC_IP>` / `<DB_PRIVATE_IP>`) — Dockerized MS SQL Server 2022 (container `handyfix-sql`), memory-capped at 3 GB (`MSSQL_MEMORY_LIMIT_MB=3072`) to prevent OS starvation on a 4 GB host. `handyfix_staging` is live and in use; `handyfix_prod` exists but is still empty.
  - Staging web host (`<STAGING_PUBLIC_IP>` / `<STAGING_PRIVATE_IP>`) — **live**, running `web` + `caddy` via `deploy/docker-compose.staging.yml`, deployed automatically on every push to `dev` (Section 3w).
  - Production web host (`<PROD_PUBLIC_IP>` / `<PROD_PRIVATE_IP>`) — provisioned, nothing deployed to it yet.
  - Both web servers have Docker + Docker Compose installed with auto-start on boot.
- **Network isolation, deliberately**: web apps reach SQL Server exclusively over the private interface (`<DB_PRIVATE_IP>`) — database traffic never touches the public internet. Root password login is disabled fleet-wide; SSH access is public-key only.
- **Staging is done** (Section 3w): `.github/workflows/deploy-dev.yml` builds/tests/deploys on every push to `dev`; `Database.Migrate()`/seeding run automatically on startup against `handyfix_staging` via a dedicated least-privilege SQL login; config arrives as environment variables into the container (`deploy/.env` on the server), **not** `appsettings.Staging.json`/`appsettings.Production.json` files — that was the original plan here, revised once the Docker approach was built (see Section 3w).
- **Still not done** — tracked in the roadmap as Tier 4: the same setup for production (`deploy-prod.yml`, deliberately deferred until staging has been in use for a while rather than built alongside it), and the app-level connection string against `<DB_PRIVATE_IP>` for `handyfix_prod` once that happens.
- **Domain**: not yet wired up, and entangled with the pending rebrand decision — see `docs/private/VISION_AND_CONTEXT.md` Sections 5.3 and 5.6 and roadmap Tier 3 item 14 below. Staging (and initial production, if needed) can run on Hetzner's free reverse-DNS hostname in the meantime, so this does not block standing up staging.

---

## 2. Completed Milestones — Sprint 1: Core Booking Engine & Payments

All items below are implemented, unit-tested, and merged to `main`.

### Slot Availability Logic
- `AvailabilityService.GetAvailableDatesAsync` / `GetAvailableSlotsForDateAsync` / `GetAllSlotsForDateAsync` filter out past hours for "today" by comparing against `DateTime.Now` (not just `DateTime.Today`), so a slot that already started stops showing as bookable mid-day.
- `BookSlotAsync` / `ReleaseSlotAsync` / `BlockSlotAsync` perform real, immediate database checks against `IsBooked`/`IsBlocked` — no client-trusted state.
- Admins can manually generate, block, and release slots via `Areas/Administration/Controllers/CalendarController`.

### Double-Booking Protection
- `AvailabilitySlot.RowVersion` (`[Timestamp] byte[]`) is an EF Core optimistic-concurrency token, backed by a real SQL Server `rowversion` column (migration `20260724001734_AddRowVersionToAvailabilitySlot`).
- `BookSlotAsync`/`ReleaseSlotAsync` catch `DbUpdateConcurrencyException` and return `false` rather than letting a losing request silently overwrite the winner.
- `IDbQueryRunner.BeginTransactionAsync` wraps multi-step operations (create booking + claim slot; release old slot + claim new slot; abandon booking + cancel its payment) in one real database transaction, so a failure partway through rolls back everything instead of leaving inconsistent state.
- A one-to-one `Booking` ↔ `AvailabilitySlot` relationship is enforced at the DB level via a unique, filtered index on `AvailabilitySlot.BookingId`.
- Verified with SQLite in-memory databases specifically where rollback/concurrency behavior needed to be exercised for real (InMemory can't do this — see Testing above).

### Booking Workflow & Rescheduling
- `BookingsService.CreateBookingAsync` and `RescheduleBookingAsync` route every slot mutation through `IAvailabilityService`'s concurrency-safe methods instead of mutating `AvailabilitySlot` directly.
- `SlotUnavailableException` is a dedicated exception type distinguishing "someone else took this slot" from other failure reasons, letting the controller react specifically.
- `BookingController` catches `SlotUnavailableException`/`DbUpdateConcurrencyException` separately: it clears the submitted `SlotId` and redisplays the form with a friendly message ("This slot was just taken by someone else, please pick another"), preserving every other field and uploaded photo the customer already entered — no data loss, no generic 500.
- `StaleBookingCleanupService` (`IHostedService`, in `Web/HandyFix.Web/BackgroundServices/`) runs every 5 minutes, finds `Pending` bookings older than 15 minutes with no completed payment, flips them to a new `Abandoned` booking status, and releases their slots — this is the fix for the "ghost Stripe checkout" problem where an abandoned payment would otherwise lock a slot forever.

### Stripe & Payments Integration
- `PaymentController`'s Stripe sandbox bypass requires **both** a missing `Stripe:SecretKey` **and** `IWebHostEnvironment.IsDevelopment()` — a missing key outside development throws immediately instead of silently faking a successful payment.
- `PaymentsService.CreatePaymentRecordAsync` supersedes (cancels) any existing `Pending` payment for a booking before creating a new one, preventing orphaned duplicate rows when a customer retries checkout.
- Stripe webhook (`PaymentController.Webhook`) handles both `checkout.session.completed` (→ `ProcessPaymentSuccessAsync`) and `checkout.session.expired` (→ `CancelPaymentAsync`, which never overwrites a payment that already succeeded).
- `StaleBookingCleanupService`'s abandonment sweep is atomic with payment cleanup: `BookingsService.ReleaseAbandonedBookingsAsync` cancels the associated `Pending` payment inside the same transaction as the booking/slot release.

### Email Dispatcher
- `IEmailSender` resolves to `SendGridEmailSender` when `SendGrid:ApiKey` is configured, falls back to a no-op `NullMessageSender` only in development, and throws at resolution time otherwise (same fail-loud-outside-dev pattern as Stripe). *(Superseded 2026-07-31 — SendGrid was swapped for Brevo/`BrevoEmailSender`, same fail-loud pattern; see Section 3q.)*
- `PaymentsService.ProcessPaymentSuccessAsync` — the method both the webhook and the browser `Success` redirect call — sends a client confirmation email and an admin notification (to a configurable `Admin:NotificationEmail`, defaulting to the seeded admin mailbox) with real booking/service/technician details.
- An idempotency guard ensures these emails fire **strictly on the first transition** to `DepositPaid`, so the webhook/redirect race (both firing for the same Stripe session) can't send duplicate confirmations.

---

## 3. Completed Milestones — Sprint 2: UI/UX & SEO

All items below are implemented and merged to `main`. Sprint 2 was audited on 2026-07-24 and implemented the same day.

### SEO Fixes
- `Home/Index.cshtml` no longer overwrites the controller's SEO-friendly `<title>` with a generic "Home Page" — the view-level `ViewData["Title"]` assignment that clobbered it was removed.
- `_Layout.cshtml` now renders `<meta name="description">` from `ViewData["MetaDescription"]` when set. That value is now populated on every public page's controller action (Home, Contact, Reviews, About, FAQ, ServiceAreas, Privacy, Terms, CookiePolicy, Booking, Services/Category, Services/Details, Services/Pricing) — previously only 3 of these ever set it.
- `robots.txt` added under `wwwroot/`, disallowing `/Administration/`, `/Identity/`, `/Payment/`, `/Settings/`, and pointing to the sitemap.
- `SeoController` (`Web/HandyFix.Web/Controllers/SeoController.cs`) serves a dynamically generated `sitemap.xml` — static routes plus every category and service slug pulled live from `ICategoriesService`/`IServicesService`, so it never goes stale as services are added/removed via the admin panel.
- **Not fixed, deliberately** — the JSON-LD structured data on `Home/Index.cshtml` and `Services/Details.cshtml` still contains fabricated business content (invented technician names/bios, an unverified "£5M public liability insurance" claim, a made-up completed-jobs count/rating). This needs real business input, not engineering, and was explicitly left alone this sprint per user direction.

### Pricing Page
- New `Services/Pricing.cshtml` view + `ServicesController.Pricing()` action (route name `Pricing`), listing every service grouped by category with hourly rate and estimated duration, sourced from the same `CategoryViewModel`/`ServiceViewModel` data the rest of the Services area already uses — no new data modeling was needed.
- The dead `href="#"` / `<!-- TODO: Pricing page -->` link on the homepage booking widget now points at this page via `asp-route="Pricing"`, and a "Pricing" link was added to both the desktop and mobile nav.
- New `wwwroot/css/pages/pricing.css`, imported from `site.css`.

### Inline-Style Cleanup (Public Views)
- All ~253 inline `style="..."` occurrences across the 14 public views that had them (`Home/Index`, `Privacy`, `Reviews`, `Contact`, `About`, `FAQ`, `Terms`, `Booking/Confirmed`, `Payment/Cancel`, `ServiceAreas`, `Booking/Index`, `Services/Category`, `Services/Details`, `_Footer`, `_LoginPartial`) were converted to CSS classes — either existing page-specific classes, new semantic classes added to the relevant `pages/*.css` file, or a new `wwwroot/css/base/utilities.css` for repeated spacing/typography/opacity/border-radius patterns (`mb-*`/`mt-*`, `fs-*`, `lh-*`, `opacity-*`, `rounded-lg`/`rounded-xl`/`rounded-full`, `icon-fill`/`icon-unfill`, `max-w-*`, etc.).
- Two intentional exceptions remain, both dynamic per-instance `background-image: url(...)` values (`Home/Privacy.cshtml`'s hero banner and `Services/Details.cshtml`'s per-service hero) — these are content data, not design-system values, same category as an `<img src>`.
- This pass also fixed several latent bugs uncovered along the way: multiple views referenced Bootstrap-*looking* classes that were never actually defined (`rounded-lg`, `rounded-xl`, `rounded-full`, `opacity-70`, `max-w-xl`/`max-w-2xl`/`max-w-7xl`) and were silently no-ops; `utilities.css` now defines all of them for real. `Services/Category.cshtml`'s "Details" button also referenced a non-existent `var(--border)` CSS variable (only `--border-subtle` exists), which meant it was likely rendering with no border at all — fixed to use `--border-subtle` like every other button of its kind.
- Verified visually: the app was run locally and screenshotted (Home, Pricing, Contact, FAQ, Reviews, About, Privacy) — no layout regressions.

### Image Loading Attributes
- Added `loading="lazy"` to every below-the-fold `<img>` in public views; the homepage hero image (the LCP candidate) was explicitly kept eager and given `fetchpriority="high"` instead.
- Added explicit `width`/`height` wherever a real intrinsic size was knowable (`hero.png` is 2752×1536; the plumbing/handyman category heroes are 1024×1024 each — dimensions read directly from the file headers since these are static assets). Where the source is dynamic (per-service uploaded images) or external, the containing element's own fixed CSS box size was used instead (e.g. 96×96 for the local-expert avatar, 256×256 for the services-page proof image), which is the correct technique when the true source size isn't controlled.
- **`srcset` was not added** — every image on the site currently exists at a single resolution (see the Images gap below), so there are no size variants to put in a `srcset` yet. Adding one meaningfully requires either sourcing multiple resolutions or extending `ImageStorageService` to generate them, both bigger than an attribute-only fix.
- **Found but not fixed**: `wwwroot/images/hero.png` is an unoptimized 6.6MB PNG (2752×1536) used as the homepage hero, About page image, and Trust section image — the single biggest real performance cost on the site, and outside the WebP pipeline (which only covers `images/services/`). `Services/Index.cshtml`'s `quality-proof-image` also points at `wwwroot/images/handyfix-proof.jpg`, which doesn't exist on disk at all — it always falls through to the `onerror` placeholder.

---

## 3a. Completed Milestones — Sprint 3 (partial): Admin Inline-Style Cleanup & CSS Bugfixes

Pulled forward from Sprint 3 and completed on 2026-07-24, same pattern as the Sprint 2 public-views pass.

- All 343 inline `style="..."` occurrences across the 10 admin views that had them (`Bookings/Index`, `Bookings/Details`, `Calendar/Index`, `Enquiries/Index`, `Enquiries/Details`, `Reviews/Index`, `Services/Index`, `Services/Create`, `Services/Edit`, `Dashboard/Index`) were converted to CSS classes — new admin-specific semantic classes in `wwwroot/css/pages/admin.css` (`admin-card`, `detail-label`, `status-badge`, `admin-table-head`, `avatar-badge`, `financial-summary-card`, `admin-form-control`, `help-card`, etc.), plus shared `dim-*`/`tint-*` sizing and color helpers added to `wwwroot/css/base/utilities.css`.
- Two exceptions were kept inline deliberately: the two `health-progress-fill` bar widths on `Dashboard/Index.cshtml`, because the page's script reads `element.style.width` directly to drive a load animation — moving that value into a CSS class would silently break the animation (`element.style` only reflects inline styles).
- **Found and fixed a much larger, previously-undiscovered bug while doing this**: `font-bold` — used 50 times across 15 views, public and admin alike — had no CSS definition anywhere in the project and was a pure no-op sitewide; every element meant to render bold text had been rendering at normal weight since Sprint 2 (and before). Also fixed in the same pass: `font-medium`, `text-right`/`text-left` (Bootstrap 5 renamed these to `text-end`/`text-start`, so the originals never worked), and a half-defined color-token family (`text-on-surface`, `text-on-secondary-container`, `text-on-tertiary-container`, `text-outline`, `text-error` were referenced but never defined, alongside siblings like `text-on-surface-variant` that were). These fixes live in `utilities.css`/`typography.css` and take effect on already-shipped Sprint 2 public pages too, not just the admin views touched this pass.
- Verified by running the app locally, logging in as the seeded admin, and screenshotting every converted page — no layout regressions.

---

## 3b. Availability Slots Logic Audit (2026-07-24)

Audited `AvailabilityService`, slot generation, and booking-concurrency handling end to end against three concerns: past-slot filtering, double-booking prevention, and slot release on expiry/cancellation.

- **Past-slot filtering**: correct as-is. `GetAvailableDatesAsync`/`GetAvailableSlotsForDateAsync`/`GetAllSlotsForDateAsync` all compare against `DateTime.Now` (not just `DateTime.Today`), so an hourly slot that has already started stops appearing as bookable partway through the day. Already covered by `GetAvailableDatesAsyncShouldExcludeTodayWhenAllOfTodaysSlotsHaveAlreadyPassed`.
- **Double-booking prevention**: correct as-is. `BookSlotAsync`/`ReleaseSlotAsync` both catch `DbUpdateConcurrencyException` from `AvailabilitySlot.RowVersion` and return `false` rather than letting a losing request silently overwrite the winner; `CreateBookingAsync`/`RescheduleBookingAsync` wrap the slot claim in a real transaction and roll back cleanly via `SlotUnavailableException` on conflict.
- **Bug found and fixed**: `BookingsService.CancelBookingAsync` was the one slot-mutating path with no concurrency handling at all — if a concurrent process (chiefly `StaleBookingCleanupService`'s 5-minute abandonment sweep) touched the same slot at the same moment an admin clicked "Cancel Booking", `SaveChangesAsync` would throw an uncaught `DbUpdateConcurrencyException`, surfacing as an unhandled 500 to the admin instead of the cancellation succeeding. Fixed using EF Core's documented conflict-resolution pattern: on catch, refresh each conflicting entry's original values from the database via `GetDatabaseValuesAsync`/`OriginalValues.SetValues` (or detach if the row is gone) and retry the save once — the booking's own status change has no concurrency token, so it's never what conflicts and is safe to resend unchanged. Covered by `CancelBookingAsyncShouldRecoverWhenSlotWasConcurrentlyModified`.
- **Observation, not fixed**: `IAvailabilityService.GetAvailableSlotsForDateAsync<T>` has no production caller anywhere (only `GetAvailableDatesAsync` and `GetAllSlotsForDateAsync` are actually used, by the public `BookingController`) — looks like dead code, left alone since removing it wasn't part of this audit's scope.

---

## 3c. Admin Layout Polish: Prominent Public-Site Link (2026-07-24)

- `_AdminTopbar.cshtml`'s link back to the public site was a plain text `<a>` reading "Home" — easy to miss and ambiguous with the admin's own "Dashboard". Replaced with a button-styled `.admin-view-site-link` (icon + "View Public Site" label, bordered card treatment matching the rest of the admin chrome) that opens the public site in a new tab (`target="_blank" rel="noopener"`), so the admin/technician can jump back and forth without losing their place in the admin panel.

---

## 3d. Admin Bookings Index: Submitted-On Column, Sorting, and Status Filter (2026-07-24)

- Added a "Submitted On" column to `Bookings/Index.cshtml` showing `Booking.CreatedOn` (stored strictly in UTC per `ApplicationDbContext.SaveChanges`) converted to local time via `.ToLocalTime()` before formatting — the only column on this page that needed a UTC conversion, since `AvailabilitySlot.StartTime` is generated and stored as naive local wall-clock business hours already.
- `IBookingsService.GetAllBookingsAsync<T>` now takes `BookingSortField sortField = CreatedOn, bool descending = true, string statusFilter = null` (new `BookingSortField` enum: `CreatedOn`, `AppointmentTime`, `CustomerName`, `Status`, defined in `HandyFix.Web.ViewModels.Booking` alongside the other Booking DTOs, since `HandyFix.Services.Data` already depends on that project — not the other way around — so that's the layer both sides can share). Sorting and status filtering happen as a real `IQueryable<Booking>` `OrderBy`/`Where` before the Mapster projection, translated fully to SQL, not done in memory. Default behavior (creation date, newest first) is unchanged from before this change.
- `Bookings/Index.cshtml`'s "Customer", "Scheduled Date", and "Booking Status" column headers are now clickable sort toggles (arrow indicator on the active column, click again to reverse direction); a status filter `<select>` was added next to the existing client-side Ref-ID/customer-name search box, which stays client-side since it's already instant and works well for this data volume.
- `BookingsController.Index` now also fetches an always-unfiltered booking list to compute the "Today's Appointments"/"Pending Approval"/"Monthly Revenue" summary cards, so applying a status filter to the table no longer skews those figures — they were previously computed from whatever the (now-filterable) list happened to contain.
- **Bug caught during verification, fixed before committing**: a hidden `<input type="hidden" name="descending" value="@Model.Descending" />` used to persist the current sort direction across a status-filter form submit was corrupted by Razor's automatic boolean-attribute collapsing (any HTML attribute whose interpolated value has static type `bool` gets rendered as a valueless attribute, e.g. `value` with no `="..."`, which browsers then read back as `value="value"` per the HTML boolean-attribute spec). Fixed by forcing the value through `.ToString()` first so Razor sees a `string`, not a `bool`, and doesn't apply the collapsing behavior.
- Verified by running the app locally and screenshotting the default view, a customer-name-sorted view, and a status-filtered view — sorting, filtering, and the summary cards all behave correctly.

---

## 3e. Admin Enquiries List: Sorting (2026-07-24)

Extends the sortable/queryable "Order by" pattern built for Bookings (Section 3d) to the Enquiries admin list, one of the two remaining lists named in the Sprint 3 roadmap line.

- Added `InquirySortField` (`CreatedOn`, `Name`, in `HandyFix.Web.ViewModels.Administration.Enquiries` alongside `EnquiryViewModel`) and `IInquiriesService.GetAllAsync<T>(sortField, descending)`, applied as a real `IQueryable` `OrderBy` translated to SQL. No status filter was added here — `Inquiry` has no status-like field to filter on.
- `EnquiriesController.Index` and `Enquiries/Index.cshtml` now use a new `EnquiryListViewModel`; the "Client Details" and "Submission Date" column headers are clickable sort toggles, same interaction pattern as Bookings (arrow indicator, click again to reverse).
- Covered by new tests: `InquiriesServiceTests.GetAllAsyncShouldDefaultToCreatedOnDescending`/`GetAllAsyncShouldSortByNameAscendingWhenRequested`.

---

## 3f. Admin Reviews List: Sorting and Status Filtering (2026-07-24)

Extends the same pattern to the Reviews admin list, the last of the three lists named in the Sprint 3 roadmap line — that line is now fully done.

- Added `ReviewSortField` (`CreatedOn`, `CustomerName`, `Rating`, in `HandyFix.Web.ViewModels.Reviews`) and extended `IReviewsService.GetAllAsync<T>` with `sortField`/`descending`/`statusFilter` parameters. Since `Review.IsApproved` is a bool rather than a named status entity, `statusFilter` is a string (`"Approved"`/`"Pending"`) matched against it in the query rather than joined against a status table.
- `ReviewsController.Index` now also recomputes the summary stat cards (Average Rating, Total Published, Approval Rate, Pending count) from an always-unfiltered fetch, same reasoning as Bookings' summary cards — applying the new status filter to the table shouldn't skew them.
- `Reviews/Index.cshtml` uses a new `ReviewListViewModel`; "Customer", "Rating", and "Date" headers are sort toggles, and a status filter `<select>` (All/Approved/Pending) sits next to the existing "Refresh List" button, reusing the `admin-status-filter`/`admin-sort-link` CSS already added for Bookings — no new CSS was needed.
- Covered by new tests: `ReviewsServiceTests.GetAllAsyncShouldSortByRatingDescendingWhenRequested`/`GetAllAsyncShouldFilterByApprovalStatusWhenRequested`.
- Verified both this and the Enquiries page (Section 3e) by running the app locally (Playwright driver script, since `chromium-cli` isn't available in this Windows environment), logging in as the seeded admin, and screenshotting both pages default/sorted/filtered — sorting, filtering, and the unfiltered summary stats all behave correctly. `console --errors` surfaced a pre-existing `s.parseJSON is not a function` error from the bundled `jquery-validation-unobtrusive` library (fires on the login page, unrelated to this change) and some pre-existing 404s for missing static assets (see Images gap below) — neither is new.

---

## 3g. Pricing Page Rebuild: Stitch Design Migration (2026-07-24)

Sprint 3 closed (list refinements + admin usability polish complete, per Sections 3a-3f). The next initiative — rebuilding the public Pricing page from a Google Stitch-generated visual design — began the same day. The Stitch output (Tailwind/inline-style HTML) was treated strictly as a design reference, not copy-pasted; the page was rebuilt natively against existing Razor/CSS conventions and real `CategoryViewModel`/`ServiceViewModel` data.

- **New reusable partial**: `Views/Shared/_PricingCard.cshtml` + `PricingCardViewModel` (Name, IconName, Description, BasePrice, EstimatedDurationMinutes, optional CtaText/CtaUrl) — a service-level card, styled via a new `.pricing-card` class (`pricing.css`, modeled on the existing `.trust-card` token set: `--radius-xl`/`--shadow-lg`→`--shadow-xl` on hover). Used in three places: the Pricing page's "Typical Job Costs" section, and a new "You Might Also Need" section on `Services/Details.cshtml` (2-3 sibling services from the same category, via a new `ServiceDetailsViewModel.RelatedServices` property populated in `ServicesController.Details()`).
- **Pricing page** (`ServicesController.Pricing()` now returns a new composed `PricingViewModel { Categories, TypicalJobs }`, modeled on the existing `HomeIndexViewModel` pattern): the old exhaustive per-category service table was replaced with — a hero with a "Book Now" CTA; an "Our Hourly Rates" section looping real `ServiceCategory` rows into `.glass-card.rate-card`s (icon via a `GetCategoryIcon` name-keyed switch, `"From £X per hour"` from `CategoryViewModel.BasePrice`, a "Minimum charge 1 hour" pill); a "Rates Explained"/"Materials & Consumables" two-column bento section; a "Typical Job Costs" grid of `_PricingCard`s for four hand-picked real services (`tap-repairs`, `tv-mounting`, `shelf-installation`, `minor-electrical-tasks` — a curated `TypicalJobSlugs` array in the controller, not a category-balance rule, hence 1 Plumbing/3 Handyman); a "Local Service Areas" teaser (real South London towns, linking to the full `Home/ServiceAreas` page); and a Pricing-specific FAQ accordion reusing the exact `.faq-accordion`/`toggleAccordion()` pattern from `Home/FAQ.cshtml`.
- The rate-card and Typical-Job-Costs grids both use one new shared CSS class, `.pricing-cards-grid` (`repeat(auto-fit, minmax(260px, 1fr))`), deliberately not a fixed column count — the "Electrical Repairs" card from the Stitch design doesn't appear because only Plumbing and Handyman exist as real `ServiceCategory` rows (`ServiceCategoriesSeeder.cs`), not because of a name-based skip; any category added later will appear automatically without layout changes.
- **Not fixed, deliberately** — several figures in the Stitch design don't correspond to anything in the data model or existing site copy and were confirmed by the user to be placeholder content, not real business facts: a tiered "£X first hour / £Y per ¼ hour thereafter" rate (only a single flat hourly `BasePrice` exists per service), specific "Monday to Saturday 8am–6pm" operating hours, a VAT-exempt disclaimer, and a 25% materials handling fee citing the Consumer Rights Act 2015. All were omitted rather than hardcoded (same treatment as the existing fabricated £5M insurance claim noted in Section 3, Sprint 2 SEO notes) — needs real business input before these specifics can be added.
- **Tech debt fixed along the way**: `.glass-card` was defined twice — the real, token-based definition in `wwwroot/css/components/cards.css`, and a second hardcoded (non-variable, no radius/shadow) redefinition in `wwwroot/css/pages/pages-info.css`. Because `pages-info.css` loads after `components/cards.css` in `site.css`, the hardcoded copy was silently winning everywhere `.glass-card` was used (e.g. `Services/Details.cshtml`'s hero badges), suppressing the intended blur/radius/shadow. The duplicate was removed; the token-based definition now applies everywhere.
- Verified: `dotnet build` (0 errors), `dotnet test` on both `HandyFix.Services.Data.Tests` and `HandyFix.Web.Tests` (all pre-existing tests still pass unchanged — no service-layer signatures were touched), plus a local run + Playwright-driven browser check of `/Pricing` and a Service Details page.

---

## 3h. Our Areas: Dynamic Service-Area Pages (2026-07-24)

Full "Our Areas" feature shipped end-to-end, built from a prior research brief (competitive analysis of Surrey Handyman's area-page structure, a Chessington-based 90-minute drive-time radius, and a top-15 shortlist balancing proximity/market value/consistency with the site's existing South-London footprint). Implemented incrementally as eight reviewed phases, each its own Conventional Commit; every phase compiled cleanly and the full test suite passed before moving to the next.

- **Data layer**: `ServiceArea`/`ServiceAreaFaq` entities (`BaseDeletableModel<Guid>`, unique `Slug` index), following `ServiceCategory`'s exact shape. No `HeroImageUrl` column — area hero images are resolved by convention from `Slug` at the view layer (`wwwroot/images/areas/{slug}-hero.webp`), the same pattern `ServiceCategory` already uses for its own hero images. Migration `AddServiceAreaAndServiceAreaFaq` applied and verified against the real database. `ServiceAreasSeeder` seeds all 15 approved areas (Chessington, Surbiton, Kingston upon Thames, Worcester Park & Ewell, Epsom, Sutton, Banstead, Esher, Leatherhead, Wimbledon, Cobham, Walton-on-Thames & Weybridge, Reigate, Dorking, Guildford) with real, non-placeholder intro/neighbourhood copy and FAQs — no fabricated founding dates or fake testimonials, matching the discipline already established for the Pricing page rebuild.
- **Service layer**: `IServiceAreasService`/`ServiceAreasService` (`GetAllAsync`, `GetBySlugAsync`, `GetNearestAsync`) mirrors `CategoriesService` exactly — inject the generic repository directly, real `IQueryable` translated to SQL (including `GetNearestAsync`'s `Math.Abs(DriveTimeMinutes)` ordering).
- **View models**: `ServiceAreaViewModel` (grid/card), `ServiceAreaFaqViewModel`, and an independent `ServiceAreaDetailsViewModel` (not a subclass — mirrors the existing `ServiceViewModel`/`ServiceDetailsViewModel` parallel-class precedent, not inheritance). `ServiceAreaDetailsPageViewModel` composes `Area` + `RelatedServices` + `Reviews`, reusing the existing `ServiceViewModel`/`ReviewViewModel` types rather than inventing new ones.
- **Routing**: `AreasController` at `/Areas` (route name `Areas`) and `/Areas/{areaSlug}` (route name `AreaDetails`), same attribute-routing convention as `ServicesController`.
- **`AreaCardViewComponent`** — the project's first `ViewComponent`. It owns its own data fetch so any view can drop in `<vc:area-card />` without its controller needing to pre-populate area data. **Bug found and fixed during manual verification**: passing `excludeAreaId` only filtered the default featured-first list rather than actually querying nearest-by-drive-time, so the Area Details page's "Nearby Areas" block showed unrelated featured areas instead of true neighbours (e.g. Cobham showed Chessington/Kingston/Epsom/Sutton instead of Wimbledon/Walton-Weybridge/Guildford). Fixed so `excludeAreaId` now triggers `GetNearestAsync`; covered by a regression test asserting the correct nearest-neighbour link appears.
- **Areas Index page** (`Views/Areas/Index.cshtml`): hero with an area-count badge, a coverage-map image, a "Featured Areas" teaser and a full "All Areas We Cover" grid, both rendered via `<vc:area-card>` (never inline markup or a partial, per the ViewComponent-only rule). No pagination — 15 areas fit the auto-fit grid comfortably.
- **Area Details page** (`Views/Areas/Details.cshtml`): dynamic hero (an `<img>`, not the sibling Service Details page's inline-style background-div — kept this feature at zero inline styles per the stricter rule for this work), quick specs, About copy, Related Services (reusing the existing `_PricingCard` partial from the Pricing rebuild), native `<details>` FAQ accordion (matching `Services/Details.cshtml`'s own pattern), Reviews (reusing the `review-card` markup from `Home/Reviews.cshtml`), and Nearby Areas via `<vc:area-card>`. Full JSON-LD: `Service`/`LocalBusiness`, `FAQPage`, `BreadcrumbList`.
- **SEO wiring**: added generic `Canonical`/OpenGraph support to `_Layout.cshtml` (there was no existing mechanism for this anywhere in the app — extended the same `ViewData["MetaDescription"]`-style optional-tag pattern rather than inventing a new one). `SeoController.Sitemap()` now includes the Areas index and all 15 area pages, fetched live so it can't go stale. The old static `Home/ServiceAreas` page is retired: its route now issues a permanent redirect to `/Areas` (preserving link equity) and the now-unused view was deleted. Fixed two stale links that still pointed at the old route (navbar, Pricing page's "View all service areas" link). Added `ItemList` JSON-LD to the Areas index.
- **Verified**: `dotnet build` (0 errors) and the full test suite (41 service-layer + 8 web-integration tests, all passing) after every phase; a live app run + Playwright-driven browser check of both pages, the fixed nearest-areas behaviour, the sitemap, and the old-route redirect; zero Tailwind classes and zero inline `style=` attributes confirmed via rendered-HTML inspection.
- **Deliberately not done**: physical hero image assets. Per the agreed convention, HandyFix only writes the path strings/markup (`wwwroot/images/areas/{slug}-hero.webp`, `overview-coverage-map.webp`); asset generation/upload is external. Until those files exist, hero `<img>` tags fall back gracefully via `onerror` to the existing `/images/hero.png`.

---

## 3i. Public-Site UI Consistency Pass (2026-07-24)

Manual QA surfaced a long list of cross-page inconsistencies and layout bugs on the public site (heading alignment, missing breadcrumbs, no nav active-states, several real margin/z-index bugs, and a mobile nav defect). Fixed end-to-end in nine phases, each independently verified.

- **Two standardized page shells** now exist for every public page: **Template A** (content/hero pages — `page-container` + `breadcrumb-nav` + two-tone `services-hero-title`), already used by Services Index/Areas Index/Category/Pricing (left-aligned) and now extended to About (left-aligned), Contact, Terms, Privacy, Reviews, and FAQ; and **Template B** (thin/form pages — centered heading, no breadcrumb), kept as-is for Booking Index, Booking Confirmed, and Payment Cancel per explicit user decision that single-column form pages don't need the wider template.
- **Heading alignment, revised same day per user feedback**: Contact, Terms, Privacy, and FAQ keep Template A's breadcrumb/shell/margin-bug fixes but the hero heading reverts to centered (a `.services-header-content.text-center` modifier adds `margin-inline: auto` so the 800px-capped hero block centers within the wider `page-container`). Only About, Services, Areas, and Pricing remain left-aligned.
- **Nav active-state**: `_Navbar.cshtml` previously hardcoded the Home link as bold/colored regardless of the current page and never applied `.active` to any other link, even though `navbar.css` already defined that state. Added a path-prefix `IsActive()` helper (exact-match for Home, `StartsWith` for everything else) applied to both the desktop and mobile link lists.
- **Root-caused and fixed the `.info-canvas` margin bugs**: the class was designed to sit inside an `.info-hero` band and pull itself up with `margin-top: -32px` to overlap the hero's bottom edge. Contact, Terms, Reviews, and FAQ all used it as their *sole* top-level wrapper with no `.info-hero` above it, so the negative margin yanked the whole page up under the sticky nav (worst on Contact, which also skipped `.page-container` entirely). About had the same class misapplied to two sections that were never meant to receive it, stacked with a redundant `mb-16` utility on top of the class's own margin — the cause of its "excessive" hero/story spacing. Fixed by giving every one of these pages a proper Template A hero block and neutralizing `.info-canvas` back to a plain positive-margin content wrapper (no more negative-margin hero-overlap trick anywhere). Privacy's one-off full-bleed `.info-hero` background-image banner was retired in favor of the same plain hero as its siblings; the now-fully-dead `.info-hero`/`.about-hero` CSS was deleted rather than left as cruft.
- **Services/Category's excessive top margin**: `.services-page` and `.category-hero-section` were both adding top padding independently (2rem + 3rem stacked) — removed the duplicate from `.category-hero-section`.
- **Local-area badges are now real and clickable**: Pricing and Category both hardcoded the same fake town list (Sutton/Croydon/Epsom/Cheam/…) that didn't match any of the 15 real seeded `ServiceArea` records and linked nowhere. `ServicesController` now injects `IServiceAreasService` and populates a new `LocalAreas` property (`PricingViewModel`, `CategoryViewModel`) with 12 real areas; both views render them as `<a>` links to `/Areas/{slug}` in a `repeat(auto-fit, minmax(140px, 1fr))` grid — reads as a clean ~4-column layout on both pages' different container widths without a hardcoded column count. Covered by `PricingPageShouldContainRealClickableAreaLinks` in `WebTests.cs`.
- **Mobile nav bug, root-caused via live Playwright testing at 10 breakpoints (320–800px)**: the mobile dropdown is `position: absolute` anchored to `<header>`, with no awareness of the cookie-consent banner rendered between the header and page content. On every first-time mobile visit (before cookies are accepted), opening the hamburger menu made the dropdown visually collide with the banner — the "Accept" button and banner text partially peeking out from underneath the translucent menu at every width under 768px. Fixed with a single CSS rule (`.header-docked:has(.mobile-nav-dropdown.is-open) + #cookieConsent { visibility: hidden; }`) rather than a JS-driven body class, since the dropdown and banner are direct siblings in the layout.
- **Verified**: `dotnet build` (0 errors) after every phase; full test suite (41 service-layer + 9 web-integration tests, including the new area-links test) passing; live app run with Playwright-driven screenshots of every touched page at desktop width and the mobile nav specifically at 10 breakpoints, confirming the cookie-banner collision fix, correct breadcrumbs/active-states/two-tone headings on every rebuilt page, and no regression on the untouched Template B pages.
- **Footer fixed same day**: `_Footer.cshtml`'s "Service Areas" column had the same stale hardcoded town names as Pricing/Category before their fix. Added `FooterServiceAreasViewComponent` (same self-fetching pattern as `AreaCardViewComponent`) so the global footer partial doesn't need every controller to populate area data; renders 8 real areas as `<a>` links to `/Areas/{slug}`.

---

## 3j. Hero Image WebP Migration (2026-07-25)

Closes Pre-Sprint 4 TODO item 2 (see Section 4) — the first of those items to ship, and the single biggest performance win on the site.

- **`hero.png` (6.6 MB PNG, 2752×1536) is retired and replaced by `hero.webp` (90 KB, 1920×1072) — a 98.6% reduction** on an image that is the homepage LCP candidate, the About page image, the Trust section image, and the `onerror` fallback for every service and area image.
- **Method, deliberately reusing the existing pipeline rather than building a new one**: the admin service-image upload cap was raised from 5 MB to 20 MB (`ServiceAdminInputModel.ImageFile`'s `[MaxFileSize]`, plus the matching help text on the Create/Edit views) because the 6.6 MB original could not otherwise pass validation. The PNG was then uploaded through the existing admin Services form so `ImageStorageService`'s SkiaSharp pipeline did the conversion — resize to 1920 wide since the source exceeded that threshold, then WebP at quality 80 — and the output was moved to `wwwroot/images/` root. `ConvertExistingJpgServiceImages` was left untouched at the time; it was removed shortly afterwards — see Section 3k.
- **8 references updated across 6 files**: `Home/Index.cshtml` (hero, Trust image, and the popular-services `onerror` fallback), `Home/About.cshtml`, `Services/Category.cshtml` (`onerror`), `Areas/Details.cshtml` (`onerror`), and the Mapster fallback expression in both `ServiceViewModel` and `ServiceDetailsViewModel`.
- **Declared dimensions corrected**: the three `<img>` tags still carried the source PNG's `width="2752" height="1536"`, but the pipeline's resize changed the intrinsic size to 1920×1072. Now accurate, matching the Sprint 2 convention of reading dimensions straight from the file header. Aspect ratio was effectively preserved by the resize (1.7917 → 1.7910), so the practical CLS impact of the stale values was negligible — but they were wrong, and explicit dimensions exist precisely to be right.
- **Known leftover, not cleaned up here**: `images/services/test-image-from-admin-edit-hero.webp` is now a 90 KB copy of the hero image, since that throwaway service record was the vehicle for the upload. It is gitignored (`**/wwwroot/images/**/test*`) so it is not committed, but it and its orphan `Service` row in the database should be removed. No real service image was overwritten — verified by timestamp and by a clean `git status` on `images/services/`.
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors) and the full suite — 41 service-layer + 9 web-integration tests, all passing.

---

## 3k. Removal of `ConvertExistingJpgServiceImages` (2026-07-25)

The boot-time legacy-image sweep has been **deleted**, reversing the "keep it as-is" line originally logged in Pre-Sprint 4 TODO item 1. Its migration job was complete and it carried a real data-loss bug, so removing it was both simpler and safer than fixing it.

- **Its job was done.** The method existed to convert leftover JPG service images to WebP, from before `SaveServiceImageAsync` wrote WebP directly. All 23 files in `wwwroot/images/services/` are already `.webp` — zero JPGs — so it was a no-op running on every application boot. It had three references (interface member, implementation, the `Program.cs` call) and no test coverage.
- **The bug that made "fix vs. delete" worth deciding:** `File.Delete(jpgPath)` sat outside *both* the `if (!File.Exists(webpPath))` guard and the `if (originalBitmap != null)` guard, so it ran unconditionally. Three consequences: (a) `SKBitmap.Decode` returns `null` rather than throwing on a corrupt or misnamed file, so the conversion body was skipped, the source was deleted anyway, and — since nothing threw — the `catch`/`LogWarning` never fired either, making the loss completely silent; (b) when a `.webp` already existed the `.jpg` was deleted without conversion, which is defensible on its own but indistinguishable from case (a) after the fact; (c) worst, `new FileStream(webpPath, FileMode.Create, ...)` truncated the target *before* `SaveTo` was called, so an encode failure left a **zero-byte `.webp`** behind — which on the *next* boot satisfied the "already exists" guard and caused the source JPG to be deleted, turning a recoverable error into permanent loss plus a 0-byte image served to users.
- **Why delete rather than repair:** a correct fix needs an atomic write (encode to a temp path, then move into place) plus a verified-output check before deleting the source. That is real work to harden a method with no remaining purpose. Deleting removes the entire class of risk instead of patching it.
- **The failure mode is now strictly friendlier.** Drop a `.jpg` or `.png` into `wwwroot/images/services/` today and nothing happens to it: it sits there inert, the view falls through to its `onerror` fallback, and the image visibly doesn't appear. Visible and harmless, rather than silent and destructive.
- **`DeleteLegacyImages` was deliberately kept.** It is the targeted counterpart — it only removes `{slug}-hero.{jpg,jpeg,png}` at the moment a new WebP is successfully written for that same slug, on admin save/rename/delete. That is bounded and safe, unlike a blind directory sweep.
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors, warning count unchanged at 240 — no orphaned usings) and the full suite, 41 service-layer + 9 web-integration tests, all passing.

---

## 3l. Areas Coverage Map: Inline Interactive SVG (2026-07-25)

Closes Pre-Sprint 4 TODO item 4. The Areas index referenced `overview-coverage-map.webp`, which never existed — the `onerror` handler simply hid the whole section, so the page shipped without its visual anchor. Replaced with a hand-built SVG rather than a generated raster, because image models garble text and geography and misspelled Surrey town names would have undercut the very local-SEO story the page exists to tell.

- **New partial `Views/Shared/_AreaCoverageMap.cshtml`** takes the page's existing `IEnumerable<ServiceAreaViewModel>` — no controller change, no new data fetch, no new view model.
- **Both axes carry real data.** Each town sits at its true compass bearing from the Chessington base, at a radius proportional to its actual seeded `DriveTimeMinutes`. Nothing about the placement is invented, and drive-time rings at 10/20/25 minutes make the scale explicit. It is presented as a coverage *diagram*, not a street map, and the caption says so.
- **Every town is a real link** to `/Areas/{slug}` with an `aria-label` carrying the full name and drive time — so unlike the raster it replaced, the town names are selectable text, crawlable, and navigable. The hub links to Chessington.
- **Label collision was the actual engineering problem.** 14 labels around a circle collide badly at shared anchors — Kingston/Surbiton are 5° apart, Dorking/Leatherhead 7°. Solved by hand-anchoring each label (`start`/`middle`/`end` plus offsets) rather than distorting the bearings, so geographic accuracy is preserved. The 20- and 25-minute ring labels are only 55px apart at a shared bearing, so those three are staggered across bearings 270°/262°/278°. **Verified by rendering to PNG with headless Edge and inspecting, not by eye** — the first two layout attempts both had collisions that were only visible once drawn.
- **Mobile gets a different layout, not a shrunken one.** Below 768px the SVG is hidden and the same data renders as pills grouped into drive-time bands — a 780px-wide diagram is unreadable on a phone. The SVG is also capped at `max-width: 880px`, since an uncapped upscale rendered the 15px labels at ~24px on a 1440px screen.
- **Unknown slugs degrade gracefully**: the geometry table is keyed by slug, and any area without an entry is simply omitted from the diagram while still appearing in the mobile list and the cards below. Adding a `ServiceArea` can never break this view.
- **Verified**: `dotnet build` (0 errors), full suite (41 + 9) passing, and a live run confirming HTTP 200, all 15 area slugs present as links, the old image reference gone, and correct rendering at 700px and 1440px. Note the standard `--window-size=390` headless capture is misleading — Chromium on Windows clamps window width to ~500px, so narrow screenshots clip content on every page, touched or not.

---

## 3m. Admin CRUD for Service Areas (2026-07-25)

Areas were seed-only: `ServiceAreasSeeder` inserts by slug but **never updates**, `IServiceAreasService` was read-only, and there was no admin controller — so changing a town's copy meant editing C# and hand-patching the database. Now fully manageable from the admin panel, which also matters for the platform direction, where editing a seeder to change a town is not viable.

- **Service layer**: `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `GetByIdAsync<T>`, `SlugExistsAsync` added to `IServiceAreasService`. The service now also takes `IDeletableEntityRepository<ServiceAreaFaq>` so it can own FAQ persistence.
- **`DeleteAsync` is a hard delete, deliberately.** `IX_ServiceAreas_Slug` is unique with **no `IsDeleted` filter**, while `ApplicationDbContext` applies a global `HasQueryFilter(e => !e.IsDeleted)`. A soft-deleted area would therefore keep occupying its slug while being invisible to every query — and on next boot `ServiceAreasSeeder` would look it up, see nothing, insert, and hit a unique-index violation. Because seeding runs inside `Program.Configure`, **that fails app startup**, recoverable only by hand-deleting the row in SQL. For the same reason `SlugExistsAsync` validates against `AllWithDeleted()`, not `All()`.
- **FAQs are deleted before the parent**: the FK is `ReferentialAction.Restrict`, so hard-deleting an area with FAQs would otherwise throw a constraint violation. On save, FAQs are replaced wholesale rather than diffed — they carry no external references, and hard-replacing stops soft-deleted orphans accumulating on every edit.
- **The slug is an explicit, editable field, not regenerated from the name** (which is what `ServicesService.CreateAsync`/`UpdateAsync` do). The area slug is load-bearing in three independent places — the public URL, the hero image filename, and the geometry key in `_AreaCoverageMap.cshtml` — so rewriting it because someone fixed a typo in the name would break all three at once. The create form suggests a slug via JS and stops the moment the field is touched; edit never touches it, and warns when it changes. Worth noting the existing `ServicesService.Slugify` could not reproduce the seeded slugs anyway: it yields `walton-on-thames--weybridge` (double hyphen) for `Walton-on-Thames & Weybridge`.
- **Controller** at `Areas/Administration/Controllers/ServiceAreasController.cs`, inheriting `AdministrationController` so it picks up `[Area("Administration")]` + `[Authorize(Roles = AdministratorRoleName)]`. Note the route is `/Administration/ServiceAreas` — this project has no `Admin` area.
- **FAQ validation avoids an index-shift trap.** `ServiceAreaFaqInputModel` deliberately carries **no DataAnnotations**: blank rows are pruned *before* validation, and binder-generated errors would still be keyed to pre-prune indices, rendering against the wrong row. The controller prunes first, then validates the survivors against the entity's own limits, so every error key matches the index the row actually renders at.
- **Views**: `Index` (table + stats + search), `Create`/`Edit` sharing a `_ServiceAreaForm` partial, with a dynamic FAQ builder in `wwwroot/js/admin-service-area-form.js`. The JS reindexes all rows after every add/remove, because the model binder stops at the first gap in `Faqs[0..n]` — removing row 1 of 3 would otherwise silently drop row 2 as well. Script lives in a file rather than the partial because `@section` does not work inside partials. Edit also reports whether the hero image actually exists on disk, mirroring how `Services/Edit.cshtml` uses `ServiceImageExists`.
- **Docs**: `docs/WORKFLOW_SERVICE_AREAS.md` documents the real three-step pipeline, plus a summary on `ServiceAreasSeeder` itself.
- **Verified**: `dotnet build` (0 errors); suite up from 41 to **47 service-layer tests** + 9 web-integration, all passing. Live authenticated round-trip confirmed create → public page live with FAQ, duplicate-slug rejected, malformed-slug rejected, half-filled FAQ rejected, blank FAQ row pruned silently, edit replacing (not appending) FAQs, delete → public page 404 → **slug immediately reusable**.
- **Pre-existing quirk noticed, not fixed**: `CheckConsentNeeded = true` means the `CookieTempDataProvider` cookie is withheld until cookie consent is accepted, so `TempData` success banners do not render for a user who has not accepted the banner. Affects the existing `ReviewsController` messages equally; not introduced here.
- **Not verified visually**: the admin pages are auth-gated and this session had no browser automation, so structure and behaviour were verified over authenticated HTTP rather than by screenshot. Layout risk is low — the views reuse the existing admin classes throughout, with `.faq-row` the only new visual element.

---

## 3n. Reviews Cleanup: Remove Fabricated Trust Content (2026-07-31)

Closes Pre-Sprint 4 Tier 1 item 3 (see Section 4) — started ahead of the client call, since none of this required business input. Full reasoning in `docs/private/VISION_AND_CONTEXT.md` Sections 4 and 5.12; this section records what shipped.

- **`ReviewSeeder` deleted outright** (not just unregistered) and its registration removed from `ApplicationDbContextSeeder.cs` — it seeded 5 generic e-commerce-filler reviews (fake `IsApproved = true` rows averaging 4.2 against the "4.9/5" claimed elsewhere) that had no purpose left once local submission is gone.
- **Local review submission removed entirely**: `ReviewsController` (public, held only the `Submit` action), `IReviewsService.AddReviewAsync`/`ReviewsService.AddReviewAsync`, and `ReviewInputModel` are all deleted; `ReviewsListViewModel.NewReview` is gone. `Home/Reviews.cshtml`'s form, star-rating JS, and validation partial are removed along with it — there is no longer a POST path that creates a `Review` row from the public site. `Admin`'s own approve/delete workflow (`Areas/Administration/Controllers/ReviewsController.cs`, `ReviewsService.ApproveReviewAsync`/`DeleteReviewAsync`) is untouched, since that's the intended path for whatever a future Business Profile API import lands (roadmap Tier 3, not started).
- **"Verified Client" removed from all three display surfaces** (`Home/Index.cshtml`, `Home/Reviews.cshtml`, `Areas/Details.cshtml`) along with the sidebar claim *"Every review is manually verified by our support team"* — submission required no booking, no account, and no email, so the site was asserting a verification step it never performed.
- **Reviews page sidebar rebuilt as a Google CTA**, replacing the submission form: a "Loved our work?" card linking out to a new `Business:GoogleReviewsUrl` config key (`appsettings.json`, empty by default — deliberately not guessed, since no Business Profile exists yet — roadmap Tier 3 item 13). `HomeController.Reviews()` now takes `IConfiguration` and passes the value through; the view shows the link when set, or a plain "coming soon" line when it's blank, so this becomes a one-line config edit once the profile exists rather than a code change (matches the "5-minute edit at the end" principle from `VISION_AND_CONTEXT.md` Section 7). The homepage's "Leave a Review" button is renamed **"Read All Reviews"** — it still routes to `/Reviews`, but the text no longer promises an on-site submission that doesn't exist.
- **Dead CSS removed alongside the markup**: `.reviews-trust-icon-box`, `.rating-selector-buttons`, `.star-btn`(`.star-active`), `.verified-badge`, and `.testimonial-role-label` — confirmed each had no other caller before deleting. `.info-bottom-action` was checked and kept — `Home/Terms.cshtml` still uses it.
- **Explicitly not touched**, per the existing scope split in Section 4: the fabricated `4.9`/`2.4k Verified Reviews` stat pills on `Home/Reviews.cshtml` and the `12k+ Jobs Completed`/`4.9/5 Rating`/`Based on 2,500 reviews` trust-stats card on `Home/Index.cshtml` — those are Tier 3 item 16, blocked on real business facts from the client call, not this item.
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors); full suite now 46 service-layer (one test removed, `AddReviewAsyncShouldAddUnapprovedReview`, since the method it covered no longer exists) + 9 web-integration, all passing. Live local run + Playwright-driven screenshots of Home, Reviews, and an Areas details page confirmed no "Verified Client" text anywhere, the submission form is gone, the Google CTA renders its "coming soon" fallback correctly with an empty config value, and review cards still render normally.
- **Resolved 2026-07-31**: the local dev database had the 5 old seeded rows (orphaned once the seeder was deleted) plus a manually-added test row (`"test" / "TestTest"`) from earlier QA of the submission form. The user deleted all of them via `Areas/Administration/Reviews` once the bug in Section 3o was fixed — confirmed gone, no reviews remain in the local dev database.

**Follow-up the same day, on-site display hidden entirely pending the Google API import**: showing a "be the first" empty state (or, worse, a stray Admin-approved row) still implied on-site reviews are a live feature, which they no longer are per the agreed strategy. Rather than rely on "zero rows happens to look empty," on-site review display is now gated behind an explicit `Business:ShowOnSiteReviews` config key (`appsettings.json`, default `false`) so it can't accidentally reappear before the Google Business Profile API import is actually built (roadmap Tier 3, not started) — flipping one value turns it back on everywhere at once, same "5-minute edit later" pattern as `GoogleReviewsUrl`.
  - `HomeIndexViewModel`, `ReviewsListViewModel`, and `ServiceAreaDetailsPageViewModel` each gained a `ShowOnSiteReviews` bool; `HomeController` (`Index`, `Reviews`) and `AreasController` (`Details`, which now also takes `IConfiguration`) read the config value once and skip the `GetLatestApprovedAsync` call entirely when it's off, rather than fetching data that won't render.
  - **Home/Index**: the whole "What Our Customers Say" testimonials band (including the "Read All Reviews" button) is hidden when off.
  - **Reviews page**: the fabricated `4.9`/`2.4k Verified Reviews` stat pills and the review-feed grid are hidden when off — bundling their removal in here rather than waiting on Tier 3 item 16, since hiding fabricated numbers needs no real data, unlike replacing them. The "Loved our work?" Google CTA becomes the page's sole content, re-centered as a standalone card (was built as a sidebar next to the feed) rather than left stranded next to empty space.
  - **Areas/Details**: already hid itself once `Reviews.Any()` was false; added the same toggle check for defense-in-depth so a stray Admin-approved row can't leak onto an area page before the toggle is deliberately flipped.
  - **The Reviews page and its nav/footer link stay reachable on purpose** — confirmed with the user — since the centered CTA doubles as a "reviews are coming soon" signal rather than a dead link.
  - **Verified**: `dotnet build` (0 errors) and the full suite (46 + 9, unchanged) after this change; live local run + Playwright screenshots confirmed all three surfaces render nothing review-related with the default (off) config, and the Reviews page shows a single, non-redundant "coming soon" message.

---

## 3o. Bug Fix: Admin "Delete Review" Silently Did Nothing (2026-07-31)

Found while the user tried to delete the leftover fake/test review rows through the admin panel per Section 3n's follow-up — the Delete button appeared to have no effect no matter how many times it was clicked.

- **Root cause**: `EfDeletableEntityRepository<T>.Delete()` performs a soft delete (`IsDeleted = true`, `DeletedOn` set) — the standard pattern everywhere in this app. But `ReviewsService.GetAllAsync` (which powers the Admin Reviews list) queried `AllWithDeleted()` (`IgnoreQueryFilters()`), so a "deleted" row kept reappearing in the list immediately on the next page load, with no visual indicator that anything had changed — `ReviewViewModel` doesn't even expose `IsDeleted`. `ApproveReviewAsync` had the same `AllWithDeleted()` call.
- **Traced through git history**: `AllWithDeleted()` was there from the very first commit that created `ReviewsService` (`6278115`), before any sorting/filtering existed. The Sprint 3 commit that added sorting/status-filtering (`37b43af`) extended that same query with `OrderBy`/`Where` logic without reconsidering its source — it inherited the bug rather than introducing it. No test or manual QA pass ever specifically exercised the Delete button; the Sprint 3 verification note for that feature covered only sorting, filtering, and the summary cards.
- **Fix**: both `GetAllAsync` and `ApproveReviewAsync` now query `All()` instead of `AllWithDeleted()`, so a soft-deleted review actually disappears from the admin list and can't be re-approved after deletion — consistent with how every other soft-deleted entity in the app behaves, and with the fact that this feature has no restore/undelete UI to justify reaching past the filter.
- **Regression test added**: `DeleteReviewAsyncShouldRemoveReviewFromGetAllAsyncResults` (`ReviewsServiceTests.cs`) — deletes a review, then asserts `GetAllAsync` no longer returns it. This is the test that would have caught the original bug.
- **Verified**: `dotnet test` on `HandyFix.Services.Data.Tests` — 47 passing (was 46, plus the new regression test). The Web test project wasn't rebuilt this pass since it has no code changes here and Visual Studio had the Web project's build output locked via a running IIS Express session at the time.
- **Confirmed fixed by the user 2026-07-31**, live in the admin panel: Delete now actually removes a review from the list, no longer requiring a workaround.

---

## 3p. Broken Links & Metadata Cleanup (2026-07-31)

Closes Pre-Sprint 4 Tier 1 item 5 (see Section 4).

- **`Services/Index.cshtml`'s `quality-proof-image`** pointed at `/images/handyfix-proof.jpg`, which has never existed on disk — it always fell through to an external `gstatic.com` placeholder SVG (a third-party dependency for what's meant to be a trust-building image). Now points directly at `/images/hero.webp`, the sitewide fallback image already used the same way by `Home/Index.cshtml` and `Services/Category.cshtml`; the dead `onerror` chain was removed rather than kept pointed at a file that doesn't exist.
- **`Home/Index.cshtml`'s `LocalBusiness` JSON-LD** carried an `"image": "https://handyfix.co.uk/images/logo.png"` entry — `logo.png` doesn't exist and won't until the rebrand name lands and the wordmark is built (Tier 3 item 15). Removed rather than guessed at; add it back when the real wordmark exists.
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors).

---

## 3q. Email Provider Swap: SendGrid → Brevo (2026-07-31)

Closes Pre-Sprint 4 Tier 1 item 7 (see Section 4). Reasoning in `docs/private/VISION_AND_CONTEXT.md` Section 5.11 — Twilio retired SendGrid's permanent free tier, leaving a $19.95/month minimum for capacity HandyFix will never use; Brevo's free tier (300 emails/day, permanent, no card) comfortably covers the real volume (roughly two emails per booking).

- **New `BrevoEmailSender`** (`HandyFix.Services.Messaging`) implements `IEmailSender` with a direct `HttpClient` POST to Brevo's transactional email API (`https://api.brevo.com/v3/smtp/email`, `api-key` header) — no new NuGet dependency, since the request/response bodies are plain JSON handled with `System.Text.Json` (already in the BCL). `SendGridEmailSender` is deleted outright, along with the `Sendgrid` package reference (`HandyFix.Services.Messaging.csproj`, `Directory.Packages.props`) — this is a swap, not a dual-provider abstraction.
- **One correctness fix picked up along the way**: `SendGridEmailSender` only `Console.WriteLine`'d the response status/body and never checked it, so a failed send (bad recipient, suspended account, quota exceeded) was silently swallowed. `BrevoEmailSender` checks `response.IsSuccessStatusCode` and throws with the response body on failure, consistent with the "fail loud" pattern already used for Stripe and the missing-key checks (Section 5 below).
- **DI registration** (`Program.cs`) now reads `Brevo:ApiKey` instead of `SendGrid:ApiKey`, same fail-loud-outside-Development fallback to `NullMessageSender`. `BookingsService`/`PaymentsService` are untouched — both already depend only on `IEmailSender`, never the concrete sender.
- **`README.md`** updated: the documented `dotnet user-secrets set` command is now `Brevo:ApiKey` (Brevo keys are prefixed `xkeysib-`), and the fail-loud paragraph now names Brevo.
- **Not done here, operational not engineering**: creating the actual Brevo account and verifying a sender domain — needed before `Brevo:ApiKey` can be set for real in any environment. The `bookings@handyfix.co.uk`/`no-reply@handyfix.co.uk`/`admin@handyfix.co.uk` sender addresses are unchanged and will need revisiting alongside the rebrand (Tier 3 item 10), same as they would have under SendGrid.
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors, no orphaned `Sendgrid` reference). Not live-tested against a real Brevo account — no API key exists yet (see above) — so this is verified by build/compile correctness, not an actual send.

---

## 3r. Technicians Decoupled from Capacity Slots; Admin Technician Roster (2026-07-31)

Closes Pre-Sprint 4 Tier 1 item 8 (see Section 4) — but with a **larger and different scope than that item described**, decided with the user this session. The item as written asked for a technician picker on the Calendar's slot-generation form. That was rejected in favour of removing technicians from slots altogether: technicians are fluid (largely self-employed contractors) and must not dictate booking capacity. A slot is business capacity; the technician enters the picture only once there is a real `Booking`.

### The model change: `AvailabilitySlot.TechnicianId` is gone

- **Column, FK, index and nav property all dropped** (migration `20260731200235_RemoveTechnicianFromAvailabilitySlot`, with a working `Down`). `TechnicianId` now lives on `Booking` and nowhere else.
- **This removes a whole class of bug rather than patching it.** Two nullable columns held one fact, and they were already drifting: `BookingsService.AssignTechnicianAsync` wrote only `Booking.TechnicianId`, leaving the slot's copy stale. The originally-planned fix was to sync them; deleting the duplicate makes the sync structurally unnecessary. Same reasoning as Section 3k — delete rather than repair, once the purpose is gone.
- **It also defused a live footgun.** `RescheduleBookingAsync` did `booking.TechnicianId = newSlot.TechnicianId`. Had the column been kept but left unpopulated, every reschedule would have **silently wiped the admin's technician assignment**. That line is gone and the assignment is now explicitly preserved across a reschedule, covered by `RescheduleBookingAsyncShouldReleaseOldSlotAndClaimNewOne`.
- `CreateBookingAsync` no longer copies a technician off the slot — bookings are created unassigned by design, since assignment happens after the customer books and pays.
- **The Calendar's `Tech: …` line was deleted, not rebuilt.** It had never rendered a real value: `CalendarController.Index` has no `.Include(x => x.Technician)` and the project has no lazy-loading proxies, so every slot row had always read "Unassigned" regardless of the data.
- **Counter-argument, acknowledged and rejected:** per-technician capacity ("A works Mondays, B works Tuesdays") would need exactly this column back. It is explicitly out of scope, and re-adding a nullable column is a one-line migration if the model ever changes.

### Auto-generation of slots is dead

- `GetAvailableDatesAsync`, `GetAvailableSlotsForDateAsync` and `GetAllSlotsForDateAsync` no longer call `GenerateSlotsForRangeAsync`. **Browsing the public booking page used to create 30 days of capacity as a side effect** — which also meant the admin picker the original Tier 1 item asked for would have been near-useless, since slots for the next 30 days always already existed by the time an admin looked.
- `CalendarController.Index` no longer generates either: viewing a day must not create capacity for it. The view's existing "No slots generated for this date. Use the range generator tool." empty state now does real work.
- `GenerateSlotsForRangeAsync` keeps its `(startDate, endDate)` signature and **lost its technician lookup entirely** — including the early `return` when no active technician existed, which used to make generation a silent no-op on a database with no technicians.
- The customer-facing empty state was reworded from the admin-speak "No slots generated for this date" to "No availability on this date."
- Regression tests: `GetAvailableDatesAsyncShouldNotGenerateCapacityWhenNoneExists`, `GetAllSlotsForDateAsyncShouldNotGenerateCapacityWhenNoneExists`, `GenerateSlotsForRangeAsyncShouldCreateBusinessHoursWithNoTechniciansInTheDatabase`.

### `DevelopmentCapacitySeeder` — capacity out of the box, outside production

- New `Web/HandyFix.Web/Services/DevelopmentCapacitySeeder.cs`, called from `Program.Configure` inside the existing seeding scope. Seeds **14 days** of standard capacity so a fresh clone or a newly stood-up staging box has a working booking flow without an admin opening the Calendar first (this matters for the Tier 4 staging stand-up).
- **Gated on `IWebHostEnvironment.IsProduction()`** — in production, capacity stays strictly a business decision.
- **Only fires when there is no future capacity at all** (`StartTime >= today`). That keeps it quiet once an admin has generated (or deliberately blocked — blocked slots are still rows) capacity of their own, while still topping up a long-lived staging database whose original window has fallen into the past.
- **Deliberately in the Web layer, not with the `ISeeder` implementations.** It delegates to `IAvailabilityService` rather than restating the "9–17, no Sundays" rule; `HandyFix.Data` has no reference to the services layer, so a seeder there would have meant a second copy of the same business rule.

### Admin Technician CRUD

Technicians were seed-only, and `TechniciansSeeder` inserts only when the table is **empty** — so adding a second technician later would not have worked by editing the seeder at all. That made this a hard prerequisite for Tier 2 item 11, not a nice-to-have.

- `ITechniciansService`/`TechniciansService` + `TechniciansController` + Index/Create/Edit views and a `_TechnicianForm` partial, following the Section 3m Service Areas pattern throughout. Sidebar entry added.
- **Soft delete here, the opposite of Section 3m's hard delete, for the opposite reason.** A technician squats on no unique index, but `Booking.TechnicianId` is a live FK pointing at real job history. `DeleteAsync` therefore **refuses when any booking references the technician** and returns `false`, and the admin is told to deactivate instead; the Index hides the delete button entirely for anyone with bookings. Deleting is only for a row created in error.
- `IsActive` is the intended retire path. `GetAssignableAsync` returns active technicians **plus the one currently assigned to this booking even if since deactivated** (rendered "(inactive)") — without that, opening such a booking would render a picker that omits the assignee and quietly unassign them on the next save.
- `PhoneNumber` is `[Required]` in the input model although the column is nullable: the number is what the customer receives on confirmation, so a roster entry without one is not useful. Tightening the form needed no migration.

### Booking assignment is now the only assignment path — three fixes

- **`Guid` → `Guid?` end to end** (`IBookingsService.AssignTechnicianAsync`, `BookingsController.AssignTechnician`). The picker's blank option posted an empty value that bound to `Guid.Empty` and then **failed the `TechnicianId` foreign key at `SaveChanges`** — a raw 500. It now clears the assignment, which is what the option claims to do. `AssignTechnicianAsync` also verifies a non-null id resolves to a real technician before writing.
- **Selected-option detection was `Model.TechnicianName.Contains(tech.FirstName)`** — a string match that breaks on two technicians sharing a first name. Now bound on a new `BookingDetailsViewModel.TechnicianId`.
- `BookingDetailsViewModel.Technicians` was `IEnumerable<Technician>` (a raw entity in a view model); it is now `IEnumerable<TechnicianOptionViewModel>`, and `BookingsController` takes `ITechniciansService` instead of a bare `IDeletableEntityRepository<Technician>`.

### Email: technician detail moved to the right message

- **Removed from the deposit confirmation** (`PaymentsService`). Assignment now happens after payment, so that email would have always read "Technician: Not yet assigned" — unfinished-looking to the customer. It now says "We'll confirm your assigned technician shortly", and the wasted `.ThenInclude(b => b.Technician)` join went with it.
- **Added to the admin-approval "CONFIRMED" email** (`BookingsService.UpdateStatusAsync`), which fires when an admin approves and therefore has a real assignment to report: name plus a `tel:` link, falling back to the previous generic sentence when unassigned. This is the increment of Tier 3 item 17's technician card that does not need real business data.
- `PaymentsServiceTests.ProcessPaymentSuccessAsyncShouldSendClientConfirmationAndAdminNotificationEmails` now asserts the technician name is **absent** from the client email, on a booking that has one assigned specifically to prove it stays out.

### Docs

- **`docs/WORKFLOW_BOOKINGS.md`** documents the whole pipeline end to end — generate capacity → customer books and pays → admin assigns a technician → admin approves — written for both admin users and developers, with a troubleshooting table. Named to sit alongside the existing `docs/WORKFLOW_SERVICE_AREAS.md` rather than the generic `WORKFLOW.md`, since it covers one workflow among several.

### Verified

- `dotnet build src/HandyFix.sln` — 0 errors. Full suite green: **57 service-layer** (up from 47: +4 availability, +6 new `TechniciansServiceTests`) **+ 9 web-integration**.
- **Live authenticated round-trip** against the running app: created a technician; validation correctly rejected a 1-character name and a blank phone; assignment picker rendered both technicians with the assigned one selected; assigned a different technician and confirmed the selection moved; **unassigned via the blank option and got a 302, not the old FK 500**; deleting a technician with 7 bookings was refused while one with 0 was deleted; deactivating the assigned technician kept them in that booking's picker labelled "(inactive)".
- **Auto-generation confirmed dead**: a far-future date shows no slots in the admin Calendar, `/Booking/GetSlots` returns `[]` for it, and hitting the public endpoint does not create anything. Public pages (`/`, `/Booking`, `/Pricing`, `/Areas`) all still 200.
- **The dev seeder was exercised against a throwaway database** (`HandyFix_SeedCheck`, created and dropped for the purpose, dev database untouched): it logged "Seeded 14 days of booking capacity", the public booking flow returned 8 slots/day with zero admin action, Sunday correctly returned 0, and day 15 correctly returned 0. On the real dev database it correctly stayed silent, since future capacity already existed there.
- **Not verified visually** — the admin pages are auth-gated and this session drove them over authenticated HTTP rather than a browser, same as Section 3m. Layout risk is low: the new views reuse existing admin classes throughout, with no new CSS.
- **Leftover to clean up**: live verification left a soft-deleted `Zapryan Petrov` test row in the local dev `Technicians` table. It is invisible to the app (global query filter) and harmless; removing it needs a manual `DELETE` against the dev database.

---

## 3s. README Accuracy Pass (2026-08-01)

Closes the README half of the Sprint 4 "Documentation" line (see Section 4). Documentation only — no code touched, so no build or test impact. Every claim below was verified against the code before being changed, not assumed.

- **The template's aspirational stack claims are gone.** `CQRS (MediatR)` and `AutoMapper` appeared in four places between the feature list, the tech stack, and the project-structure comments; neither package exists in `Directory.Packages.props` nor is referenced by a single `.cs` file. Replaced with what's actually there: Mapster via the `IMapFrom<T>`/`IMapTo<T>` convention, and plain async service classes. `FluentAssertions` was also listed and is not referenced — the suite is xUnit + Moq. `Feature Folders` was replaced with the real shape (layered projects + MVC Areas), and `Email and SMS notification services` with email only (`HandyFix.Services.Messaging` holds `BrevoEmailSender`/`IEmailSender`/`NullMessageSender` and nothing SMS-shaped).
- **The project-structure block had two wrong paths**: `Common/` is really `HandyFix.Common/`, and `Sandbox/` sits at `src/Tests/Sandbox/`, not `src/Sandbox/`. `HandyFix.Web.Tests/` was also described as "Tests for web controllers and endpoints" — it is `WebApplicationFactory` integration tests, and controller-level tests remain an open Sprint 4 gap, so that line was claiming coverage that does not exist. (`src/TextUi/` is deliberately absent from the block — it is untracked local scratch, not a project.)
- **Two feature checkboxes were unticked rather than reworded**: Google Business Profile integration (blocked on the rebrand — roadmap Tier 3 item 13) was bundled into a ticked "Local SEO optimization" line alongside schema markup, which *is* done; the two are now separate lines. A CI/CD line was added unticked to match roadmap Tier 4.
- **Getting Started corrected on two counts**: prerequisites said "SQL Server (LocalDB or full)" when development actually runs against the Docker container on `localhost,1433` (Section 1), and the documented `dotnet ef database update` step is unnecessary — `Program.cs` runs `Database.Migrate()` and seeds on startup. Added a note that the committed connection string is a placeholder overridden via User Secrets, and that `DevelopmentCapacitySeeder` (Section 3r) means a fresh clone has a working booking flow with no admin action.
- **The "Business Information" block was cut entirely**, per user decision. It was marketing copy in a developer README, and it was wrong: it claimed "South London & Kent" serving Croydon, Bromley, Orpington, Dartford and Sevenoaks — **not one of which is a seeded `ServiceArea`**. All 15 real areas are Surrey/SW London (Chessington through Guildford, Section 3h). Cutting it rather than correcting it also avoids a second edit when the rebrand lands, and keeps the NAP identity in exactly one place (roadmap Tier 3 item 18) instead of two. The intro line carried the same Kent error and was corrected to "based in Chessington, covering South West London and Surrey" — kept rather than cut, since a one-line description of what the repo *is* belongs in a README even when the marketing block does not.
- **The Documentation section's "coming soon" placeholders now link real docs** — `PROJECT_STATE.md`, `DESIGN.md`, and the two existing workflow docs. No new architecture prose was written: `PROJECT_STATE.md` Section 1 already is the architecture document, so the README points at it rather than duplicating (and eventually contradicting) it.

---

## 3t. Controller Test Coverage & CI-Ready Integration Tests (2026-08-01)

Closes the Sprint 4 "controller-level tests" line (see Section 4). **`HandyFix.Web.Tests` goes from 9 tests to 61**; the service-layer suite is untouched at 57, so the whole suite is now **118**. Approach is mocked-service controller unit tests (`Moq` added to `HandyFix.Web.Tests`, already in `Directory.Packages.props`), one file per area under `Tests/HandyFix.Web.Tests/Controllers/`. All 52 controller tests run in about a second with no database at all.

### The integration tests no longer touch the dev database

- **The problem, found while scoping this**: `WebTests` used a bare `WebApplicationFactory<Program>`, which boots the real `Program` and therefore ran `Database.Migrate()` + full seeding **against whatever `ConnectionStrings:DefaultConnection` pointed at** — in practice the developer's own `HandyFix` database, on every run. They also could not have run in CI at all, which would have surfaced as a red pipeline the moment Tier 4 item 19 lands.
- **New `SqliteWebApplicationFactory`** boots the same real application against a private Sqlite in-memory database. The connection is held open for the factory's lifetime, because a Sqlite in-memory database exists only while a connection to it does.
- **This needed one production change**, in `Program.Configure`: `Database.Migrate()` is now called only when `Database.IsSqlServer()`, with `EnsureCreated()` for any other provider. The migrations contain SQL Server-specific SQL (`rowversion` above all) and cannot be replayed on Sqlite. Production and development both still take the `Migrate()` branch — the new branch is reachable only from a test host.
- **Two non-obvious traps, both hit and fixed:**
  - Removing `DbContextOptions<ApplicationDbContext>` — the widely-cited recipe — **is no longer sufficient**. Since .NET 9 `AddDbContext` also registers an `IDbContextOptionsConfiguration<TContext>` that *accumulates* rather than replaces, so a second `AddDbContext` applied `UseSqlServer` *and* `UseSqlite` to the same options object and EF threw "Only a single database provider can be registered". The factory now strips every registration whose **service type** mentions `ApplicationDbContext`. Identity's stores are generic over `ApplicationDbContext` in their *implementation* type only, so they survive.
  - The factory must force `Environments.Development`, or `AdminUserSeeder` throws rather than falling back to its dev-only password — correct behaviour (Section 5), but fatal to a test host.
- **Verified decoupled, not assumed**: the 9 integration tests were re-run with `ConnectionStrings__DefaultConnection` overridden to a nonexistent host, and still passed. Sqlite still does not enforce the `RowVersion` token, so real double-booking rejection stays where it was — in the service-layer tests that build their own Sqlite context for exactly that reason.

### What the 52 controller tests actually cover

Chosen by where bugs have genuinely shipped, not by chasing line coverage:

- **`BookingControllerTests` (15)** — chiefly the slot-race recovery promised in Section 1 and never previously tested: on `SlotUnavailableException`/`DbUpdateConcurrencyException` the controller must return the *view* rather than redirect to Stripe, clear `SlotId`, add the friendly message, **and preserve every other field the customer typed**. Also: an invalid model never reaches `CreateBookingAsync` *or* the image upload; `InvalidOperationException` messages surface verbatim while unexpected exceptions do not leak internals (asserted against a fake message containing a private IP); `GetSlots` rejects unparseable dates and returns 500-as-JSON rather than throwing into an AJAX call.
- **`PaymentControllerTests` (11)** — the Stripe sandbox bypass treated as the security control it is. It fires only when the key is genuinely absent **and** the environment is Development; in Production/Staging/QA it throws **and records no payment** (a payment row there would mark an unpaid booking as paid). A blank-string key counts as missing, not configured. The webhook returns `BadRequest` for an unverifiable signature rather than 5xx, since Stripe retries on 5xx.
- **`AdminBookingsControllerTests` (10)** — regression for the Section 3r foreign-key 500: the blank picker option must reach the service as `null`, explicitly *not* `Guid.Empty`. Plus the Section 3d summary-card bug: with a status filter applied, all three cards must still be computed from an unfiltered fetch (each would read a different, wrong number otherwise). Written month-boundary-safe so the revenue assertion cannot break depending on the day it runs.
- **`ReviewVisibilityTests` (6)** — the Section 3n `Business:ShowOnSiteReviews` gate on both Home and Reviews, asserting the reviews are **not fetched at all** when off rather than fetched and hidden, so a stray Admin-approved row cannot leak.
- **`AdminServiceAreasControllerTests` (5)** — the Section 3m index-shift trap: with a blank row 0 pruned, an error on the surviving row must key to `Faqs[0]`, not `Faqs[1]`, or it renders against a row the admin cannot see.
- **`AdminDeletionTests` (5)** — the deliberately opposite delete semantics: technicians with bookings are refused *with an explanation* (not a silent no-op, the same failure mode as the Reviews delete bug in Section 3o), while a service deletes its image **before** its row, since the image path is derived from the slug and becomes unfindable afterwards. Enforced with a `MockSequence`, so reordering the two lines fails the test.

### Verified

- `dotnet build src/HandyFix.sln` — **0 errors**. Full suite green: **57 service-layer + 61 web-integration/controller = 118**.
- **Resolved vulnerability warning**: adding `Microsoft.EntityFrameworkCore.Sqlite` originally surfaced `NU1903` — `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 had a high-severity advisory ([GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)). This was resolved by pinning `SQLitePCLRaw.lib.e_sqlite3` to version `2.1.12` in `Directory.Packages.props` using Central Package Management (CPM) transitive pinning, resulting in a completely clean security audit.

---

## 3u. Unified Agent Workflow & Public-Repo Security Audit (2026-08-01)

The project is worked on with two AI agents — Claude Code and Gemini in the Antigravity IDE — and they were following **two different, partly contradictory rulebooks**. Now one.

### The problem, concretely

- `CLAUDE.md` (tracked) required commit bodies of `- ` bullets and *"No prose paragraphs."* An untracked `.agents/AGENTS.md` required *"Multi-line detailed explanation"* — prose. **Both agents were obeying their own file correctly**, which is why three of the four commits made on 2026-08-01 have prose bodies against a history of bullets. This was a rules bug, not carelessness.
- The two files were also complementary in ways neither knew about: `CLAUDE.md` alone had context initialization, `PROJECT_STATE.md` synchronisation, plan-before-code and the no-`Co-Authored-By` rule; `.agents/AGENTS.md` alone had the StyleCop/using-directive rules and the package-security workflow (CPM, `--vulnerable` audit, 14-day age window) — the habit that actually caught CVE-2025-6965.
- `.agents/AGENTS.md` was **untracked and in a non-standard location**, so it was invisible in a fresh clone and to anyone else on the project.

### The structure: one rulebook, two signposts

- **`AGENTS.md` at the repository root** now holds the complete workflow, tracked. This is the location Antigravity reads and the emerging cross-tool convention.
- **`CLAUDE.md` and `GEMINI.md` are pointers only**, carrying no project rules — just "read `AGENTS.md` and follow it", plus a few genuinely tool-specific harness notes (Claude's file-tool preference and Windows shell syntax).
- **`GEMINI.md` exists deliberately even though it is nearly empty.** In Antigravity a root `GEMINI.md` *overrides* `AGENTS.md`; leaving the slot vacant invites someone to fill it later with rules that silently outrank the shared workflow. Occupying it with a pointer closes that door, and the file says so.
- `.agents/AGENTS.md` deleted, its content merged in.
- **Why pointers rather than three copies:** duplicated rules are precisely what caused the contradiction. One real file plus signposts leaves nothing to drift.

### What the merged file adds beyond the two originals

- **Public-repository rules.** This repo is public and used as a portfolio piece, so the file states what may never be committed (credentials, real IPs, client PII, agent reasoning) and — the part that is easy to get wrong — that the check must happen **before `git add`**, because `git rm --cached` does not remove anything from history.
- **The testing conventions**, previously only discoverable by reading this file: which of the three layers a test belongs in, never to swap `SqliteWebApplicationFactory` for a bare `WebApplicationFactory<Program>`, and that InMemory-versus-Sqlite is load-bearing rather than stylistic.
- **The commit-format conflict resolved to bullets**, matching real history.
- **No `§` symbols in documentation**, and the requirement to explain *why* an approach is right rather than only listing steps.

### Security audit of the public repository

Prompted by the portfolio use. Full history swept for credential patterns, plus every historical version of `appsettings.json`.

- **No live credential has ever been committed.** `CloudflareR2:AccessKeyId`/`SecretAccessKey` appear only as empty strings in every version that has them; no Stripe live key, Brevo key or AWS key anywhere. Commit `b2e3bc8` handled the original cleanup properly.
- **Three low-severity items do remain in history**, and none justifies a history rewrite (which would break every existing clone): the Cloudflare account id in the old R2 `ServiceUrl`, the `zap-contruction` bucket name, and a hardcoded `Admin123!` seed password removed in `b2e3bc8`. That password should not be reused when staging is stood up.
- **`zap-contruction` is not a leak.** Confirmed with the user 2026-08-01: "Zap Construction" is the client's *existing* business name — he owns the domain (locked in the inaccessible `name.com` account) plus some dormant social accounts — not an unannounced future name. The rebrand question is whether to **keep** it, not whether to reveal something secret.
- **One forward-looking gap closed**: `appsettings.Staging.json` and `appsettings.Production.json` were **not gitignored**. They do not exist yet — the deployment work creates them, holding the connection string and the private database IP — so a single `git add .` would have published the infrastructure layout. Added to `.gitignore` now, while it costs nothing.
- Untracked `clean_base64.txt`/`design_base64.txt` in the repo root are base64-encoded copies of `DESIGN.md`, used for moving the design into Google Stitch. Already gitignored, no longer needed, safe to delete.

---

## 3v. Docker Containerization Started; Staging-Readiness Gaps Found and Fixed (2026-08-04)

First concrete work on roadmap Tier 4 item 19 (Hosting & CI/CD). Scope deliberately limited to the container image itself and what a live local run against it exposed — the actual staging `docker-compose`/Caddy/GitHub Actions setup is still ahead.

- **`Dockerfile` + `.dockerignore` added** at the repo root. Multi-stage build (`mcr.microsoft.com/dotnet/sdk:10.0` → `mcr.microsoft.com/dotnet/aspnet:10.0`), `.csproj` files copied and restored before the rest of the source so the restore layer stays cached across source-only changes. Runs as the base image's non-root `app` user by default (no separate `USER` directive needed on .NET 8+ images). Listens on 8080 via `ASPNETCORE_HTTP_PORTS`.
- **Verified against a throwaway local SQL Server container, not the developer's real dev database** — deliberately, both to avoid any risk to real dev data and because a *fresh, empty* database is exactly the state `handyfix_staging` is actually in (per Section 1, two empty databases already created via SSMS). `Database.Migrate()` (`Program.cs:140-149`, already environment-agnostic) created the full schema from nothing and every seeder completed cleanly inside the container.
- **Three fail-loud-outside-Development guards fired in sequence** during this test, each surfacing a real staging secret that Tier 4 item 19's bullet list didn't originally call out individually: `Admin:SeedPassword` (`AdminUserSeeder.cs`), `Brevo:ApiKey` (`Program.cs:89-104`), and `Stripe:SecretKey` (`PaymentController.cs:40-74`) all correctly refuse to run outside Development without an explicit value, rather than silently seeding a weak password or faking success. Confirms these three deliberately-strict guards (not bugs) all still work as designed under a real container boot.
- **`Stripe:AllowSandboxOutsideDevelopment` added** (`PaymentController.cs`) — the existing Development-only Stripe sandbox bypass (mock session, real booking-confirmed flow, real emails) now also activates on an explicit config flag, so staging can demo the full booking flow before a real Stripe account exists. Production stays protected by default: the flag is never set there, and a missing `Stripe:SecretKey` still throws exactly as before. Reversible with zero code changes — remove the flag from staging's environment once real Stripe test keys land, and the real integration takes over.
- **Email sender addresses made configurable.** All four call sites (`PaymentsService.cs`, `BookingsService.cs`) hardcoded `bookings@handyfix.co.uk`/`no-reply@handyfix.co.uk` as the `from` address — a domain nobody owns yet (Section 1, domain pending the rebrand decision). Brevo, like any real provider, rejects sends from an unverified sender, which would have broken the booking-confirmation email specifically (unhandled exception, 500 to the user) the moment a real `Brevo:ApiKey` was configured. Added `Email:BookingsFromAddress`/`Email:SystemFromAddress` config keys, defaulting to the existing literals so nothing changes for anyone not overriding them; `BookingsService` gained a new `IConfiguration` constructor dependency to read them (`PaymentsService` already had one). For local/staging testing, override to a real address verified as a Brevo sender — never hardcoded into a tracked file, since that would be committing personal/business PII to a public repo.
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors) and `dotnet test` (118/118 passing, both projects) after the sender-address change, including the mechanical update to 7 `BookingsService` constructor call sites in `BookingsServiceTests.cs`. End-to-end in the container: a real booking created, sandbox-bypass payment, a real confirmation email delivered via Brevo from a verified sender, the booking visible as `DepositPaid` in `/Administration`, and the resulting `Bookings`/`Payments` rows inspected directly via SSMS against the container's published port.
- **Not yet done** — the rest of Tier 4 item 19: `docker-compose.yml` + a Caddy reverse proxy (automatic HTTPS via the Hetzner reverse-DNS hostname, HTTP Basic Auth, and an `X-Robots-Tag: noindex` header so an unfinished staging site can't be crawled or indexed) on the actual staging host; the GitHub Actions workflow (new `dev` branch → build/test → push to GHCR → SSH deploy); a dedicated least-privilege SQL login for `handyfix_staging` (the local test used `sa`, which is fine for a throwaway container but wrong for a real server); and real Stripe test-mode keys once the account exists, at which point `Stripe:AllowSandboxOutsideDevelopment` gets removed from staging's config rather than kept around.

---

## 3w. Staging Is Live: Tier 4 Item 19 Complete (2026-08-04)

Everything Section 3v listed as "not yet done" now is. Roadmap Tier 4 item 19 (Hosting & CI/CD) is
closed for staging — production deploy is a deliberately separate, later piece of work.

- **`dev` branch created**; `.github/workflows/deploy-dev.yml` triggers on every push to it:
  restore → build → test → build the Docker image → push to GHCR (`ghcr.io/denidim/handyfix-web`,
  tagged `latest` and by commit SHA) using the workflow's own `GITHUB_TOKEN` (no separate registry
  credential) → SSH into the staging host and `docker compose pull && up -d`.
- **`deploy/docker-compose.staging.yml` + `deploy/Caddyfile`** committed to the repo (structure
  only, no secrets — both use `${VAR}`/`{$VAR}` placeholders). Real values live in a `.env` file
  that exists only on the staging server (`deploy/.env.example` is the tracked template,
  `deploy/.env` is gitignored). `web` publishes no ports directly; `caddy` is the only container
  with 80/443 published, terminating automatic HTTPS (Let's Encrypt, via the Hetzner reverse-DNS
  hostname — the real domain, item 14, still isn't settled) and HTTP Basic Auth in front of the
  whole site, plus an `X-Robots-Tag: noindex, nofollow` header as a second layer.
- **Dedicated SQL login** (`handyfix_staging_app`, `db_datareader`/`db_datawriter`/`db_ddladmin` on
  `handyfix_staging` only) created directly via `sqlcmd` inside the `handyfix-sql` container,
  replacing the local test's use of `sa`. `db_ddladmin` rather than `db_owner` — enough for EF
  Core's `Database.Migrate()` to create/alter schema, without the ability to drop the database or
  manage permissions.
- **A dedicated GitHub Actions SSH keypair**, separate from the developer's personal key, added
  to the staging host's `authorized_keys` alongside it. Rotating it (leaked runner, compromised
  secret) doesn't touch personal access.
- **Two real bugs, both invisible until the very first live CI runs actually happened** — this is
  exactly why "it works on my machine" isn't verification, and why standing up CI honestly was
  worth doing rather than assuming the local Docker test (Section 3v) was sufficient:
  1. **`SqliteWebApplicationFactory` didn't actually force Development for seeding purposes.**
     `AdminUserSeeder.GetSeedPassword` reads `Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")`
     directly — a deliberate choice (Section 5) since `HandyFix.Data` has no ASP.NET Core hosting
     reference to check `IWebHostEnvironment` with instead — but `builder.UseEnvironment(Environments.Development)`
     only configures the hosting abstraction, not that raw process variable. Every `WebTests.cs`
     test failed on the first-ever GitHub Actions run with the exact `Admin:SeedPassword is not
     configured` exception Section 3v had just finished explaining, because a clean runner has no
     ambient `ASPNETCORE_ENVIRONMENT` to coincidentally match. Fixed by also calling
     `Environment.SetEnvironmentVariable` in the factory, with a comment explaining why both calls
     are necessary so the next person doesn't delete the "redundant-looking" one.
  2. **The deploy step silently did nothing for two runs while reporting success.** The script had
     no `-f docker-compose.staging.yml` (not a default-discovered filename) and no `set -e`, so
     `docker compose pull`/`up -d` failed silently and the trailing `docker image prune -f` (which
     always succeeds) determined the whole step's exit code. Caught by checking the server
     directly (`docker compose ps`, empty) rather than trusting the green checkmark. Fixed with
     both the explicit `-f` flag and `set -euo pipefail`.
- **Verified live, not just "green in Actions"**: `docker compose ps` on the staging host shows
  both containers actually running; `curl` against the real public hostname over HTTPS returns
  `401` with no credentials and `200` with the correct Basic Auth header, the real homepage title,
  and the `X-Robots-Tag: noindex, nofollow` header present.
- **Real values** (SSH keys, the dedicated SQL login's password, every `deploy/.env` entry) are
  recorded in `docs/private/INFRASTRUCTURE.md`, never in a tracked file.
- **Deliberately still open, tracked there rather than blocking this**: `Stripe:SecretKey` (no
  real Stripe account yet — `Stripe:AllowSandboxOutsideDevelopment=true` covers full booking-flow
  demos in the meantime), `CloudflareR2:*` for staging (photo upload untested until a bucket
  strategy is decided — **resolved 2026-08-05, see Section 3ae**), and rotating the `sa` password on
  the DB host (currently follows the same weak pattern already flagged once in `AGENTS.md` for the
  old dev seed password).

---

## 3x. Documentation Sync: `DESIGN.md` Rewrite + 5 New `docs/WORKFLOW_*.md` Files (2026-08-04)

Closes both remaining Sprint 4 documentation gaps in one pass — `DESIGN.md` and the workflow-doc
index — using a research dump gathered by two Explore agents (full CSS tree, all five remaining
admin areas' controllers/services/models/config) so every class name, file path, line number, and
business rule cited below was confirmed against the live source rather than assumed.

- **`DESIGN.md` fully rewritten**, not incrementally patched — the old version (last touched
  2026-07-16) covered only the original color/typography/spacing tokens and knew nothing built
  since. Now documents: the full `base/variables.css` token set (flagging that two color-token
  generations coexist — the original hand-named set and a larger, later Material-You-style extended
  palette, with `--secondary` now the real accent color on current pages, not `--accent`); the
  `base/utilities.css` catalog; both page-shell patterns by name (Template A =
  `page-container`/`breadcrumb-nav`/`services-hero-title`, Template B = the auth/payment/booking-
  confirmed thin-centered pages, each with its own page-scoped classes rather than one shared pair);
  the `pages/admin.css` vocabulary, including that it loads **outside** the `site.css` `@import`
  chain entirely; the card-component family; and the area-card/coverage-map SVG classes.
- **New "Known Issues" section** (documented, not fixed, matching this file's own "found but not
  fixed" convention): `.bento-card` and `.trust-card` are each defined twice with different rules,
  in two different files apiece; `--max-width-desktop`, referenced by `.info-canvas.wide`, is never
  defined anywhere and is currently a no-op. None of these were touched — this was a documentation
  pass, not a CSS cleanup.
- **Five new `docs/WORKFLOW_*.md` files**, same structure as the two that already existed
  (`WORKFLOW_BOOKINGS.md`, `WORKFLOW_SERVICE_AREAS.md` — business rule first, then code location, a
  troubleshooting table, a Related section):
  - **`WORKFLOW_SERVICES.md`** — services & categories admin, and the `ImageStorageService` WebP
    pipeline. States plainly that **categories have no admin UI or controller at all** (seed-only,
    `ServiceCategoriesSeeder` inserts-only) — easy for an admin to go looking for and not find.
    Documents the `{slug}-hero.webp` convention's hardcoded locations — now **five**, not four: a
    fifth spot in `Views/Administration/Services/Edit.cshtml` (builds the same path to decide
    whether to show a "current image" preview) was found during this pass and wasn't previously
    recorded anywhere, including here. Also documents `ServicesService.Slugify`'s known
    double-hyphen bug (`Replace("--", "-")` runs once, not recursively — `"Walton-on-Thames &
    Weybridge"`-shaped names don't fully collapse) and that `CategoryViewModel.Slug` has its own,
    separate, even simpler buggy slug computation.
  - **`WORKFLOW_TECHNICIANS.md`** — roster CRUD, the deactivate-vs-delete distinction (delete
    refused whenever *any* booking, including soft-deleted ones, references the technician), the
    actual `TempData` messages the controller shows, and the `John Doe` seed-once placeholder.
  - **`WORKFLOW_REVIEWS.md`** — admin approve/delete, and the `Business:ShowOnSiteReviews`/
    `GoogleReviewsUrl` public-display gate stated as explicitly separate from approval itself (an
    approved review still won't show publicly if the toggle is off).
  - **`WORKFLOW_ENQUIRIES.md`** — opens with the Enquiry/Inquiry naming split (admin/URL/view-model
    layer says "Enquiry", entity/service/DB layer says "Inquiry", both intentional) specifically so
    it isn't rediscovered by grep-confusion later. Notes `Inquiry` has no status-shaped field at all,
    which is why there's no status filter here unlike Bookings/Reviews.
  - **`WORKFLOW_DEPLOYMENT.md`** — the only one of the five with a hard constraint: public-repo-safe,
    no real hostnames/IPs/credentials anywhere, pointing to `docs/private/INFRASTRUCTURE.md`/
    `STAGING_RUNBOOK.md` by filename only for real values (verified by re-reading the finished file
    against every placeholder already used elsewhere in this repo before treating it as done).
    Documents the CI pipeline, the compose/Caddy stack, the startup seeding sequence, and that
    `AdminUserSeeder` fails app startup entirely outside Development if `Admin:SeedPassword` is
    unset — the exact failure mode the private runbook's troubleshooting section already covers in
    more detail with real values.
- **README's Documentation section** now lists all 7 docs and the "in progress" note naming the
  5 gaps is gone — that note existed specifically to be deleted once this landed.
- **Nothing in the code was changed.** Every discrepancy found along the way (the fifth hardcoded
  image-path location, the two duplicate class-name pairs, the dead CSS variable) is recorded as a
  known issue for a future pass, not fixed here — this was scoped as documentation only.

---

## 3y. Known Issues Fixed: CSS Duplicates + the Slug Bug (2026-08-04)

The three CSS "Known Issues" and the `Slugify` bug Section 3x had just finished documenting as
found-but-not-fixed are now actually fixed — first item of a larger code-cleanup pass (explicit
types, thin controllers, and an authorization flip are queued next as separate phases).

- **`.bento-card` name collision resolved by renaming, not merging.** The two components were
  never actually the same thing (public bento image tiles vs. admin Dashboard stat cards) — renamed
  only the `pages/admin.css` family to `.dashboard-stat-card` (and its `-header`/`-icon-wrapper`/
  `-value`/`-label` children), updating the 4 stat-card usages in
  `Areas/Administration/Views/Dashboard/Index.cshtml`. The public `.bento-card` in
  `components/cards.css` and its one consumer (`Home/Index.cshtml`) are untouched.
- **`.trust-card` duplicate removed.** Confirmed the `components/cards.css` definition was always
  losing to `pages-info.css`'s (later in the `site.css` import chain) for the one view that uses it
  (`Home/Index.cshtml`) — deleted the losing, dead definition. Zero visual change, since it was
  never the one rendering.
- **`.info-canvas.wide` removed**, not given an invented width. Nothing in the codebase applied
  `.wide` to `.info-canvas` and `--max-width-desktop` was never defined anywhere — genuinely unused
  code with zero consumers, so it was deleted rather than guessing at a design value nobody asked
  for. If a wider info-page variant is ever needed, it should be added deliberately when something
  actually uses it.
- **The `Slugify` bug is fixed at the root, and de-duplicated.** `ServicesService.Slugify` and
  `CategoriesService.Slugify` were byte-for-byte identical private methods, both with the same bug
  (`.Replace("--", "-")` runs once, not repeatedly, so 3+ consecutive hyphens don't fully collapse —
  e.g. `"Walton-on-Thames & Weybridge"` → `walton-on-thames--weybridge`). Replaced both with a call
  to one new shared `HandyFix.Services.Data.Common.SlugGenerator.Slugify`, which collapses any run
  of hyphens via `Regex.Replace(slug, "-{2,}", "-")`. Covered by a new regression test,
  `ServicesServiceTests.CreateAsyncShouldCollapseMultipleHyphensInSlug`.
- **`CategoryViewModel.Slug`'s independent third slug algorithm removed.** It used to recompute
  `Name.Replace(" ", "-").ToLower()` fresh on every access (no `&`/`/` handling at all, and
  disconnected from whatever `CategoriesService.CreateAsync` had actually persisted). Now an ordinary
  mapped property, sourced directly from `ServiceCategory.Slug` via an explicit Mapster mapping — one
  source of truth instead of three independent slug implementations across the codebase.
  `ServiceAreasService` deliberately still doesn't use `SlugGenerator` — its slug stays an explicit,
  admin-typed field, unaffected by this change (see Section 3m for why that design is intentional).
- **Docs updated in the same pass**: `DESIGN.md`'s "Known Issues" section is removed (nothing left
  to list) and its admin/card-component sections updated to describe the renamed/deduplicated
  classes; `docs/WORKFLOW_SERVICES.md`'s `Slugify` section rewritten to describe the fix instead of
  the bug.
- **Verified**: `dotnet build` (0 errors) and the full test suite passing, plus a live app run
  confirming the renamed Dashboard stat cards and the Home page trust/bento sections render
  unchanged.

---

## 3z. Code Cleanup Phase 2: `var` → Explicit Types (2026-08-04)

Second phase of the same cleanup pass (Section 3y was phase 1). Production code (`Data`, `Services`,
`Web`) now uses explicit local variable types instead of `var`; test projects are untouched by
agreement, since `var` there is the standard convention and the volume (741 of 1101 total usages)
outweighs the benefit.

- Added a repo-root `.editorconfig` (none existed before) setting `csharp_style_var_for_built_in_types`,
  `csharp_style_var_when_type_is_apparent`, and `csharp_style_var_elsewhere` to `false:suggestion`,
  so the change doesn't silently erode on future PRs.
- Applied mechanically via `dotnet format style --diagnostics IDE0008 --exclude src/Tests` rather
  than by hand — safer than manual/regex editing since Roslyn can't produce a type mismatch, and far
  faster across ~360 production occurrences. 42 files touched, 234 insertions / 212 deletions.
- Anonymous-type sites (`new { ... }`) are unaffected, since `var` is required there.
- **Verified**: `dotnet build` (0 errors) and the full test suite (119) unchanged — only local
  variable declarations were rewritten, no signatures changed.

---

## 3aa. Code Cleanup Phase 3: Thin Controllers (2026-08-04)

Third phase of the cleanup pass. Target pattern taken from the user's own earlier project
([FindATrade](https://github.com/denidim/Final-CSharp-Web-Project), same course template HandyFix
started from): a controller action calls **one** service method and translates the result —
no inline file/stream handling, no inline third-party SDK calls, no raw repository queries, no
hand-rolled aggregation. Eight controllers touched, each its own commit, each verified with a full
`dotnet build` + test run before moving to the next:

- **Admin `ServicesController`** — the case named explicitly. `Create`/`Edit`'s `IFormFile` stream
  reads, `ImageStorageService` calls, and SkiaSharp-error-to-ModelState handling moved into two new
  `ServicesService` methods, `SetServiceImageAsync`/`UpdateServiceImageAsync`; `DeleteAsync` now
  deletes the image itself, so the controller no longer injects `IImageStorageService` at all. Also
  added `IImageStorageService.GetServiceImagePublicUrl(slug)`, and had `UpdateServiceImageAsync` call
  it for the slug-rename case instead of rebuilding the URL inline — this fully eliminates one of the
  five places `docs/WORKFLOW_SERVICES.md` recorded the `{slug}-hero.webp` convention as hardcoded
  (the old `ServicesController.cs:168`), down to four. *(That doc still said five and this section
  still said "one more of the five" until a documentation-accuracy pass caught both — Section
  3ad.)*
- **`DashboardController`** — four raw `IDeletableEntityRepository<T>` injections replaced with one
  count/sum method each on `IBookingsService`/`IInquiriesService`/`IReviewsService`/
  `IPaymentsService`. The `PendingReviewsCount` `AllWithDeleted()` quirk was preserved exactly, not
  quietly "fixed" while relocating it.
- **`CalendarController`** — added `IAvailabilityService.GetAllSlotsForDayAsync`, a sibling of the
  existing `GetAllSlotsForDateAsync` *without* its `> now` cutoff (confirmed by reading both
  queries side by side: the admin calendar must still show a day's already-elapsed slots, unlike
  the customer-facing method — swapping in the existing method as originally planned would have
  been a real regression, caught before it shipped).
- **`BookingsController`** — the status-dropdown query and the Today/Pending/Revenue card
  arithmetic both moved to `IBookingsService` (`GetStatusOptionsAsync`, a pure
  `GetSummaryStats(bookings)`). The controller still decides whether to re-fetch the unfiltered
  list or reuse what it already has — that's a per-request efficiency call, not a business rule —
  so the existing "no duplicate query when unfiltered" behavior and its test survived unchanged.
- **`ReviewsController`** — same `GetSummaryStats(reviews)` pattern on `IReviewsService`.
- **`SeoController`** — sitemap `XElement`/`XNamespace` construction extracted to a new static
  `HandyFix.Web.Services.SitemapXmlBuilder`. URL *generation* (`Url.Action`/`Url.RouteUrl`) stays in
  the controller since it's inherently `IUrlHelper`-bound, not something the service layer should
  depend on.
- **`ServiceAreasController`** — FAQ row pruning and the length/required validation rules moved
  into `IServiceAreasService.PruneAndValidateFaqs`, returning `(key, message)` errors instead of
  writing to `ModelState` directly (`ModelState` itself stays a controller concern — it's
  MVC-specific). `EnsureAtLeastOneFaqRow` stays in the controller: guaranteeing the redisplayed form
  has a row to type into is a view concern, not a business rule.
- **`PaymentController`** — the highest-risk item, done last with extra care. The entire
  sandbox-bypass-vs-real-Stripe-Checkout decision, `SessionCreateOptions` construction, and webhook
  signature verification/event dispatch moved into two new `IPaymentsService` methods,
  `CreateCheckoutSessionAsync` and `HandleWebhookEventAsync`. `StripeConfiguration.ApiKey`
  assignment moved to `PaymentsService`'s constructor to match. **Found and fixed a real issue
  along the way**: adding `Stripe.net` to `HandyFix.Services.Data` pulled in a vulnerable
  `Newtonsoft.Json 12.0.3` transitively (GHSA-5crp-9r3c-p9vr) that had only ever been silently
  masked in `HandyFix.Web` by an unrelated package's higher floor — pinned to `13.0.3` in
  `Directory.Packages.props` (transitive pinning, same mechanism already used for the
  `SQLitePCLRaw` CVE in Section 3t).
- **Tests**: every relocated behavior got its test suite moved to where the behavior now actually
  lives (e.g. `AdminDeletionTests`' "image deleted before the row" case → `ServicesServiceTests`;
  `AdminBookingsControllerTests`' summary-card arithmetic → `BookingsServiceTests`;
  `AdminServiceAreasControllerTests`' FAQ-pruning cases → a new
  `ServiceAreasServiceTests.PruneAndValidateFaqsTests`; the entire Stripe sandbox-bypass contract →
  `PaymentsServiceTests.CreateCheckoutSessionAsyncTests`). Controller test files kept only the
  wiring-level checks. Net suite size after all eight: 130 (up from 119 at the start of the phase).
- **Deliberately left as-is**: `BookingController`'s exception-to-view-redisplay try/catch (that
  *is* the controller's job) and the Identity `Register` page's direct `ApplicationUser`
  construction (standard scaffolding, not HandyFix business logic).
- **Verified**: `dotnet build` (0 errors, 0 warnings after the Newtonsoft.Json fix) and the full
  test suite after every controller, not just at the end.

---

## 3ab. Code Cleanup Phase 4: Remove `SettingsController`; `[Authorize]`-by-Default (2026-08-04)

Final phase of the cleanup pass.

- **`SettingsController` deleted entirely**, not locked down. It was public, carried **no**
  `[Authorize]`, and its `InsertSetting` action wrote a row to the database on a bare GET with no
  CSRF protection — leftover Nikolay Kostov template scaffolding (confirmed via grep: no link to
  `/Settings` anywhere in the real site). Removed the controller, its view, `SettingsListViewModel`/
  `SettingViewModel`, `ISettingsService`/`SettingsService`, their tests, the Dashboard's
  `SettingsCount` stat (confirmed unused — computed but never actually rendered in the Dashboard
  view), and the matching demo code in `Tests/Sandbox/Program.cs` (its only purpose was exercising
  `ISettingsService.GetCount()`). The underlying `Setting` entity/table and its migration were left
  alone — dropping those is a schema change out of scope here.
- **`BaseController` now carries `[Authorize]`**, flipping the site from "open unless protected" to
  "protected unless opened." `[AllowAnonymous]` added to the six controllers that must stay public:
  `HomeController`, `AreasController`, `BookingController`, `PaymentController` (covers the Stripe
  webhook too — Stripe cannot authenticate as a HandyFix user), `SeoController`, and the public
  `ServicesController`. `AdministrationController`'s existing `[Authorize(Roles=Administrator)]`
  stays, now redundant-but-correct on top of the base attribute. Identity Razor Pages
  (Login/Register) are untouched — `PageModel`s don't inherit `BaseController`.
- **Confirmed today's actual auth surface, before touching anything**: exactly one
  `[Authorize(Roles=...)]` on `AdministrationController` and nothing else anywhere in the app — this
  flip changes the *default*, not which routes were actually reachable by whom.
- **Verified two ways**: the full test suite (128/128 — the integration tests hit these routes
  anonymously via a real HTTP client and assert `EnsureSuccessStatusCode()`, so a missing
  `[AllowAnonymous]` would have failed as a redirect instead of 200); and a live run curling every
  public route (200), every `/Administration/*` route unauthenticated (302), and the webhook (400
  for an invalid signature — reachable, correctly rejected, not 401/302).

**Net effect of the four-phase cleanup pass**: the CSS/slug Known Issues are fixed, production code
uses explicit types with an `.editorconfig` to keep it that way, eight controllers are thin, one
piece of unauthenticated dead scaffolding is gone, and the site defaults to locked-down. 13 commits
across the four phases (plus this doc pass), each independently built and tested.

---

## 3ac. Bug Fix: Cookie Consent Never Actually Persisted Behind Caddy (2026-08-04)

Reported by the user testing staging: the cookie banner reappears on every new page, even
immediately after clicking Accept. Root cause traces back to the same infrastructure gap for two
unrelated symptoms — worth recording together since the fix is one change, not two.

- **Root cause**: staging runs behind Caddy, which terminates real HTTPS from the browser and
  proxies to the `web` container over plain HTTP on the internal Docker network (see Section 3w).
  `Program.cs` never called `UseForwardedHeaders()`, so the app never read the `X-Forwarded-Proto`
  header Caddy sends by default — every request looked like plain HTTP to the app, regardless of
  what the browser actually used.
- **Symptom 1 (reported)**: the consent cookie's `CookiePolicyOptions.MinimumSameSitePolicy` was
  set to `SameSiteMode.None`, which browsers only honor if the cookie is also `Secure`. Since the
  app believed every request was HTTP, `SecurePolicy=SameAsRequest` never added `Secure`, and
  browsers silently discarded a `SameSite=None` cookie without it. The banner's `Accept` click
  still ran its JS (`banner.remove()`), so it *looked* dismissed — but nothing persisted, so the
  next page load had no consent cookie and showed it again. Forever.
- **Symptom 2 (found while investigating, not reported)**: `PaymentController.Pay` builds the
  Stripe Checkout `SuccessUrl`/`CancelUrl` from `Request.Scheme`, which was reading `http` for the
  same reason — a real Stripe redirect back to the customer would have pointed at an insecure URL
  on staging (and later production).
- **Fix**: added `services.Configure<ForwardedHeadersOptions>(...)` (`XForwardedFor` +
  `XForwardedProto` + `XForwardedHost`, with `KnownNetworks`/`KnownProxies` cleared — Docker Compose
  assigns Caddy's address dynamically, so there's no fixed IP to allow-list; this is safe because
  `web` publishes no ports of its own, so Caddy is the only thing that can reach it at all) and
  `app.UseForwardedHeaders()` as the first line of the middleware pipeline, before exception/HSTS
  handling. Also changed `MinimumSameSitePolicy` from `None` to `Lax` — this is an ordinary
  first-party cookie with no cross-site need, and `Lax` never requires `Secure` at all, so the
  banner now also works correctly over bare HTTP in local dev (which has no proxy adding forwarded
  headers), not just in the specific staging/production topology.
- **Verified live, not just by reasoning**: ran the app locally and compared the generated consent
  cookie string with and without a forged `X-Forwarded-Proto: https` header — `secure` only appears
  when the header says HTTPS, exactly as intended. Confirmed the same fix corrects the general
  `Request.Scheme`-based URL generation too, using `sitemap.xml`'s URLs (built the same way the
  Stripe URLs are) as a proxy: `http://` without the header, `https://` with it.

---

## 3ad. Documentation Accuracy Audit: Cross-Reference Drift Caught and Fixed (2026-08-04)

Requested pass over this file specifically to verify every claim against the actual current
code/repo state, not to add new entries — the four-phase cleanup (Sections 3y–3ab) and the cookie
fix (Section 3ac) each moved or renamed things this file references elsewhere, and a couple of
cross-references weren't updated in the same pass that made them stale.

**Found and fixed, this file:**
- Tier 5 item 20 (dev-database housekeeping) still credited `ServicesController.Delete` with
  deleting the image before the row — that logic moved into `ServicesService.DeleteAsync` in
  Section 3aa's thin-controller pass. Corrected the reference and noted the move inline.
- Section 1's Hosting & Infrastructure subsection still framed staging as "provisioned but not yet
  wired up" and named `appsettings.Staging.json`/`appsettings.Production.json` as the intended
  config approach — both written 2026-07-30, before Section 3w (2026-08-04) actually stood staging
  up on environment variables via `deploy/.env`, not appsettings files. Rewrote to state staging is
  live and describe the real config mechanism.
- Section 3aa itself described the `ServicesController.cs:168` removal as "centralizing one more of
  the five" hardcoded `{slug}-hero.webp` locations — the correct count after that removal is four,
  not five-minus-one-still-called-five. Corrected the wording and pointed forward to this section.
- Section 4 Tier 1 item 1 still listed all "four independent places" the hero-image path convention
  was hardcoded, including the now-deleted `ServicesController.cs:168` entry. Replaced the stale
  inline list with a pointer to `ImageStorageService.GetServiceImagePublicUrl` (the canonical source
  added in Section 3aa) and to `docs/WORKFLOW_SERVICES.md` for the current, accurate list.
- Section 2's Email Dispatcher bullet still described `SendGridEmailSender`/`SendGrid:ApiKey` as the
  live implementation — accurate for what Sprint 1 actually shipped, but SendGrid was replaced by
  Brevo in Section 3q (2026-07-31) and the bullet never got an update marker. Left the historical
  record itself intact (this file doesn't rewrite what already shipped) and appended a
  superseded-annotation pointing at Section 3q instead.

**Found and fixed, elsewhere:**
- `docs/WORKFLOW_SERVICES.md` still said the hardcoded path convention appeared in "five" places and
  listed `ServicesController.cs:168` as one of them — the same stale count as the Section 3aa item
  above. Updated to four and renumbered the remaining list.

**Verified, no changes needed:**
- Structural integrity: every `##`-level section header from 1 through 5 is present exactly once, in
  order, with no gaps or duplicates (`1, 2, 3, 3a`–`3z`, `3aa`, `3ab`, `3ac`, `3ad`, `4`, `5`).
- The top "Last updated" blockquote's parenthetical nesting is balanced.
- Test count: the file's "130 (up from 119)" (Section 3aa) and "128/128" (Section 3ab and
  elsewhere) claims are both correct as stated, not contradictory — 130 was the count right after
  Phase 3; Phase 4 then deleted `SettingsController`'s two tests, landing at 128, which `dotnet
  test` confirms is still the exact count today (73 + 55 across the two test projects).
- Section 4's roadmap tiers and Section 5's architectural decisions were spot-checked against the
  code they reference and found current — no drift found there this pass.

This kind of drift is structural, not a one-off mistake: a file this size, updated inline every
session, will keep doing it unless a cross-reference is treated as a real dependency at edit time,
not just prose. No fix for that beyond catching it before it compounds further.

---

## 3ae. Bug Fixes: Mobile Horizontal-Scroll Overflow, Silent Booking-Form Failures, CloudflareR2 Wired Up for Staging (2026-08-05)

Surfaced by the user's first live mobile testing pass against staging: pages drifting/zooming
sideways on swipe, a booking silently failing (no visible error) when a photo was attached but
succeeding without one, and no documented way to inspect the staging database directly.

**Mobile horizontal-scroll overflow, fixed:**
- Root cause, found by reproducing locally with Playwright at 375px/320px and diffing
  `document.documentElement.scrollWidth` against the true viewport width: Home's Trust-section
  decorative glow elements (`.blur-circle`, `home.css`, absolutely positioned with negative
  `top`/`right`/`left` offsets) bled 16–40px past their `.trust-image-wrapper` container, which had
  no `overflow: hidden` of its own.
- `body { overflow-x: hidden }` alone (pre-existing) was not sufficient to contain this on mobile —
  `scrollWidth` still measured 391/336 against a 375/320 viewport even with it set. Fix extends the
  rule to `html, body` in `reset.css`, which is the actual scrolling root; verified this brings
  `scrollWidth` back to an exact match at both widths.
- The same check (cold load and interactive, including selecting a category/service/date to render
  the calendar/slots) found **no** overflowing element on Booking or Services locally, before or
  after the fix, despite the user also reporting Booking's footer breaking the same way. Could not
  independently reproduce that one — the global `html`/`body` rule should still guard against this
  whole class of bug there regardless, but this is flagged rather than claimed fixed with evidence.

**Booking form silently reloading when a photo was attached, fixed — two independent layers:**
- Layer 1 (this section): `Booking/Index.cshtml` had no `asp-validation-summary` anywhere — every
  existing `<span>` is `asp-validation-for`, tied to one specific field. `BookingController`'s
  `RedisplayBookingForm` adds page-level failures (slot race, or any unhandled exception) via
  `ModelState.AddModelError(string.Empty, ...)`, which had nowhere to render. Any generic booking
  failure — not just the R2 one below — was therefore always invisible to the customer. Fixed by
  adding `<div asp-validation-summary="ModelOnly">` to the top of the form.
  - Caught and fixed before commit: the div's original `font-weight-bold` class doesn't exist in
    this codebase (Bootstrap 5 renamed it `fw-bold`) — the same no-op-class mistake already
    documented in Section 3a (`text-right`/`text-left`).
  - Verified by full suite (128/128) and reading the tag-helper's `ModelOnly` scoping against
    `RedisplayBookingForm`'s empty-key error — **not yet spot-checked live in a browser**; worth
    doing on the next staging pass rather than assumed from code alone.
- Layer 2, see below: the specific trigger was CloudflareR2 not being configured on staging at all,
  which made every photo upload throw.

**CloudflareR2 wired up for staging:**
- Decision made: staging reuses the existing dev bucket (`zap-contruction`) rather than a dedicated
  one — this was the one open question blocking Section 3w.
- `docker-compose.staging.yml` maps `CloudflareR2:*` to five new `R2_*` env vars, same pattern as
  Brevo/Stripe; documented in `deploy/.env.example`. `CloudflareR2Service` now throws a clear
  `InvalidOperationException` if any are missing instead of failing inside the AWS S3 SDK with an
  opaque error — same fail-loud pattern as Stripe/email (Section 5).
- Real values added directly to `/opt/handyfix/deploy/.env` on the staging host over SSH (a
  timestamped backup of the prior file, `.env.bak-*`, was left alongside it) and recorded in
  `docs/private/INFRASTRUCTURE.md`, never in a tracked file — `deploy-dev.yml` does not regenerate
  this file from anything on push, it only pulls the image and runs `docker compose up -d` against
  whatever `.env` is already on the box, so this had to be a manual step.
- Deployed via the normal `dev` push → `deploy-dev.yml` pipeline (commit `cf44489`); build, test, and
  deploy all succeeded, confirmed via the public Actions run.
- **A real-device check by the user surfaced a second, deeper bug**: booking with a photo on staging
  still threw `Cloudflare R2 is not fully configured`, even after the steps above. Root cause —
  `deploy-dev.yml`'s SSH step (`docker compose -f docker-compose.staging.yml pull && up -d`) restarts
  containers using **whatever `docker-compose.staging.yml` is already on the server**, never the repo
  version; it has no step that copies the compose file (or `Caddyfile`) across, ever. The `.env` fix
  above genuinely landed on the server, but the *compose file's new env-var mappings* — the part
  reading `.env` and turning it into `CloudflareR2:*` config keys the app can see — did not, because
  the server's copy predated that change. This is not a one-off mistake: it's the same "config lives
  on the server, not synced by CI" pattern already true of `.env`, just for a second file that's
  easier to assume is kept current *because* it's git-tracked.
  - Fixed: the corrected `docker-compose.staging.yml` was written to the server over SSH (a
    timestamped `.bak-*` of the prior version left alongside it, same as the `.env` backup).
  - **`docs/WORKFLOW_DEPLOYMENT.md` and `docs/private/STAGING_RUNBOOK.md` both had the identical
    blind spot** — neither previously stated that the compose file and `Caddyfile` are static,
    server-resident files the pipeline never re-syncs. Both corrected: a new explicit section in the
    public doc, a new incident writeup (Случай 6) in the private runbook, plus the stale
    "CloudflareR2 not configured"/"untested" lines in each fixed to match the current state.
  - **Confirmed working 2026-08-05**: the next `dev` push (the docs commit above, `93e33f9`)
    re-triggered `deploy-dev.yml`, which restarted the containers against the corrected compose file
    without anyone needing to SSH in and run `up -d` by hand. The user then re-tested a booking with
    a photo attached on live staging and confirmed it completes successfully — the R2 upload, and the
    booking-to-payment flow around it, both work end to end.
- **Mobile horizontal-scroll fix also confirmed working on a real device.**
- **Also raised, not yet decided**: whether `deploy-dev.yml` should sync the whole `deploy/` folder
  (compose file + `Caddyfile`) onto the server on every push, and separately whether it should
  regenerate `.env` from GitHub Actions repo secrets instead of relying on hand-maintained files —
  the gap that made both of today's config-drift bugs unanswerable from the repo alone in the first
  place. Deliberately deferred as a separate follow-up, not bundled into this fix.

---

## 3af. Bug Fix: Live Client-Side Validation Was Silently Broken App-Wide (2026-08-05)

Surfaced by a real complaint: filling in the booking form gave no indication of what was wrong when
the submit button stayed disabled. Root cause turned out to be app-wide, not Booking-specific.

- **Root cause**: `libman.json` pinned jQuery to **4.0.0**, which removed `$.parseJSON` entirely. The
  bundled `jquery-validation-unobtrusive` still calls it internally whenever it displays an error
  message, so every form's live validation was crashing silently the moment it tried to show
  anything — confirmed via a `pageerror: s.parseJSON is not a function` thrown from inside
  `.valid()`. This is the same "pre-existing, unrelated" console warning already noted from the
  login page back in Section 3f — it wasn't unrelated, it was this.
- **Fix**: pinned jQuery back to **3.7.1** (the last 3.x release, still current/maintained, and what
  `jquery-validation-unobtrusive` is actually built against) — `libman.json` + the vendored
  `wwwroot/lib/jquery/dist/*` files it restores on build via `Microsoft.Web.LibraryManager.Build`.
  Verified live on both Booking and Contact: real `[MinLength]`/`[EmailAddress]`/`[Required]`
  messages from `BookingInputModel`'s own DataAnnotations now render live in the
  `asp-validation-for` spans that were already sitting unused in the markup — this fixes every form
  in the app, not just Booking, since none of them ever needed new markup, only a working library.
- **`Booking/Index.cshtml`**: added a plain visible hint under the submit button specifically for
  service/time-slot selection, since those are custom JS widgets with no typed input for a
  DataAnnotations message to attach to — nothing else would ever have told the user that's what was
  blocking "Proceed".
- **Tried and reverted**: routing the submit button's enabled state through `$("#booking-form").valid()`
  as a single source of truth, called on every keystroke. This looked like the more "correct" unified
  approach, but empirically it corrupted jQuery Validate's own internal per-field state — even a
  fresh, isolated `.valid()` call on one field afterward started returning wrong results. Reverted to
  the original presence-based check for button gating, left jQuery's own native per-field blur/keyup
  handlers untouched to do the message display on their own (which they now do correctly on their
  own, now that the library itself works).
- **Why downgrade instead of patching `jquery-validation-unobtrusive` for jQuery 4**: there's no
  jQuery-4-compatible release of that library to upgrade to — this isn't a "we're behind" gap, it's
  jQuery 4.0.0 (a very recent, aggressive major release) getting ahead of its own plugin ecosystem.
  jQuery 3.x is still actively maintained in parallel by the jQuery team for exactly this reason.
  Forking/patching the validation library ourselves was considered and rejected as more ongoing
  maintenance burden than pinning to the version its own author built it against.
- **Verified**: `dotnet build`/`dotnet test` (128/128) unaffected, as expected for a client-side-only
  change; live Playwright testing of both the failure mode (pre-fix, confirmed the crash) and the fix
  (post-fix, confirmed correct live messages) on Booking and Contact.

---

## 3ag. Test Coverage: `CategoriesServiceTests`/`ServicesServiceTests`, and a Latent Test-Infrastructure Gap Found Along the Way (2026-08-05)

With the client call (roadmap Tier 0) still pending, this closes the Sprint 4 note that flagged
`CategoriesServiceTests` (1 test) and `ServicesServiceTests` (5 tests) as the thinnest spots in the
suite — 20 new tests added covering every previously-untested method on both services (the read
methods, `UpdateAsync`, and all three image-management methods on `ServicesService`).

- **A real bug was found while writing the very first assertion on a custom-mapped property**:
  `CategoryViewModel.BasePrice` (its Mapster `IHaveCustomMappings` rule, `min` of the category's
  services) always came back `0`, no matter what was seeded. Root cause was **not** the code under
  test — `HandyFix.Services.Data.Tests` had never once called `MappingConfig.RegisterMappings`
  anywhere in the project. `Program.cs` calls it at real app startup, and `HandyFix.Web.Tests`
  inherits it for free by booting the real `Program` (Section 3t) — but this pure-unit-test project
  does neither, so `MappingConfig.GlobalConfig` had been `null` for every test that ever ran here.
  Mapster doesn't throw on a null config; it silently falls back to its own bare defaults
  (plain property-name matching, no custom `.Map()` rules at all). Plain properties matched by name
  were never affected, which is exactly why this stayed invisible: no existing test before today
  happened to assert on a property that only exists via a custom mapping.
- **Fixed at the source, not per-test**: new `Tests/HandyFix.Services.Data.Tests/MappingTestSetup.cs`
  registers the real mappings once via a `[ModuleInitializer]`, so every test in the project now runs
  against the same Mapster configuration the live app actually uses. This is a **test-only fix — no
  production code changed**, and it is not a live bug: `Program.cs` has always called
  `RegisterMappings` for real, so the actual site was never affected.
- **That fix immediately un-masked three more failures in pre-existing `BookingsServiceTests`** (not
  written today), for the same underlying reason: `BookingDetailsViewModel.PaymentStatus`'s mapping —
  `Payments.Any() ? Payments.OrderByDescending(...).First()... : "Unpaid"` — had also never actually
  run against real Mapster config before. Once it did, three sort/filter tests whose seeded bookings
  have no `Payment` rows (a normal state — any booking that hasn't reached Stripe checkout yet looks
  exactly like this) started throwing `Sequence contains no elements`. Confirmed via a Sqlite-backed
  rerun of the same scenario that this is **EF Core's InMemory provider failing to short-circuit the
  ternary** (it evaluates `Payments.First()` on the "then" branch unconditionally instead of only when
  the guard is true) — a real relational engine handles it correctly, so this was never reachable in
  production either. Fixed by switching `CreateBookingsServiceWithThreeBookings` (the shared helper
  behind all three tests) from InMemory to Sqlite in-memory, the same treatment already established in
  this file for transaction/concurrency tests InMemory can't represent correctly (see Section 5's
  architectural-decisions note on this).
- **Two of the 20 new tests needed the same Sqlite treatment up front**, for the identical reason:
  `GetAllAsyncShouldComputeBasePriceAsMinimumOfItsServices` and
  `...ShouldReturnZeroBasePriceWhenCategoryHasNoServices` exercise that same `Services.Min(...)`
  aggregate — this is now a second documented instance of "ternary/aggregate over a correlated
  collection navigation needs Sqlite, not InMemory," worth remembering before writing the next test
  that hits this shape.
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors, warning count unchanged apart from the one
  expected `SA1412` on the new file, same as every other file in this project); full suite
  `HandyFix.Services.Data.Tests` (75 → 95) + `HandyFix.Web.Tests` (55, untouched) = **150/150 passing**.

---

## 3ah. Tier 0 Client Call (First Sitting): Rebrand Name and Domain Resolved (2026-09-01)

No code changed this session — business decisions only, made live with Zaprqn on the call that
Section 4 had been waiting on since 2026-07-30. Full reasoning logged in
`docs/private/VISION_AND_CONTEXT.md` Sections 5.3, 5.6, 5.7, 5.9, and 6; this records what shipped
and what's still open against the roadmap.

- **Business name resolved: "Plumbing Handyman Surrey."** Two alternatives were considered and
  dropped: "Zap Construction" (the client's existing dormant name — reads as electrical work, mismatched
  the plumbing/handyman mix) and "HandyFixFix" (a rhyming suggestion, rejected as too close to the
  retired "HandyFix" placeholder and non-descriptive of the actual services). The chosen name
  literally states both service categories already in the data model (`Plumbing`, `Handyman`
  `ServiceCategory` rows), so no marketing copy has to explain what the business does.
- **Domain bought fresh, not recovered.** The old Zap Construction/name.com locked-mailbox situation
  (`PROJECT_STATE.md`'s prior Tier 3 item 14, `VISION_AND_CONTEXT.md`'s prior Section 5.6) is moot now
  that the name changed. Registered via `names.co.uk`: `plumbing-handyman-surrey.co.uk` (primary, with
  Domain Proxy for WHOIS privacy) and `plumbing-handyman-surrey.com` (backup). Deliberately **not**
  bought: the no-"Surrey" variants and the no-hyphen merged variants — priced and considered, cut to
  keep the client's spend down at launch (squatting risk on an unknown local business name is low
  right now; cheap to add later if it becomes real). Denislav manages the `names.co.uk` account,
  Zaprqn paid on his own card — full detail, including the recovery-email/2FA responsibility this
  arrangement carries, in `VISION_AND_CONTEXT.md` Section 5.6.
- **Hyphen + "Surrey" were both deliberate, not defaults.** `plumbing-handyman-surrey` (hyphenated,
  "Surrey" included) was chosen over a shorter no-hyphen/no-"Surrey" alternative that had a real
  SEO/inclusivity argument for it (avoids implying South-West-London areas already in the 15-area
  footprint — Kingston, Wimbledon, Sutton — are out of scope). Zaprqn's explicit call: he wants the
  Surrey identity and accepts that trade-off. Recorded as a deliberate positioning choice so it isn't
  mistaken for an oversight later.
- **Insurance figure: closed, not just outstanding.** Coverage was already confirmed real
  (2026-07-30); today's session closed the open half of that item for good — Zaprqn will not publish
  an exact figure or certificate at all. The existing site copy ("Comprehensive Public Liability
  insurance," no number) already matches this decision, so no code change was needed — it happened to
  already be written the right way.
- **New task, not on the original checklist**: Zoho Mail, not the registrar's own email add-on.
  `names.co.uk` offered branded mailboxes at £8/month for 2 (recurring, not one-off); Zoho Mail's free
  tier covers up to 5 branded addresses on the same domain at £0 (webmail/app access only on the free
  tier — no IMAP/POP without upgrading to its $1/user/month Lite plan). Not yet set up: needs MX
  records pointed at Zoho in the `names.co.uk` DNS panel, then mailbox creation.
- **Clarified, not collected**: worker photos aren't needed per-worker, only Zaprqn's own — the only
  public-facing destinations for a technician photo (the single-person bio block, the
  confirmation-email card) were never a team roster, so a worker's photo would have no UI to appear
  in. Names and phone numbers for the whole roster are still needed and were **not** collected this
  session.
- **Still open after this session** (see Section 4 for the full current state of every roadmap item):
  years trading, real phone number, real trading address, his photo and name, the worker roster
  itself, Custom Projects scope, and a rough completed-jobs count — none of these were reached in this
  sitting of the call.
- **Also surfaced, unrelated to the call, still unresolved**: staging currently shows "No availability"
  on every date. Root cause diagnosed — `DevelopmentCapacitySeeder` only tops up booking capacity at
  application startup, and the staging container hasn't restarted since the last `dev` deploy
  (2026-08-05); its originally-seeded 14-day window ran out around 2026-08-19. Fix is a container
  restart (re-runs the seeder) or a manual admin-panel slot generation — not yet done.

---

## 3ai. Site-Wide Visual Design Refresh: Body Texture, Glass-Morphism Consolidation, Photography Gaps Closed (2026-09-02)

Started from a homepage-only background-image experiment the user had tried by hand (four generic
low-res abstract gradient stock images, none brand-related); scope grew to a full audit-and-fix pass
across every public page's CSS after discussing it together.

- **New image-generation pipeline, built ad hoc this session, not yet a committed project asset**:
  Gemini API (`gemini-3.1-flash-image`, confirmed current model against Google's own docs, up to
  4K/16:9), a billed key, a PowerShell REST-call script, and a scratch Node/sharp WebP conversion step
  — output copied straight into `wwwroot/images/`. This is the same shape of workflow Tier 1 item 1
  above already specifies for the services-image batch; this session's images are a different set
  (page-level hero/background photos, not per-service/per-area), so no naming collision, but the
  pipeline itself could be reused for that batch later.
- **Recurring Gemini gotcha, hit twice this session and worth remembering**: unprompted, the model
  fabricated readable brand text twice — "HandyFix" signage on a generated service van, and a wholly
  unrelated "CITY PLUMBING SERVICES" embroidered on a generated polo shirt. Matches the exact failure
  mode already logged in Tier 1 item 1 above for the original 16 keeper images. Fix: explicit "no
  text/no logo/no lettering of any kind" in the prompt, then visually verify before use — a milder
  negative-prompt phrase was not enough on the first pass either time.
- **`layout.css`'s sitewide `body` background** (attachment `fixed` dropped for performance) was the
  single highest-leverage change: nearly every glass-card on the site (Contact form, FAQ accordions,
  Reviews sidebar, Booking/Confirmed, auth pages) is translucent over this image, so replacing the old
  mismatched abstract gradient with one subtle on-brand texture upgraded roughly eight pages at once.
- **Glassmorphism consolidated onto `--glass-bg`/`--glass-border` tokens** — `navbar.css`'s
  `.mobile-nav-dropdown` hardcoded its own `rgba(255,255,255,0.75)` instead of the token;
  `.testimonial-card`'s distinct recipe (sits on a dark section, needs a much lighter wash) is kept but
  now commented as deliberate, not drift.
- **Opaque-vs-glass card rule enforced**: `pricing.css` already documented glass-card as "reserved for
  hero/overlay contexts" (line 184) but `Services/Pricing.cshtml`'s rate cards used it anyway — fixed
  to the opaque recipe matching `.pricing-card`/`.info-bento-card`.
- **Auth pages (`auth.css`) brought into the system** — `.auth-card` converted from a flat opaque card
  on a flat background to the same glass-over-texture treatment as Contact/FAQ/Reviews, per explicit
  user confirmation.
- **`Home/CookiePolicy.cshtml`** had zero shared chrome (bare `<div>`, no breadcrumb, no hero) — the
  only page in the whole site like this; now uses the same `.services-header`/`.info-canvas`/
  `.info-card` shell as Terms/Privacy.
- **`Home/Reviews.cshtml`** hero normalized to `.text-center`, matching the other content pages
  (Contact/FAQ/Terms/Privacy) — it had been left off that pattern with no documented reason.
- **Homepage**: reverted the `.division-card-content` background-image experiment (dark text over a
  busy photo — confirmed illegible before shipping), added a light glass scrim behind `.bento-header`
  (previously had none, unlike `.cta-card` which already did), applied a new wide on-brand photo to
  `.final-cta`/`.cta-card`/`.popular-services-section`.
- **`Services/Index.cshtml` and `Areas/Index.cshtml`** — both had zero photography (confirmed against
  the code, not assumed); given the `.service-hero-section` image-hero pattern already proven on
  `Services/Details`/`Areas/Details`, both now reuse it rather than inventing a new one.
- **Real bug fixed**: `Services/Details.cshtml:161`'s "Local Expert" avatar was hardcoding a live
  external Google placeholder SVG (`gstatic.com/labs-code/stitch/...`) — replaced with two generated,
  on-brand, generic professional headshots (`expert-david.webp`/`expert-mark.webp`, matched to the
  existing `isPlumbing` persona branch), plus an `onerror` fallback matching the Category/Areas-Details
  convention this file was missing.
- **CSS typo fixed**: `home.css`'s `.cta-card-bg` had `rgba(255x, 255, 255, 0.65)` — an invalid value
  silently dropped by the browser, so the white/blur scrim behind the final-CTA text was never actually
  rendering. Now `rgba(255, 255, 255, 0.65)`.
- **New assets** (all logo/text-free, verified — see the Gemini gotcha note above): `bg-texture-body.webp`,
  `bg-cta-wide.webp`, `services-hero.webp`, `areas-hero.webp`, `expert-david.webp`, `expert-mark.webp`.
  **Removed**: the four discarded `bg-image-1..4.webp` abstracts.
- **Verified in-browser**, not just compiled: `dotnet build` clean, then Playwright screenshots
  (headless Chromium, 1440px) of every touched page, plus a scrolled homepage capture (the site's own
  `IntersectionObserver` fade-in otherwise makes a plain full-page screenshot look broken — a
  screenshot artifact, not a bug). No new console/network errors introduced; one pre-existing unrelated
  404 noted (`Contact` page's `jquery.validate.unobtrusive.min.js`), out of scope here.
- **Not done, flagged to the user, not started unprompted**: the site's visible brand text is still
  "HandyFix"/"Handy Fix" everywhere (navbar, footer, hero copy, JSON-LD `name`/`@id`/`url`/`sameAs`) —
  stale as of Section 3ah's rebrand decision the day before ("Plumbing Handyman Surrey"). Out of scope
  for this pass — a full rename touches far more than the CSS/imagery here, and Tier 2/3 items feeding
  the correct NAP data (real phone, real address, wordmark) haven't landed yet either — but worth its
  own pass. None of this session's new image assets carry any old wordmark, so nothing here needs
  rework once the rename happens.

---

## 3aj. Homepage Popular Services Mobile UX Redesign: Strategy 4 (2×2 Compact Tile Grid) (2026-09-02)

- **Problem identified**: On mobile view (< 768px), the Popular Services section previously collapsed from the desktop 3-column asymmetrical Bento grid into a single vertical stack of 4 massive 240px image cards. It occupied ~1,000px of scrolling space with low information density (only service name and starting price were shown), while richer properties already loaded on `ServiceViewModel` (`CategoryName`, `EstimatedDurationMinutes`, `Description`) were unused. The "See all services" link was also hidden on mobile (`display: none` below 768px).
- **Exploration & Strategy Evaluation**: Four distinct mobile UX strategies were formulated, implemented, and reviewed live by the user:
  1. *The Rich Bento*: Full photographic cards with category badge, duration pill, clamped 2-line description, and "Book →" action pill.
  2. *Mobile App Split Row*: 120px horizontal split cards (photo thumbnail left, structured surface card right, TaskRabbit pattern).
  3. *Horizontal Snap Carousel*: Native CSS touch-snap carousel (`scroll-snap-type: x mandatory`), cards sized to 82vw to peek from the edge.
  4. *2×2 Compact Tile Grid*: Two-column by two-row compact tile grid (~175px tall per card) displaying all 4 services at a glance on a single screen without scrolling or swiping.
- **Winner selected**: Strategy 4 ("2×2 Compact Tile Grid") was selected as the final implementation.
  - All 4 popular services are immediately visible on a mobile viewport with category badges, duration pills, bold titles, 2-line descriptions, flat-rate pricing, and booking buttons.
  - Added full-width `.bento-mobile-footer` button ("View All Services →") styled with high-contrast primary navy background, white text, and `.btn-outline-secondary` utility class added to `buttons.css`.
  - Desktop view (≥ 768px) remains completely untouched as the standard 3-column asymmetrical Bento grid.
- **Mobile Header & Navigation Polish**:
  - *Sticky Header*: Fixed mobile stickiness by moving `html, body` from `overflow-x: hidden` to `overflow-x: clip;` in `reset.css` (preventing mobile browsers from creating a scroll clipping context that kills sticky positioning), and set `.header-docked` to `position: -webkit-sticky; position: sticky; top: 0; z-index: 1020;`.
  - *Mock Phone Number*: Added interactive tap-to-call button (`07123 456789`, `tel:07123456789`) in `.navbar-actions` on the mobile header bar, plus a prominent call banner inside `.mobile-nav-dropdown`.
  - *Opaque Menu Gradient*: Replaced the low-opacity glassmorphism background (`rgba(255, 255, 255, 0.65)`) on `.mobile-nav-dropdown` with a 98% opaque branded gradient (ice blue to soft coral to clean slate) with 24px backdrop blur and `#0f172a` semibold link typography, ensuring underlying page text cannot bleed through.
- **Desktop Popular Services Contrast Polish**:
  - Upgraded `.bento-card-desc` on desktop from translucent `rgba(255, 255, 255, 0.85)` at `13px` to pure `#ffffff` at `14px` with `font-weight: 500` and `text-shadow: 0 1px 3px rgba(0, 0, 0, 0.85)`.
  - Added a dedicated vertical gradient scrim directly behind `.bento-card-content` on desktop, guaranteeing high contrast over light photo details.
- **Specialised Divisions Card Theming**:
  - Styled Plumbing & Heating card content with a soft, airy sky-blue gradient (`#e0f2fe` to `#f8fafc`), sky border (`#bae6fd`), and cyan hover elevation glow.
  - Styled General Handyman card content with an elegant blush/coral gradient (`#fff1f2` to `#f8fafc`), coral border (`#fecdd3`), and themed rose icons, checkmarks, and link (`#e11d48`).
- **Verification**: Full test suite passed (150/150 green — 95 `Services.Data.Tests` + 55 `Web.Tests`).

---

## 3ak. Service Catalog Refresh: Flat Hourly Pricing + 8 New Services (2026-09-08)

- **Context**: user-driven market research (AI-assisted, cross-checked live against Aspect.co.uk, Fantastic Handyman, Silver Saints, and Handy Squad's own published rate cards) plus a pricing decision agreed with the business's co-owner (Zapryan). Two decisions came out of it: which services were genuinely missing from the catalog vs. already covered inside a broader bundle, and a move from 20 individually-judged flat prices to a uniform hourly-rate model.
- **Pricing model changed**: every `Service.BasePrice` now derives from **£80/hr (Plumbing) or £60/hr (Handyman), 1-hour minimum**, ~~with a small number of deliberately-larger jobs (Pipe Repairs, Radiator & TRV Replacement, Outside Garden Tap Installation at £120; Kitchen Plumbing £150; Bathroom Plumbing £160; Shower Installation £240; Furniture Assembly £90 and Gutter Clearing £90; Painting Touch-Ups £120; Property Maintenance £180) priced above the floor by judgment, not by a minutes-based formula~~ — **superseded later the same day, see Section 3al: those 10 were flattened to the uniform £80/£60 rate too, so there are no exceptions left.** `EstimatedDurationMinutes` was confirmed (by reading `ServicesService`/booking code, not assumption) to have **zero functional wiring** anywhere: not slot generation, not price calculation, purely a displayed number. Deriving price from it would have been false precision, so duration and price are now treated as fully independent.
- **`ServicesSeeder` behavior changed from insert-only to upsert-on-price/duration.** Previously a service already found by `Name` was left untouched forever, so editing the seeder's numbers had no effect on an already-seeded database (`handyfix_staging` included). It now updates `BasePrice`/`EstimatedDurationMinutes` on every startup for a matched row, leaving `Slug`/`Name`/`Description`/`CategoryId` alone. *(Since Section 3aw it syncs `CategoryId` too — leaving it alone meant a category move in the seeder never reached an already-seeded database.)* This is intentionally scoped to pre-launch: `handyfix_prod` is still empty, so there is no live admin-made price edit yet that this could clobber. **Revisit before production go-live** — once real admin price edits can exist, an unconditional reseed-on-startup will silently overwrite them.
- **8 new services added** (20 → 28): Plumbing gained Washing Machine & Dishwasher Install, Radiator & TRV Replacement, Outside Garden Tap Installation, Silicone & Mastic Resealing, Bath & Shower Screen Fitting; Handyman gained Door Trimming & Shaving, Lock & Handle Replacement, Gutter Clearing. Each was cross-checked against the *existing* service descriptions first — most of what first looked like a market gap (appliance hookup, radiator/valve work, resealing, lock/handle work, gutter clearing) was already promised inside a broader bundle's description text, just never sold as its own line item.
- **Parent bundle descriptions tightened** to remove the now-duplicated wording: `kitchen-plumbing` no longer mentions washing machines/dishwashers, `general-plumbing-maintenance` no longer mentions radiator bleeding/valve replacement, `door-repairs` no longer mentions handles/locks, `minor-home-repairs` no longer mentions silicone sealant, `property-maintenance` no longer mentions gutter clearing/lock changes — otherwise two different bookable services would have claimed the same job.
- **Side effect, not yet verified live**: `CategoryViewModel.BasePrice` (Section 5's `Min(Services.BasePrice)` mapping, shown on `/Pricing` as "Our Hourly Rates") should now resolve to exactly £80/£60 for both categories, since every service in each category has a floor at that value — this should also close the pre-existing gap where that page's "1-hour minimum, additional time billed at the same rate" copy didn't match any real per-service price. Not re-verified with a live app run this pass.
- **Known gap**: the 8 new services have no hero image yet. `ServicesSeeder`'s `ServiceImage` back-fill (line ~90) will create a DB row pointing at `/images/services/{slug}-hero.webp` for each regardless of whether that file exists on disk — expect broken images on the new services' cards/detail pages until the AI image-generation pipeline (Section 4 Tier 1 item 1) is run for these 8 slugs.
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors, pre-existing warnings only) and full test suite (95 `Services.Data.Tests` + 55 `Web.Tests`, 150/150 green) — no test hard-codes the old seeded prices. Not yet verified with a live app run against a freshly-seeded database.

---

## 3al. Category Card Price Display Fix and Flat-Rate Pricing Finalized (2026-09-08)

Same-day follow-on to Section 3ak, driven by a user request to fix the Services/Category card's
price copy and layout, which surfaced a pricing-model question along the way.

- **Category card copy**: `Services/Category.cshtml`'s per-service price changed from "From £X" /
  a plain "Fixed Rate" label to "Base Price £X" / a "See Pricing" link to the `Pricing` route
  (`asp-controller="Services" asp-action="Pricing"`) — the previously-inert label text is now real
  navigation to the full rate breakdown.
- **No-wrap layout fix**: `.service-card-price-box` now has `flex-shrink: 0` and
  `.service-card-price` has `white-space: nowrap` (`service-category.css`), so the price stays on
  one line regardless of service-name length instead of shrinking/wrapping alongside a long title.
  Verified against "Washing Machine & Dishwasher Install" (the longest name in the catalog) at
  1440px — the title wraps to two lines as expected, the price does not. Only checked at desktop
  width this pass; headless-Chrome narrow-viewport captures are known-unreliable on this Windows
  environment (Section 3l), so mobile wasn't independently re-verified — but the rule carries no
  width-gating media query, so no different behavior is expected there.
- **Flat-rate pricing finalized, superseding Section 3ak's tiered model**: the 10 services Section
  3ak priced above the £80/£60 floor by judgment (Pipe Repairs, Radiator & TRV Replacement, Outside
  Garden Tap Installation, Kitchen Plumbing, Bathroom Plumbing, Shower Installation, Furniture
  Assembly, Gutter Clearing, Painting Touch-Ups, Property Maintenance) are now priced at the flat
  category rate too — every Plumbing service is £80, every Handyman service £60, no exceptions.
  `EstimatedDurationMinutes` is unchanged for all of them (still shown as "Approx. N Hours" on cards
  and details) — only `Service.BasePrice` moved.
  - **This reverses a same-day business decision made with Zapryan (Section 3ak), and was
    explicitly confirmed with the user before proceeding** — it was not an inferred engineering
    call. The initial request ("don't calculate the price for services over 1 hour, just leave the
    base rate for all services") assumed the tiered prices were an accidental duration-scaling bug;
    reading Section 3ak while drafting this entry showed every one of the 10 values matched
    Zapryan's judgment-based figures exactly, i.e. they were intentional. Flagged back to the user
    before editing `PROJECT_STATE.md` or committing; the user confirmed flat pricing is the current
    intended decision, superseding the tiered one from earlier the same day.
- **Downstream effect, no code change needed**: `CategoryViewModel.BasePrice`
  (`Min(Services.BasePrice)` per category, Section 1) and `BookingsService.CreateBookingAsync`'s
  `totalAmount` (`Sum` of selected services' `BasePrice`) both already read `Service.BasePrice`
  directly, so both now consistently reflect the flat rate.
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors, pre-existing warnings only) and the full
  suite, 95 `Services.Data.Tests` + 55 `Web.Tests`, 150/150 green — no test hard-coded the old
  tiered prices. Live app run (existing `dev`-branch dev server, restarted mid-session to pick up
  the reseeded prices) + headless Chrome screenshots of `/Services/plumbing` confirming both the
  new copy and the no-wrap fix render correctly.

---

## 3am. Small Building & Refurbishments Category & Quote-Based Routing (2026-09-08)

Completed Roadmap Tier 2 item 12 (`docs/private/VISION_AND_CONTEXT.md` Section 5.13): established the new "Small Building & Refurbishments" category (`small-building-works`) and 8 services, scope and pricing confirmed with Zaprqn. The booking mechanism question flagged 2026-08-03 is now resolved: quote/survey flow, not slot-based instant booking.

- **8 new catalog services seeded**:
  1. `full-bathroom-refurbishment`: Full Bathroom Refurbishment (demolition, plumbing, tanking, tiling, sanitaryware fit-out).
  2. `kitchen-fitting-alterations`: Kitchen Fitting & Alterations (unit assembly, worktops, splashbacks, sink & appliance integration).
  3. `partition-walls-drylining`: Partition Walls & Stud Work (timber/metal studs, acoustic insulation, drywall, room reconfigurations).
  4. `plastering-ceiling-repairs`: Plastering & Ceiling Repairs (multi-finish skimming, water damage ceiling patches, coving).
  5. `wall-floor-tiling`: Wall & Floor Tiling (porcelain, ceramic, natural stone, metro tiles for bathrooms, kitchens, hallways).
  6. `flooring-installation`: Flooring Installation (LVT, laminate, engineered wood with underlay and trims).
  7. `external-brickwork-paving`: External Brickwork & Paving (mortar repointing, garden walls, patio repairs, steps, shed bases).
  8. `custom-carpentry-boxing-in`: Custom Carpentry & Boxing-In (bespoke alcove units, custom shelving, boiler/pipework boxing).
  - **Deliberately priced outside the Section 3al flat-rate rule**: these are per-job estimates (£280-£650), not the £80/£60 hourly floor — the flat-rate decision applies to the two hourly-billed categories only, since a multi-day refurbishment doesn't fit a per-hour figure. `CategoryViewModel.BasePrice` (`Min` per category) is intentionally not "no exceptions" across *all* categories, only within Plumbing and Handyman.
- **Quote-first routing**: multi-day/quoted projects bypass the standard 1-hour `AvailabilitySlot` instant booking entirely.
  - On `Category.cshtml`, `Details.cshtml`, and `Services/Index.cshtml`, the action button switches from "Book Slot" to "Request Quote" / "Request Free Estimate", routing to `/Contact?service=@svc.Name`.
  - `HomeController.Contact(string service = null)` pre-populates the enquiry message with the requested service name.
  - `Pricing.cshtml`'s "Our Hourly Rates" cards show "From £X" / "Quoted after free survey" for this category instead of "£X per hour" / "Minimum charge 1 hour", and the page's intro copy and pricing-model note box both call out the split explicitly rather than implying every job is hourly.
  - Sidebar CTA changes from instant calendar booking to project survey consultation.
- **Home page 3-division bento**: `Home/Index.cshtml` gained a 3rd division card ("Building & Refurb"); `home.css` moved to a 3-column desktop grid (`min-width: 1024px`) with a warm amber/stone theme (`.division-card-building`).
- **No fabricated technician bio added**: `Details.cshtml`'s "Local Expert" block (the existing hardcoded "David"/"Mark" personas already flagged for removal in Roadmap Tier 3 item 17) is not shown at all for this category — an initial draft of this feature invented a third persona ("Zap", 15 years experience) with a bio/photo mismatch bug (no avatar branch, so it rendered Mark's photo); caught in review and removed rather than fixed, since inventing a specialist here would be the same anti-pattern Tier 3 item 17 already exists to undo, not something to extend to a third category.
- **Known gap, unchanged from Section 3ak's pattern**: the 8 new services and the new division-card hero image have no real artwork yet; `onerror` falls back to `/images/hero.webp`.
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors, pre-existing warnings only) and the full suite, 95 `Services.Data.Tests` + 55 `Web.Tests`, 150/150 green, after both the initial category work and the technician/pricing-page fixes above. Not yet verified with a live app run.

---

## 3an. Plumbing Hourly Rate Raised to £90 (2026-09-08)

- **`ServicesSeeder`**: all 15 Plumbing services moved from `Price = 80.00m` to `90.00m`, flat rate, no exceptions — same rule as Section 3al, new floor. Handyman (£60) and Small Building & Refurbishments (per-job quotes) are unaffected.
- Because the seeder upserts by service name on every app startup (Section 3ak), this takes effect on the dev/staging DB on next run with no migration. `/Pricing`'s "Our Hourly Rates" card for Plumbing (`CategoryViewModel.BasePrice`, `Min` per category) will show £90 accordingly.
- Not yet verified with a build, test run, or live app run.

---

## 3ao. Surrey-First Rebrand, Croydon/Mitcham Purge, and Small-Building Pricing/Insurance Copy Fixes (2026-09-08)

Business decision: the catalog area (Section 3ak-3am's Chessington/Surrey coverage list) never included Croydon or Mitcham, but ad-hoc marketing copy, meta descriptions, and JSON-LD structured data across the site still named them alongside other non-Surrey boroughs (Bromley, Kent, generic "South London"). Reworked every visible and structured mention to lead with Surrey and feature the affluent towns already established in `ServiceAreasSeeder` (Chessington, Cobham, Esher, Weybridge, Guildford, Wimbledon, Epsom, Kingston) instead.

- **Surrey now leads South London** in every hero title, `<title>`/meta description, and JSON-LD `PostalAddress`/`areaServed` block: `Home/Index.cshtml`, `Home/About.cshtml`, `Services/Index.cshtml`, `Services/Category.cshtml`, `Services/Details.cshtml`, `HomeController`, `ServicesController`. Confirmed with the user as "Surrey first" over the alternative of just appending "and Surrey" to existing South-London-first copy.
- **Croydon/Mitcham removed everywhere** — including `About.cshtml`'s entire "Rooted in Croydon" origin narrative (now "Rooted in Chessington", matching Section 3ak's "Chessington is where it all starts for us" framing already established for the Areas feature) and the two `Details.cshtml` "Local Expert" bios (`expertBorough`/`expertBio` for David/Mark), which now reference Epsom/Cobham/Esher instead of Sutton/Croydon.
- **Fake LocalBusiness JSON-LD address updated to Chessington**, per explicit user confirmation (previously deliberately left alone per Section 3's Sprint 2 note — this supersedes that, at least for locality/region/geo, since Croydon could no longer stand and Chessington is the one real anchor point already in the codebase): `Home/Index.cshtml`'s `PostalAddress` (`Croydon`/`CR0 1XX` → `Chessington`/`KT9 1AA`, `addressRegion` `London` → `Surrey`) and `geo` coordinates (moved from Croydon's to Chessington's). `Details.cshtml`'s `Service` schema address and `areaServed` list updated the same way. `telephone`, `sameAs`, and the invented review/job-count markup were not touched — still tracked as fake NAP under Tier 3 item 18.
- **Insurance copy genericized**: the specific "£5M public liability insurance" figure (`Home/Index.cshtml` trust card, `Details.cshtml` FAQ answer and sidebar trust item) is now "Comprehensive public liability insurance" everywhere — the real cover is higher and the business does not want the figure public. No number is quoted anywhere now.
- **`Bath & Shower Screen Fitting` and `Silicone & Mastic Resealing` moved from Plumbing to Handyman** in `ServicesSeeder` (`CategoryId` changed, `BasePrice` dropped from £90 to the flat £60 Handyman rate per the Section 3al/3am no-exceptions rule — confirmed with the user rather than assumed).
- **Small Building & Refurbishments pricing display made consistent with the rest of the catalog**: `Category.cshtml`'s and `Services/Index.cshtml`'s service cards now show "Survey & Fixed Quote" with a working `See Pricing` link to the `Pricing` route for this category, instead of a bare "From £X" price with a non-clickable label. `Details.cshtml`'s "How much does X cost?" FAQ (both the visible accordion and its JSON-LD `FAQPage` twin) is now conditional on `isBuilding`: building services describe the free-survey/fixed-quote process instead of stating a per-hour base rate that never applied to them.
- **Home page final CTA**: text now reads "Surrey and South London residents"; fixed a `.final-cta`/`.cta-card` bug in `home.css` where the outer section and the inner rounded card both rendered the identical `bg-cta-wide.webp` background — since only the card had `border-radius`/`overflow: hidden`, the sharp, un-clipped copy of the same photo was visible immediately outside the card's rounded corners. Removed the duplicate background from `.final-cta`; only the card renders the photo now.
- **Local Expert gap for Small Building & Refurbishments**: resolved same day once the user supplied real bio/photo content — see Section 3ap.
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors, pre-existing warnings only) after this section's changes. Test suite and live app run not yet done — the user asked for the full batch first, before testing.

---

## 3ap. Real "Local Expert" Added for Small Building & Refurbishments: Zap (2026-09-08)

Closes the gap Section 3am deliberately left open (no fabricated persona for this category) and Section 3ao's "known gap" note, now that the user supplied real content for the business's actual co-founder rather than an invented one.

- **`Details.cshtml`'s "Local Expert" block now renders for all three categories.** The `!isBuilding` guard around the block was removed; `expertName`/`expertRole`/`expertBorough`/`expertBio`/`expertAvatar` gained a `isBuilding` branch (Zap: "Projects Director", "Surrey", bio and closing line supplied verbatim by the user — his own words about being in construction since 2004, personally surveying every project and working alongside the on-site teams to completion). The block's closing sentence is `isBuilding`-conditional (`expertClosingLine`) since the generic "property maintenance" wording used for David/Mark didn't fit a multi-day refurbishment project.
- **Real photo, not a placeholder**: the user's supplied `zap-photo.avif` (AI-generated headshot, 425×650) had to be converted — GDI+ (`System.Drawing`) cannot decode AVIF at all ("Out of memory" is its generic unrecognized-format error), but Windows' WIC codec stack could (this machine has an AVIF codec extension installed), so a PowerShell script decoded it via `System.Windows.Media.Imaging.BitmapDecoder` to PNG, then a throwaway console app referencing this repo's own `SkiaSharp` 2.88.9 (already a dependency, used identically in `ImageStorageService`) resized it to 320px wide and re-encoded as WEBP at quality 80 — matching both the format and quality setting every other site image already uses. Saved as `wwwroot/images/expert-zap.webp` (42.5 KB), same naming convention as `expert-david.webp`/`expert-mark.webp`. The existing `onerror` fallback to `/images/hero.webp` was left in place unchanged.
- **The `4.9/5 Rating` / `Over 1,200 services completed` stats line was left as-is** for all three experts — it reads as company-wide social proof rather than a personal claim, and auditing/removing it is Tier 3 item 17's job, not in scope here. *(Removed in Section 3av, once the block showed a real person's name on every service page.)*
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors, pre-existing warnings only). Test suite and live app run not yet done, per the user's "batch everything, test at the end" instruction for this whole session.

---

## 3aq. Navbar Rework: Category Links, Mobile Scroll Fix, Login Greeting Removed (2026-09-08)

- **`_Navbar.cshtml`**: the single "Services" link (desktop and mobile) replaced with three direct category links — Plumbing, Handyman, Small Building Work — routed via `asp-route="ServiceCategory"` to `/Services/{plumbing|handyman|small-building-works}`. The `/Services` overview page itself is unchanged and still reachable from elsewhere (e.g. the homepage's "View All Services" button); it's just no longer in the top nav.
- **Mobile menu scroll bug fixed**: `.mobile-nav-dropdown` had no `max-height`/`overflow-y`, so once its content (now longer, with 3 links instead of 1) exceeded the screen height, the extra items extended past the viewport with nothing to scroll — the page behind the menu could still scroll instead. Added `max-height: calc(100dvh - 84px - var(--spacing-4))` with `overflow-y: auto` and `overscroll-behavior: contain` (`navbar.css`), plus a `mobile-nav-open` class toggled on `<body>` from `site.js` alongside the existing `is-open` toggle, with `body.mobile-nav-open { overflow: hidden; }` locking the page behind it while the menu is open. **This lock caused a regression, fixed in Section 3av**: `overflow` on `<body>` makes it a scroll container, so the `position: sticky` header pinned to the top of the document instead of the viewport — opening the menu while scrolled down left both off-screen.
- **`_LoginPartial.cshtml`**: removed the "Hello, {email}!" greeting link for signed-in users — its length scaled with the user's email and was pushing the desktop nav (now carrying 3 extra category links) to wrap onto a second row. Admin link and Logout button untouched; the account-management page itself (`/Account/Manage`) still exists, just no longer linked from the nav.
- Not yet verified with a build/test run at the time — see Section 3ar, where both were run together.

---

## 3ar. Small Building & Refurbishments: Site-Wide Hardcoded-Price Audit (2026-09-08)

Prompted by a user request to replace the Details-page "Starting Price: From £X" spec card for this category with a proper "Pricing Model" card, which led to auditing every other place `Service.BasePrice` gets rendered — since this category's whole pricing model is survey-then-quote, not a flat number, and the user does not want any fixed sum implied for it anywhere on the site.

- **`Details.cshtml` spec-bento card**: for `isBuilding`, the "Starting Price / From £X" card is now "Pricing Model / Free Survey & Fixed Quote" with a `request_quote` icon; non-building services are unchanged.
- **Found and fixed 7 more places quietly asserting a fixed building price**, none of which had been caught by the earlier Section 3ao pass (that pass fixed the *visible* per-service FAQ and category-card price, not these):
  1. `Pricing.cshtml`'s "Our Hourly Rates" card showed "From £280" for the category — now "Survey & Fixed Quote" (new `.rate-card-price-quote` modifier in `pricing.css` shrinks the font, since that slot was sized for a short "£X" figure).
  2. `_PricingCard.cshtml` (the shared partial behind "Typical Job Costs" on `Pricing.cshtml` and "You Might Also Need" on `Details.cshtml`) always rendered "From £X" and a minutes/hours duration with no awareness of category — added `PricingCardViewModel.IsQuoteBased`, set at both call sites (`related.CategorySlug`/`svc.CategorySlug == "small-building-works"`), so a building service's card now reads "Survey & Fixed Quote" / "Day Rate / Survey" instead. This is what made **a building service's own Details page show *other* building services with a hardcoded price** in "You Might Also Need" — the most visible instance of the bug.
  3. `Details.cshtml`'s JSON-LD `Service` schema unconditionally emitted a numeric `Offer.price` — now omitted entirely for building services (no invented price for structured data/search results either), emitted as before for Plumbing/Handyman.
  4. `Home/Index.cshtml`'s hero booking-widget category `<select>` showed "Small Building & Refurbishments (from £280/hr)" — wrong on both the number and calling a day-rate/quote job "hourly". Now shows just the category name for building.
  5. `Home/Index.cshtml`'s "Popular Services" bento had no category guard on its "From £X" — not currently reachable (the homepage only ever surfaces the first 4 services, which are always Plumbing today) but one seed-order or admin-panel change away from showing a raw building price. Guarded defensively the same way.
  6. `Booking/Index.cshtml`'s service `<select>` listed building services with "(from £X/hr)" even though the page's own category radio buttons only offer Plumbing/Handyman (building is quote-first per Section 3am and was never meant to reach this instant-booking wizard at all) — now filtered out of the dropdown entirely rather than just re-worded.
  7. `ServicesController.Details()`'s meta description hardcoded "transparent pricing starting from £X. Book a slot now." for every service — now branches on category: building services get "Free on-site survey and a fixed quote before any work begins. Request your quote today." with no number. Also fixed a leftover "HandyFix London" / "in South London" in this same action that Section 3ao's rebrand pass had missed.
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors, pre-existing warnings only) and the full suite, 95 `Services.Data.Tests` + 55 `Web.Tests`, 150/150 green — covers this section and Section 3aq together. Live app run not yet done.

---

## 3as. Mobile Sticky CTA Stripped to a Bare Floating Button (2026-09-08)

Found and fixed on live staging testing: the mobile sticky CTA bar (`_MobileStickyCta.cshtml`) still showed a hardcoded "Estimated Rate £55/hr" that matched neither the current £90 Plumbing nor £60 Handyman rate, alongside the "Book Now" button.

- **Rate label/value removed entirely** — not reworded, since a single flat figure can't represent two different category rates (and never should have, since it predates the flat-rate-per-category model from Section 3al).
- **`.mobile-cta-bar`'s glass background, blur, top border, and shadow removed** per explicit user request — the bar itself is now invisible; only the "Book Now" button renders, right-aligned in the same position it always occupied (`justify-content: flex-end`, not centered or full-width).
- **`pointer-events: none` added to `.mobile-cta-bar`, `auto` back on `.btn-mobile-cta`** — needed once the bar had no visible background, since an invisible `position: fixed` element spanning the full width would otherwise silently swallow taps across the bottom of every page.
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors, pre-existing warnings only) and the full suite, 150/150 green.

---

## 3at. Home Page Trust Stats Card Replaced with a "Join Our Team" Invitation (2026-09-08)

- **`Home/Index.cshtml`'s floating stats card** on the "Trust Your Home to Qualified Experts" section (overlaid on the hero image, `.trust-stats-card`) — previously "12k+ Jobs Completed" / a 5-star row / "4.9/5 Rating, Based on 2,500 reviews" — is now a recruitment invitation: a `groups` icon, "Join Our Team", and a one-line pitch, with the whole card now a link to `Home/Contact` (no dedicated careers page or address exists, so the general enquiry form is the only real destination today). *(Superseded by Section 3av: the card now links to a dedicated `/JoinOurTeam` application form.)*
- **Removes fabricated numbers, not just repurposes the slot**: those job-count/rating figures were invented and never sourced from real data — the same category of content already flagged elsewhere (Tier 3 item 17/18) as needing real business input rather than engineering. This instance is fixed by removing the number entirely rather than being left to revisit later.
- **CSS**: `.trust-stats-rating` and `.trust-stats-stars` (both now unused) removed from `home.css`; the card gained `.join-team-card`/`.join-team-icon-col`/`.join-team-icon`/`.join-team-arrow` for the link-hover affordance (slight lift + arrow shift), reusing the existing `.trust-stats-card`/`.trust-stats-divider`/`.trust-stats-col` layout shell unchanged.
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors, pre-existing warnings only) and the full suite, 150/150 green. No test hard-coded the old stats text.

---

## 3au. About Page Redesign: Variation 3 (Property Solutions Hub) Finalized (2026-09-10)

The existing `/About` page was identified as sparse and generic (a single 1:1 image, two brief paragraphs, and legacy placeholder claims such as "5,000+ Successful Fixes" and "15+ Specialist Techs" that conflicted with honest trust signals). To determine the optimal direction, three distinct design variations were built alongside the preserved baseline and tested via an interactive preview switcher toolbar:

- **Variation 1**: The Local Craftsman & Founder Story (authentic founder focus, agency-vs-direct comparison card).
- **Variation 2**: Precision & Standards Blueprint (4-step workflow timeline, equipment & tidy finish standards).
- **Variation 3**: Complete Property Solutions Hub (multi-trade showcase across Plumbing, Handyman, and Small Building & Refurbishments).

**Resolution**: Following testing, **Variation 3 (Complete Property Solutions Hub)** was selected as the winner and finalized as the permanent About page. All temporary switcher scaffolding, experimental partials, and unused CSS rules were removed:
- **`About.cshtml`**: Inlined with Variation 3 directly, eliminating all partial views and switcher logic. Features dedicated division cards (*Precision Plumbing* at £90/hr, *Handyman & Maintenance* at £60/hr, *Small Building & Refurbishments* with bespoke estimates), "Who We Work With" cards (Homeowners, Landlords, Property Managers), and Chessington coverage pills.
- **`HomeController.About()`**: Reverted to clean parameterless signature with updated title (`About Us - Plumbing Handyman Surrey`) and meta description.
- **Scaffolding cleaned up**: `_AboutOriginal.cshtml`, `_AboutSwitcher.cshtml`, `_AboutV1.cshtml`, `_AboutV2.cshtml`, and `_AboutV3.cshtml` deleted. Unused Var 1/Var 2/switcher CSS removed from `pages-info.css`.
- **`HomeControllerTests.cs`**: Unit tests updated to verify parameterless `About()` action returns expected `ViewResult` with correct metadata.

### Verified
- `dotnet build src/HandyFix.sln` — 0 errors, clean build.
- Full test suite: 151/151 passed (95 in `HandyFix.Services.Data.Tests` + 56 in `HandyFix.Web.Tests`).
- Visual check: `/About` renders Variation 3 cleanly without toolbar.

**Pre-commit review fixes**: the three section eyebrow labels ("Full-Service Trade Depth", "Tailored Care", "Local Geographic Base") used a `letter-spacing-1` class that doesn't exist anywhere in the codebase — a silent no-op — swapped for the existing `tracking-wide` utility. `pages-info.css` was also missing its trailing newline. Two judgment calls were surfaced to and resolved by the user rather than decided unilaterally: the new division cards' "Fixed Rate • £90/hr" / "£60/hr" badges are hardcoded text rather than pulled from `Service.BasePrice` (kept hardcoded, deliberately, as simple marketing copy — not wired live) and the page's use of "Plumbing Handyman Surrey" while the rest of the site still says "Handy Fix" (kept as-is; the full site rename is separate, later work).

---

## 3av. Mobile UX Fixes, Join Our Team Form, and Pricing Consistency Pass (2026-09-10)

A nine-item mobile QA epic, plus related pricing contradictions found while auditing it. Scope decisions confirmed with the user before implementation: applications reuse the enquiry pipeline rather than a new entity; one Zapryan bio with a per-category role line; the compact card layout applies to all three category pages; and three extra pricing conflicts (below) fixed in the same batch.

- **Mobile menu opened off-screen — a regression from Section 3aq.** The scroll lock `body.mobile-nav-open { overflow: hidden; }` turns `<body>` into a scroll container, and a `position: sticky` element pins to its nearest scroll container — so the header snapped to the top of the *document*, off-screen for anyone scrolled down. Fixed by making `.header-docked` `position: fixed` below 768px (desktop keeps `sticky`); fixed positioning resolves against the viewport, which no ancestor's `overflow` can change, so the scroll lock stays. A fixed header leaves the flow, so `body:has(> .header-docked)` is padded by `--header-height`, which `site.js` publishes from a `ResizeObserver` (57px measured; the CSS fallback is also 57px for a JS-off visit). **The `:has()` scoping is load-bearing**: `_AdminLayout` loads `site.css` too but renders no public header, and an unscoped rule would have put a gap at the top of every admin page on a phone. The dropdown's `max-height` now uses the same variable instead of a hardcoded 84px.
- **Local Expert is Zapryan on every service page** (`Services/Details.cshtml`): real photo (`expert-zap.webp`) and his own supplied bio and closing line, verbatim, for all three categories; only the role varies ("Owner & Lead Plumber" / "Owner & Lead Handyman" / "Projects Director"). The invented "David"/"Mark" personas are gone, along with the fabricated `4.9/5 Rating | Over 1,200 services completed` line — it would otherwise have sat under a real person's name — and its now-unused CSS. `expert-david.webp`/`expert-mark.webp` deleted (no remaining references).
- **Compact mobile category cards** (`service-category.css`, all three category pages since `Services/Category.cshtml` is one shared view): below 768px the card becomes a grid and `.service-card-content` gets `display: contents`, so its children join the card's grid — an 88px thumbnail beside title/price/description, meta line and buttons full-width below. CSS-only, so the desktop row layout cannot regress. Card height ~430px → 214–265px; the Small Building list drops from ~3,440px to 2,032px.
- **"You Might Also Need" thumbnails**: `PricingCardViewModel.ImageUrl` added; `_PricingCard.cshtml` renders a full-bleed 16:9 image (negative margins cancel the card padding, `overflow: hidden` clips to the radius) with the category icon as a badge, plus a hover lift. Because the partial is shared, `/Pricing`'s "Typical Job Costs" gets the same treatment. Services with no artwork on disk fall back to `hero.webp`, as everywhere else.
- **Dedicated Join Our Team form** at `/JoinOurTeam` (`HomeController.JoinTeam` GET/POST, `Views/Home/JoinTeam.cshtml`, `JoinTeamInputModel`): trade, years of experience, availability, own tools/transport, optional "about you". Saved through the existing `IInquiriesService.CreateInquiryAsync` — `JoinTeamInputModel.ToContactInputModel()` formats the fields into one message prefixed `[Job Application]` — so applications land in the admin Enquiries list (whose detail view already uses `pre-wrap`, so the line breaks render) with no migration and no new admin screen. Chosen over a separate `JobApplication` entity as the smaller change; revisit if application volume makes mixing them with customer enquiries a problem. `YearsExperience` is `int?` so an empty field fails `[Required]` instead of binding as 0. Linked from the home page's Join Our Team card (previously `/Contact`, see Section 3at) and the footer; added to the sitemap. No CV upload — the upload pipeline is image-only.
- **About page**: all three "Explore" buttons now use the solid `btn-secondary` style; each division card's checkmarks take its icon tile's accent (blue/green/amber); the three "Who We Work With" icons get distinct accents. `.guarantee-icon-box` colours became CSS variables with the original cyan as fallback, because the Booking page sidebar uses the same class.
- **"Book This Service" CTA** (Plumbing/Handyman only; the building branch is already quote-first): Request Your Booking → `/Contact?service=…` (reuses the existing message pre-fill), Book a Slot → the booking wizard, Call Us → `tel:` (still the Tier 3 item 18 placeholder number). The note underneath said "No upfront payment required", which was false for slot bookings — now "Requests are free • Slots secured with a £50 deposit".
- **Area cards show images** on Featured Areas, All Areas and the Area Details "Nearby Areas" block: `src` is the Section 3h convention path `/images/areas/{slug}-hero.webp` with `onerror` falling back to the existing `areas-hero.webp`. That folder doesn't exist yet, so every card currently shows the generic placeholder; real per-area art will appear with no code change.
- **Pricing audit**: every per-service price was already data-driven from `ServicesSeeder` at £90 (Plumbing) / £60 (Handyman). Fixed the statements that contradicted it: the FAQ's invented "£120 flat emergency callout" (now the £90/hr Plumbing rate, one-hour minimum, no call-out fee — matching `/Pricing`); `Booking/Index.cshtml`'s claim that the deposit "covers the 1st hour base rate"; the same page's summary script, which displayed the *first-hour price* as the deposit (the server charges a flat £50 — `BookingsService.CreateBookingAsync`) and used stale £65/£45 fallback rates; and `"priceRange": "$$"` → `"££"` in three JSON-LD blocks.

**Found during this pass** (the first three were fixed the same day, Section 3aw):
- **`ServicesSeeder`'s upsert doesn't sync `CategoryId`** (only `BasePrice`/`EstimatedDurationMinutes`, per Section 3ak), so Section 3ao's move of Bath & Shower Screen Fitting and Silicone & Mastic Resealing to Handyman never reached an already-seeded database: both still render under Plumbing at £60 on dev, and staging was seeded before 3ao too. **Fixed in Section 3aw.**
- **`_ValidationScriptsPartial.cshtml` breaks client-side validation wherever it's used** (Contact, Login, Register): it re-loads `jquery.validate` on top of the copy `_Layout` already loads — discarding the methods the unobtrusive adapter registered, hence the `__dummy__` console errors — and points at a non-existent unobtrusive path (missing `/dist/`, a 404). `JoinTeam.cshtml` deliberately doesn't include it. **Fixed in Section 3aw**, which also found that Login/Register were loading a separate, package-bundled copy.
- **Form success messages don't show for visitors who haven't accepted cookies**: `CheckConsentNeeded = true` blocks the non-essential TempData cookie. The submission itself succeeds; only the confirmation is lost. Affects Contact as well as Join Our Team. **Fixed in Section 3aw.**
- Verification left two `QA Test Applicant` enquiries in the local dev database — add to Tier 5 item 20's cleanup.

### Verified
- `dotnet build src/HandyFix.sln` — 0 errors; no new warnings from touched files (the SA1412 BOM warnings on `HomeController.cs`/`SeoController.cs` pre-date this change; the new `JoinTeamInputModel.cs` has a BOM).
- Full suite 154/154 (95 `Services.Data.Tests` + 59 `Web.Tests`), including three new `HomeControllerTests`: GET returns the form, an invalid POST re-renders without saving, a valid POST saves one prefixed enquiry with no images and redirects.
- Live app run + Playwright (`playwright-core` 1.62.1 in a scratch folder outside the repo, driving system Chrome with 390×844 mobile emulation and a 1440px desktop viewport): scrolled 1500px then tapped the menu — header at `top: 0`, dropdown within the viewport, scroll lock on; body padding equals header height; Zapryan with the correct role on all three categories; the three booking links resolve correctly; all 21 area images render via the placeholder; About button styles and icon colours as intended; `/Booking` summary shows a £50.00 deposit; no `£120` on `/FAQ`; `££` on all three pages; no horizontal overflow at 390px; Join Our Team client validation, end-to-end submission, success message (with cookies accepted) and a clean console.

---

## 3aw. Follow-ups: Seeder Category Sync, Validation Partials, Essential TempData Cookie (2026-09-10)

The three problems Section 3av found but kept out of its own scope, fixed the same day on the user's go-ahead.

- **`ServicesSeeder` now syncs `CategoryId` on existing rows**, alongside `BasePrice`/`EstimatedDurationMinutes`. Section 3ak scoped the upsert to price and duration, so Section 3ao's move of Bath & Shower Screen Fitting and Silicone & Mastic Resealing from Plumbing to Handyman never reached an already-seeded database — both were listed under Plumbing at the £60 Handyman rate. The next startup corrects them: done on dev, and on staging with its next deploy. Section 3ak's pre-launch caveat now covers category too: once real admin edits exist in production, the unconditional sync would overwrite an admin's category change as well as a price. Guarded by `ServicesSeederTests.SeedAsyncShouldMoveAnExistingServiceToItsSeededCategory` (InMemory is correct here — the seeder has no transactions or concurrency tokens to prove). `HandyFix.Data.csproj` gained `InternalsVisibleTo` for `HandyFix.Services.Data.Tests`, since the seeders are `internal`.
- **Client-side validation works again on every form.** `Views/Shared/_ValidationScriptsPartial.cshtml` loaded `jquery.validate` a second time after `_Layout` had already loaded it with the unobtrusive adapter — the second copy replaced `$.validator` and discarded the rules the adapter had registered (the `__dummy__` errors) — and referenced a non-existent unobtrusive path (a 404). It is now intentionally empty, kept as a file so existing `<partial>` references stay valid. Investigating it showed the scaffolded Login/Register pages never used that file: Identity pages resolve `Areas/Identity/Pages/_ValidationScriptsPartial.cshtml` first, supplied by the Identity UI package, which did its own double-load — the source of the `s.parseJSON is not a function` error Section 3f recorded on the login page. An empty app-level override now shadows it, which also covers the package's unscaffolded pages (Forgot Password and the rest).
- **Form confirmations show without cookie consent.** `CheckConsentNeeded = true` blocked the TempData cookie (non-essential by default), so the confirmation shown after a Contact or Join Our Team submission was silently dropped for any visitor who hadn't accepted the banner, even though the submission itself was saved. `CookieTempDataProviderOptions.Cookie.IsEssential = true` in `Program.cs`: the cookie carries no tracking data and exists only to answer a request the visitor just made — strictly necessary, the same category as the antiforgery cookie. The Cookie Policy page doesn't list framework cookies individually (antiforgery included), so its copy was left unchanged.
- `JoinTeam.cshtml`'s note about avoiding the broken partial was removed, since the partial is now safe.

**Found but not fixed:** the Contact page's inline submit script prepends `[Category: …]` to the message *before* validation runs, so an empty message passes both client and server validation (the prefix alone meets the 10-character minimum). Minor; recorded here rather than widening this pass. **Fixed in Section 3ax.** Verification added one `QA Test Contact` enquiry to the dev database (listed under Tier 5 item 20).

### Verified
- `dotnet build src/HandyFix.sln` — 0 errors; no new warnings (the SA1412 on `ServicesSeeder.cs` pre-dates this change; the new test file has a BOM).
- Full suite 155/155 (96 `Services.Data.Tests` + 59 `Web.Tests`), including the new seeder test.
- Live app run + Playwright, never accepting the cookie banner: after startup both moved services list under Handyman and neither under Plumbing; Contact shows inline client-side validation and its confirmation without consent, with no console errors; Login and Forgot Password block an empty submit with inline messages and no console errors.

---

## 3ax. Contact Form Validates What the Visitor Typed; Categories from Real Data; QA Rows Removed (2026-09-10)

- **The bug (Section 3aw's found-but-not-fixed item):** the Contact page's inline submit script wrote `[Category: X]` into the Message field when the form was submitted — before jQuery validation's own submit handler ran — so validation checked the *prefixed* text. The prefix alone met the 10-character minimum, so an empty message passed both client-side and server-side validation.
- **The fix — stop editing the visitor's input:** `ContactInputModel.Category` is now a real `[Required]` field bound to the dropdown; the submit script is deleted; `InquiriesService.CreateInquiryAsync` adds the `[Category: X]` prefix when saving, so the admin Enquiries list reads exactly as before, and Join Our Team (which sends no category) is saved as typed. Chosen over the smaller alternative — only adding the prefix after validation passes — because that keeps the fragile in-browser rewrite and still lets a JavaScript-disabled POST through.
- **Message limit 3000 → 2900:** the prefix is now added *after* validation, and `Inquiry.Message` is `nvarchar(3000)`, so a maximum-length message plus the prefix would otherwise fail on save.
- **Category options now match the business:** built from the real `ServiceCategory` rows (`ICategoriesService`, passed as `ViewData["ContactCategories"]`) plus "Other / Not Sure", behind a "-- Select a category --" placeholder. This replaces the hardcoded Plumbing / Electrical / HVAC / Handyman / Other list — the business offers neither Electrical nor HVAC as a category, and Small Building & Refurbishments was missing. A category added through the admin panel now appears with no code change (`VISION_AND_CONTEXT.md` Section 1).
- **Pre-selection:** `HomeController.Contact(service)` looks up the named service and pre-selects its category, so the Request Quote / Request Your Booking links (Category page, Service Details sidebar) arrive correctly tagged. Without it, every building quote request would have been filed under whichever category happened to be listed first.
- **QA rows removed:** the three Section 3av/3aw verification enquiries, plus this pass's own test submission, were hard-deleted with `sqlcmd` inside the SQL Server container, each in a transaction that committed only on the exact expected row count (credentials read from user secrets inside the command, never printed). The admin Enquiries "Delete Permanent" button couldn't do it — it only soft-deletes — which is now roadmap Tier 3 item 24, alongside a note that the legal pages as a whole need proper work.

### Verified
- `dotnet build src/HandyFix.sln` — 0 errors; no warnings from touched files.
- Full suite 158/158 (97 `Services.Data.Tests` + 61 `Web.Tests`). New: `InquiriesServiceTests.CreateInquiryAsyncShouldPrefixMessageWithCategoryWhenProvided`, `HomeControllerTests.ContactGetShouldPreselectTheCategoryOfTheRequestedService` and `ContactGetWithoutServiceShouldLeaveCategoryUnselected`. The existing `CreateInquiryAsyncShouldSaveToDatabaseWithImages` still proves a message without a category is saved unchanged.
- Live app run + Playwright: the options are the placeholder, Handyman, Plumbing, Small Building & Refurbishments and Other, with nothing pre-selected by default; an empty message is blocked in the browser with "Please enter your message." and the field is left untouched; a missing category is blocked; `?service=` pre-selects Small Building (Wall & Floor Tiling), Plumbing (Tap Repairs) and Handyman (TV Mounting), and nothing for an unknown name; no console errors; a direct POST with an empty message, bypassing every browser-side check, re-renders with the error — no redirect, nothing saved; a real submission was stored as `[Category: Handyman] …` (confirmed in the database, then deleted — 0 QA rows left, the 7 real enquiries untouched).

---

## 4. Current Standing & Remaining Roadmap

### Pre-Sprint 4 TODOs — resequenced by launch-blocking priority (updated 2026-07-30)

> Supersedes the original chronological ordering (items 1–10, first logged 2026-07-25). The business decisions behind every item below — and the full reasoning — live in `docs/private/VISION_AND_CONTEXT.md` Section 5–6; this section tracks only what's left to *build*. Target launch is aggressive: roughly 4 weeks out from 2026-07-30. Ordering follows one rule now — **what's actually on the critical path to launch, not the order things were discovered in.**

**Audit findings that drove these decisions** (verified against the code on 2026-07-25, still accurate):
- `wwwroot/images/services/` holds 23 files but only **16 unique images** — the original Gemini run hit a rate limit and left 7 byte-identical duplicates across four groups: `door-repairs`/`furniture-assembly`/`minor-home-repairs`; `curtain-and-blind-fitting`/`handyman-category`/`painting-touch-ups`/`property-maintenance`; `tv-mounting`/`wall-mounting`; `minor-electrical-tasks`/`shelf-installation`. Plus one junk file, `test-image-from-admin-edit-hero.webp` (670×458, gitignored by `**/wwwroot/images/**/test*`).
- **Every existing image has a garbled, inconsistent "HandyFix" logo rendered on the technician's polo** — an AI text artifact that differs in every frame. The business is now confirmed rebranding (name TBD, see `VISION_AND_CONTEXT.md` Section 5.3), so the logo-free/text-free constraint on future artwork is a certainty now, not a hedge.
- The plumbing/handyman two-tone is currently **baked into the artwork pixels**. There is no division-scoped image tint anywhere in `wwwroot/css/` — every existing overlay (`.hero-bg-overlay`, `.bento-card-overlay`, `.service-hero-overlay`) is chromatically neutral. This is what motivates item 1's move to CSS.
- `wwwroot/images/areas/` does not exist; all 15 of its paths are already wired into the Areas feature with graceful `onerror` fallback to `/images/hero.webp` (was `hero.png` until Section 3j).
- **There is no `aggregateRating` / `Review` structured data anywhere in the repo.** The invented technicians, £5M insurance claim, and job counts are all in ordinary HTML markup on `Home/Index.cshtml` and `Services/Details.cshtml`. The JSON-LD's actual problem is fake NAP data (Tier 3 item 18).

---

#### Tier 0 — unlocks everything else (this week)

**0. The client call.** A single structured session with Zaprqn collecting everything Tier 2/3 below is blocked on. Confirmed happening this week (week of 2026-07-30). See `VISION_AND_CONTEXT.md` Section 6 for the canonical checklist. **Do not start Tier 2/3 work before this happens** — guessing at any of it means redoing it.

---

#### Tier 1 — no client input needed, start immediately in parallel

**1. Image generation batch.** Bulk-generate and download images via a terminal script kept **outside the repo** — no committed generator project. The plumbing/handyman two-tone gets applied **via CSS later**, not baked into the generated artwork, so the tone can be retuned without regenerating anything.
  - **Provider: Google Gemini API** (2.5 Flash Image / "Nano Banana"), decided 2026-07-30 — real API access rather than the rate-limited consumer app that produced the original 16 keepers. Same model family, so new output should match their style without a visible seam.
  - **Budget: $30** (raised 2026-07-30 from the original $10–15 estimate), comfortably covers 30–60+ images at Gemini's per-image pricing with room for retries — and now also covers whatever the Custom Projects category (Tier 2 item 12) turns out to need.
  - **Prompting strategy**: structured/JSON-style prompts (a fixed schema per shot — subject, style, lighting, camera angle, palette, negative prompt) to keep a large batch visually consistent, using the 16 existing keeper images and 4–5 client-supplied background photos as style anchors once shared. Full prompt-schema design is implementation-session work, not designed yet.
  - **The agreed workflow, end to end:** generate outside the repo → **convert to WebP outside the repo** (quality 80, to match what the app's own pipeline produces) → **name each file exactly per the conventions below** → copy the finished `.webp` files straight into `wwwroot/images/`. The app is not involved at any step: no admin upload needed, no startup conversion, and no database changes, because `ServicesSeeder.cs:79` already created `ServiceImage` rows pointing at the convention path `/images/services/{slug}-hero.webp`.
  - **Do not copy `.jpg` or `.png` into `wwwroot/images/`.** Nothing converts them any more (see Section 3k) — they will simply sit there while the views, which expect `.webp`, fall through to their `onerror` fallback. Failure is visible and harmless, but it wastes time.
  - No resize is needed on your side for service images: at 1024×1024 they are under the app pipeline's 1920px threshold, so it would not have resized them either. Only area heroes (1600×700) and any large marketing images need attention to dimensions.
  - The admin upload form remains available as an alternative for one-off replacements — it still does resize + WebP + correct naming automatically, and it is the path that produced `hero.webp` in Section 3j. It is just not the efficient route for a 40+ image batch.
  - Filename conventions are fixed and must be matched exactly: services `/images/services/{slug}-hero.webp` (the canonical source is `ImageStorageService.GetServiceImagePublicUrl`, added in the Section 3aa thin-controller pass; `docs/WORKFLOW_SERVICES.md` tracks the remaining places that can't call it and have to hardcode the pattern themselves — currently `ServicesSeeder.cs`, the two service view models' Mapster fallback, and the admin Edit view's preview); areas `/images/areas/{slug}-hero.webp` (resolved by convention at the view layer — `ServiceArea` deliberately has no image column). Note the two category tiles break the pattern: `{slug}-category-hero.webp`.
  - Target dimensions the markup already declares: service/category 1024×1024, area heroes 1600×700. (The coverage map is no longer an image asset — see Section 3l.)
  - **New scope, folded into the same batch rather than a second round**: whatever the Custom Projects category (Tier 2 item 12) needs once scoped with Zaprqn — likely one category/service hero for full bathroom installs and one for full kitchen installs.

**2. Hero image handling.** ~~Done — see Section 3j.~~

**3. Reviews cleanup.** ~~Done — see Section 3n.~~ Engineering complete; the actual Google Business Profile URL still needs filling in once it exists (Tier 3 item 13).

**4. Area map.** ~~Done — see Section 3l.~~

**5. Broken links & metadata.** ~~Done — see Section 3p.~~

**6. Caching.** ~~Done.~~

**7. Email provider swap — SendGrid → Brevo.** ~~Done — see Section 3q.~~

**8. Manual per-slot technician assignment.** ~~Done — see Section 3r.~~ **Resolved differently than this item proposed**: rather than adding a technician picker to slot generation, technicians were removed from `AvailabilitySlot` entirely. Slots are pure capacity; assignment happens on the `Booking` after the customer has booked and paid. Auto-generation of slots from the public booking page was removed in the same pass, and a non-production-only capacity seeder added so fresh clones and staging still have a working booking flow.

---

#### Tier 2 — blocked on the client call (Tier 0)

**9. Real business facts.** ~~Insurance certificate~~ **closed 2026-09-01, not merely still needed — Zaprqn will not publish an exact figure at all, coverage-confirmed-real is the whole story (Section 3ah).** Still open: real years trading, real phone number, real trading address, his photo and name — the client call started 2026-09-01 but didn't reach these. Feeds directly into Tier 3's NAP/stats work — nothing there can be finalized before this lands.

**10. Rebrand — the business name itself.** ~~Confirmed happening at this week's call.~~ **Done 2026-09-01 — see Section 3ah.** Name is "Plumbing Handyman Surrey." Unblocks Tier 3's Google Business Profile creation (item 13), domain decision (item 14, also done), wordmark/logo (item 15), and final JSON-LD/NAP values (item 18) — all now startable.

**11. Real technician roster.** Names and phone numbers for Zaprqn and his worker(s) — however many are actually going active at launch. **Not collected at the 2026-09-01 call session** — still open. **No longer a code change**: since Section 3r there is full admin CRUD at `/Administration/Technicians`, so this is now data entry through the UI. Note this was a hard prerequisite, not a convenience — `TechniciansSeeder` only inserts when the table is empty, so adding a second technician by editing the seeder would never have worked. The seeded placeholder (`John Doe / 07123456789`) should be edited into a real person or deactivated once real names land; it can't be deleted once it has bookings, by design. **Clarified 2026-09-01**: only Zaprqn's own photo is needed, not one per worker — see item 17, no team-roster UI exists for worker photos to appear in.

**12. Custom Projects / Small Building Works category scope.** ~~New service category for launch — full bathroom installation, full kitchen installation.~~ **Done 2026-09-08 — see Section 3am.** Category created as "Small Building & Refurbishments" (`small-building-works`) with 8 tailored services (Bathroom Refurbishment, Kitchen Fitting, Partition Walls & Stud Work, Plastering & Ceiling Repairs, Wall & Floor Tiling, Flooring Installation, External Brickwork & Paving, Custom Carpentry & Boxing-In), scope and pricing confirmed with Zaprqn. Quote-first routing implemented (`/Contact?service=...`) so multi-day jobs bypass hourly slot booking, including on the `/Pricing` page. Category landing, catalog, home division cards, and pricing page fully wired.

---

#### Tier 3 — blocked on Tier 2 outputs landing

**13. Google Business Profile creation & verification.** ~~Cannot start until the business name (item 10) is settled.~~ **Unblocked 2026-09-01 — start now.** Verification (commonly postcard-based, 1–2+ weeks in transit) is the single longest lead time on the whole launch-blocker list and shouldn't wait behind other Tier 3 work.

**14. Domain finalization.** ~~Depends on the rebrand decision.~~ **Done 2026-09-01 — see Section 3ah.** The old name.com/Zap Construction recovery question is moot — a fresh domain was bought instead: `plumbing-handyman-surrey.co.uk` (primary) + `.com` (backup), via `names.co.uk`, Denislav's account, Zaprqn paid. **New follow-up, not yet done**: point the domain's DNS at the Hetzner staging box (currently reachable only via its free reverse-DNS hostname), and set up Zoho Mail (free tier, up to 5 branded addresses) — MX records + mailbox creation, chosen over the registrar's own paid email add-on.

**15. Wordmark/logo.** Blocked on item 10. Build as SVG from the existing Outfit font + navbar cyan accent dot once the name lands — not AI-generated, which cannot render text reliably (the exact problem with the current garbled-logo images, item 1).

**16. Fake statistics → real or removed.** Replace fabricated figures across `Home/Index`, `Home/Reviews`, `Home/About`, `Services/Index`, `Services/Details`, `Services/Category`:
  - **Resolved 2026-07-30, closed for good 2026-09-01**: `Up to £5M Public Liability insurance` → **"Comprehensive Public Liability insurance"**, confirmed genuine coverage (exact figure will not be published at all, per Zaprqn — not merely still pending, item 9) — `Home/Index.cshtml:238` keeps its existing "…covering every single visit" tail unchanged; `Services/Details.cshtml:217`'s shorter sidebar line and `Services/Details.cshtml:153`'s FAQ-prose version get the equivalent swap adapted to each sentence's shape, not a literal paste. The **"100% satisfaction guarantee"** bundled into that same FAQ sentence (`Services/Details.cshtml:153`) is confirmed **not real — remove it outright**, don't reword it.
  - **Still blocked on item 9 (real facts)**: ~~`12k+ Jobs Completed`, `4.9/5 Rating`, `Based on 2,500 reviews` (`Home/Index.cshtml:251,262,263`)~~ removed Section 3at; `4.9` + `2.4k Verified Reviews` (`Home/Reviews.cshtml:96,109-113`); ~~`4.9/5 Rating` + `Over 1,200 services completed` (`Services/Details.cshtml:170,173`)~~ removed Section 3av; `4.9/5 Average Rating` (`Services/Index.cshtml:95`); ~~`5,000+ Successful Fixes`, `15+ Specialist Techs`, `Crafting Quality Since 2018` (`Home/About.cshtml:14,48-53`)~~ removed Section 3au; `3 Active Technicians Nearby` (`Services/Category.cshtml:140`). The counts contradict each other (12k+ vs 5,000+ vs 1,200 jobs; 2,500 vs 2.4k reviews vs 5 rows in the database), and most are rating/review claims that no longer make sense once Reviews goes GBP-link-only (item 3) — expect most to become the honest, already-identified substitutes rather than real numbers: *"Direct to your technician — no call centre"*, *"Covering 15 areas from Chessington"* (real `ServiceArea` rows), *"Fixed hourly rates, quoted upfront"* (real `BasePrice`), *"Pay securely online — deposit only"* (real Stripe integration).
  - Worth noting on the upside, still true: no Gas Safe, NICEIC, TrustMark, Which? or Checkatrade badges appear anywhere — the highest-severity fabrication category (Gas Safe numbers are legally regulated) is clean.

**17. Technicians — public-facing bio block.** ~~Remove the hardcoded `"David"` / `"Mark"` Razor variables~~ **Invented personas removed 2026-09-10 (Section 3av)** — every service page now shows Zapryan's real photo and his own supplied bio, with only the role line varying by category. Still open: the block is still hardcoded Razor variables in `Views/Services/Details.cshtml`; binding it to the real `Technician` entity once item 11 lands is the remaining work. Do **not** AI-generate a face here. Confirmed 2026-07-30: no public technician roster/grid UI is needed for now beyond this bind-to-real-data fix — that's a "grow into it later" feature. A small technician-detail card **in the booking confirmation email** is separate, small new work — the email already renders the technician's name as a plain list item (`PaymentsService.SendBookingConfirmationEmailsAsync`); turning that into a styled card with name/phone (photo only once item 9's photo lands) is the actual remaining task here.

**18. NAP consistency.** The site must state one identity before the Google Business Profile is claimed — blocked on items 9 and 10 landing together. Currently: phone `07123456789` (fake/sequential) appears in three JSON-LD blocks (`Home/Index.cshtml`, `Services/Details.cshtml`, `Areas/Details.cshtml`); `addressLocality` is `Croydon` on Home but `Chessington` on Area pages (Chessington is correct per the seeded drive-time data — 0 minutes, "Home Turf"); `streetAddress` is the non-address `"South London Dispatch Office"` with invalid partial postcode `"CR0 1XX"`; `sameAs` points at two probably-nonexistent social profiles — remove until real profiles exist. Bad NAP/social data actively harms local SEO and will conflict with the real profile once claimed.

**24. Legal pages & data protection — all of it needs proper work before launch.** Added 2026-09-10 at the user's request. The Privacy Policy, Terms & Conditions and Cookie Policy are template text that has never been checked against what the site actually does; none of it should be treated as legally sound. Known gaps, checked against the code on 2026-09-10:
  - **Stale identity and dates.** Privacy ("Last Updated: June 15, 2024") and Terms ("October 24, 2024") predate this project, and both give contact emails on a `handyfix.com` domain the business doesn't own — the real domain is `plumbing-handyman-surrey.co.uk` (item 14). No data controller (legal entity and address) is named anywhere; that part is blocked on item 9's real trading details.
  - **Data the pages don't mention**: Join Our Team applications (Section 3av), enquiry and booking photos stored in Cloudflare R2, and transactional email sent through Brevo. No retention period is stated for anything.
  - **Cookie banner and Cookie Policy.** The banner says cookies are used "to understand how you use our site", but the site has no analytics at all. The Cookie Policy describes Stripe's cookies only and doesn't list the site's own (antiforgery, login, consent, and the TempData cookie made essential in Section 3aw).
  - **The admin "Delete Permanent" button on an enquiry doesn't delete permanently.** `InquiriesService.DeleteAsync` calls the deletable repository's `Delete`, which only sets `IsDeleted`/`DeletedOn`: the row — name, email, phone number and message — stays in the database, merely hidden from every query. For personal data, a button labelled "permanent" that isn't is a data-protection problem as well as a misleading label, and it means a genuine erasure request can't currently be honoured from the admin panel. Decision still to make: either make it a real delete (`HardDelete`, after removing the enquiry's `InquiryImage` rows — the FK — and the photo files in R2), or label it honestly and add a separate purge. Found while removing QA rows in Section 3ax, which had to use SQL for exactly this reason.
  - **Terms content**, not just wording: the £50 deposit, the 24-hour cancellation window and the refund rules need confirming with the business.

---

#### Tier 4 — deployment (content-independent, runs in parallel with Tiers 1–3)

~~**19. Hosting & CI/CD (staging half).**~~ **Done 2026-08-04 — see Section 3w.** `dev` branch,
`deploy-dev.yml` (build/test/push to GHCR/SSH deploy), Caddy (HTTPS + Basic Auth + noindex), and a
dedicated least-privilege SQL login are all live and verified against the real public staging URL.
Config is environment variables into the container (not `appsettings.Staging.json` files — revises
the original plan, see Section 3w). **Still open, not blocking**: `deploy-prod.yml` for production
(deliberately deferred until staging has been used for a while, rather than built in parallel);
`Stripe:SecretKey` (no real account yet, sandboxed via `Stripe:AllowSandboxOutsideDevelopment`);
~~`CloudflareR2:*` for staging~~ **done 2026-08-05, see Section 3ae**; rotating the DB host's `sa`
password.
  - **Staging should be reachable before real business facts exist.** Confirmed goal (2026-07-30): filling in Zaprqn's real data should be a 5-minute edit at the end, not a blocker for standing up staging and demoing on an actual phone. Achieved — currently running on the Hetzner-assigned reverse-DNS hostname (item 14 below still unresolved, doesn't block this).

#### Tier 5 — dev-environment housekeeping (parked until just before launch)

**20. Clear the QA leftovers out of the local dev database.** Deliberately parked 2026-08-01: nothing is broken, and none of it reaches production — every row below was created by hand through the admin UI during live verification, so it exists in no seeder and a freshly-seeded `handyfix_prod`/`handyfix_staging` will never contain it. State verified against the dev database 2026-08-01; the two halves need **different methods**, which is the part worth not rediscovering:

  - **`Zapryan Petrov` technician** (`F83BC86C-98C1-4CC1-8B63-1ACFF9EBB83A`, Section 3r) — needs a **manual `DELETE`**. It is already soft-deleted, so the global query filter hides it from the admin list entirely and there is no UI path back to it; `TechniciansService.DeleteAsync` soft-deletes anyway, so even reaching it would not hard-delete. Confirmed safe: **0 bookings reference it**, and `John Doe` remains in the table afterwards, so `TechniciansSeeder` (which inserts only when the table is *empty*) will not re-seed.
  - **`test-image-from-admin-edit` service** (Section 3j) — **use the admin UI, not SQL**. It has a `BookingServices` row pointing at it, so a hard `DELETE` would fail the foreign key; soft delete is correct. `/Administration/Services` → Delete does both halves in one action, because `ServicesService.DeleteAsync` calls `imageStorageService.DeleteServiceImage(slug)` *before* deleting the row (this moved from the controller into the service itself in the Section 3aa thin-controller pass — same behavior, different home), removing `wwwroot/images/services/test-image-from-admin-edit-hero.webp` from disk. And `ServicesService.GetAllAsync` queries `All()` (not `AllWithDeleted()`), so it genuinely disappears from the list — this feature does **not** have the Section 3o Reviews bug.
  - **Worse than Section 3j recorded**: that service is `IsActive = 1, IsDeleted = 0` — currently **live on the public site** as an 11th Handyman service, on `/Services/handyman`, `/Pricing`, and in the generated sitemap. Dev-only, but it is not merely an inert row. A second row, `test-image` (Plumbing), is already soft-deleted with no references and needs nothing.
  - **Related, tracked separately**: the seeded `John Doe` placeholder technician has 8 bookings and therefore cannot be deleted by design (Section 3r). Editing it into a real person is Tier 2 item 11, not this item.
  - ~~**Added 2026-09-10**: three enquiries created by live verification in Sections 3av/3aw — two `QA Test Applicant` (Join Our Team) and one `QA Test Contact`.~~ **Removed 2026-09-10 (Section 3ax)** — hard-deleted by ID with `sqlcmd` inside the SQL Server container, in a transaction that committed only on exactly three affected rows. The admin Enquiries "Delete Permanent" button could not do this: it only soft-deletes (Tier 3 item 24).

---

#### Tier 6 — flagged in a marketing-strategy discussion, not on the critical path to launch (2026-08-03)

Neither item below is a data-integrity bug or a launch blocker — both are gaps that only bite once the technician roster or the admin team grows past its current size (Zaprqn plus one worker). Recorded here so they aren't rediscovered from scratch later, not because either needs fixing now.

**21. No technician-skill/service-category matching.** Nothing in the data model stops an admin from assigning a technician to a booking for a service outside that technician's actual trade — `Technician` carries no skill/specialty tags, and the assignment dropdown (`TechniciansService.GetAssignableAsync`, Section 3r) lists every active technician for every booking regardless of what service was ordered; it only ever filters on `IsActive`. Harmless today since Zaprqn and his worker(s) likely cover both Plumbing and Handyman between them, but becomes a real mis-dispatch risk (a plumbing-only technician assigned to an electrical job) the moment a single-trade technician joins the roster. Fix shape, when it's worth doing: add a category/specialty tag set to `Technician`, filter the picker by the booking's service category, keep the full list reachable but flagged for a deliberate manual override.

**22. Approving a booking has no guard against a missing technician.** `BookingsController.Approve` calls `UpdateStatusAsync` unconditionally — nothing checks `Booking.TechnicianId` before the status flips to `Approved` and the "CONFIRMED" email fires. Not a data-loss or silent-failure bug: the email still sends, just with a generic line instead of naming a technician, and this exact sequencing trap is already called out in `docs/WORKFLOW_BOOKINGS.md`'s troubleshooting table ("Assign first, then approve"). The gap is that nothing in the UI *enforces* the order — it relies entirely on the admin remembering the documented sequence, with no warning or disabled state on the Approve button when unassigned. Worth a small UI safeguard once more than one person is approving bookings day to day.

**23. Fallback Booking Flow / Lead Generation.** When the availability calendar has no open slots, users should not hit a dead end. Instead of "No slots available", the booking UI should fall back to a "Request a Quote" or "Join Waitlist" form. This captures the lead (name, phone, problem description) so the admin can call and manually schedule them outside standard slots. Added to roadmap following a business strategy QA session (2026-08-03).

---

### Sprint 3 — Admin & Polish — **CLOSED** (2026-07-24)
- ~~Admin panel list refinements (sortable/queryable "Order by" on Bookings/Enquiries/Reviews lists).~~ **Done** — Bookings in Section 3d, Enquiries in Section 3e, Reviews in Section 3f.
- ~~Usability enhancements across the admin area.~~ **Dropped** — stayed unscoped with no concrete items identified; user confirmed the list-refinement work above satisfies Sprint 3's usability goals and closed the sprint without further items here.
- ~~Admin-area inline-style cleanup (343 occurrences, deferred here from Sprint 2 to avoid mixing scope).~~ **Done** — see Section 3a below.

### Pricing Page Migration & Pricing System Integration — **CLOSED** (2026-07-24)
Next initiative after Sprint 3, not part of the original Sprint 4 plan below. See Section 3g for the full scope of what shipped (new `_PricingCard` partial, rebuilt Pricing page, Service Details integration, `.glass-card` dedupe fix). Confirmed complete — no further work planned under this initiative.

### Sprint 4 — Testing, Documentation & Deployment
- ~~Comprehensive unit test coverage beyond what Sprint 1 required (controller-level tests).~~ **Done — see Section 3t.** 52 mocked-service controller tests added (`HandyFix.Web.Tests` 9 → 61, whole suite 118), and the integration tests no longer run against the developer's dev database, which also makes them CI-viable for Tier 4.
- ~~Broader service coverage — `CategoriesServiceTests`/`ServicesServiceTests` were the thinnest tests in the suite.~~ **Done — see Section 3ag.** 20 new tests added (6 → 26 combined), covering every previously-untested method on both services; also fixed a latent test-infrastructure gap it surfaced (Mapster's real mapping config was never registered in this test project).
- ~~Architecture documentation and a completed GitHub README.~~ **Done — see Section 3s.** The README's inaccurate tech-stack claims are corrected and its Documentation section now links `PROJECT_STATE.md`/`DESIGN.md`/the workflow docs instead of "coming soon" placeholders. No separate architecture document was written: Section 1 of this file is that document, and the README points at it rather than duplicating it.
- ~~Bring `DESIGN.md` back in sync with the CSS that actually exists.~~ **Done — see Section 3x.**
- ~~Document every remaining workflow, then complete the README index.~~ **Done — see Section 3x.** All five gaps (services & categories, technicians, reviews, enquiries, deployment) now have their own `docs/WORKFLOW_*.md`, and the README's "in progress" note is gone.
- ~~CI/CD pipeline setup for staging.~~ **Done 2026-08-04 — see Section 3w.** `deploy-dev.yml` builds, tests, and deploys on every push to `dev`.
- Production deployment — `deploy-prod.yml` deliberately not built yet, see roadmap Tier 4 item 19.

---

## 5. Architectural Decisions Worth Remembering

- **Optimistic concurrency needs a provider that actually enforces it.** EF Core's InMemory provider silently ignores both transactions and `RowVersion` concurrency checks — tests that need to prove rollback or double-booking rejection use Sqlite in-memory (`Microsoft.Data.Sqlite`, `DataSource=:memory:`, open connection kept alive for the test's duration), not InMemory.
- **`IDbQueryRunner.BeginTransactionAsync`** is the standard way to wrap multi-repository mutations atomically; nested `SaveChangesAsync` calls from different repositories sharing the same scoped `DbContext` automatically join the ambient transaction — no need to pass a transaction object around explicitly.
- **The "fail loud outside development, fall back safely inside it" pattern** is now used twice (Stripe key, Brevo key) and should be the default template for any future third-party integration key: never let a missing production secret silently degrade to mock/no-op behavior.
- **Capacity and assignment are separate concerns, and only one of them is a slot.** `AvailabilitySlot` is business capacity and carries no technician; `Booking.TechnicianId` is the single home of "who does this job", set by an admin after payment (Section 3r). Don't reintroduce a technician (or any other assignment-shaped field) onto the slot to make a query convenient — that duplication is exactly what caused the stale-copy and wiped-on-reschedule bugs Section 3r removed.
- **Read paths must not write.** `GetAvailableDatesAsync` used to generate 30 days of slots as a side effect of being read, so merely browsing the public booking page created capacity nobody had decided to offer. Generation is now only ever triggered deliberately — by an admin, or by the non-production capacity seeder. Treat any "get" that mutates as a bug, not a convenience.
- **`SlotUnavailableException`** exists specifically so controllers can distinguish "the resource you wanted is gone" from generic `InvalidOperationException` validation failures — reuse this pattern rather than string-matching exception messages.
- **`wwwroot/css/base/utilities.css`** (added in Sprint 2) holds the small, generic spacing/typography/opacity/radius classes shared across every page (`mb-*`, `fs-*`, `lh-*`, `opacity-*`, `rounded-*`, `icon-fill`, etc.) — check here before inventing a new one-off class or reaching for an inline `style=`. Anything page-specific still belongs in that page's own `pages/*.css` file.
- **The sitemap is generated, not static** — `SeoController.Sitemap()` queries categories/services live via `ICategoriesService`/`IServicesService` rather than hardcoding URLs, specifically so it can't go stale as services are added or removed through the admin panel. Follow the same approach for any future sitemap-like listing.
- **Commit hygiene**: this project follows Conventional Commits with a `type(scope): title` subject line followed by a `- ` bullet per notable change (not prose paragraphs) — see recent `git log` for the established style before writing commit messages.
