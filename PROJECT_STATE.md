# HandyFix — Project State & Architecture Roadmap

> **Purpose**: This is the permanent architectural memory for HandyFix. It records what the system actually is (not aspirational template boilerplate), what's been built and verified, and what's left. Update it at the close of each sprint rather than letting it drift out of sync with the code.
>
> **Last updated**: 2026-07-31 — Tier 1 item 8 shipped, resolved differently than planned: technicians are decoupled from capacity slots entirely, slot auto-generation is removed, and there is now admin CRUD for the technician roster; see §3r. (Earlier the same day: Tier 1 items 3, 5 and 7 shipped — Reviews cleanup §3n, broken links §3p, Brevo email swap §3q.) (Previous update, 2026-07-30: Pre-Sprint 4 TODOs resequenced into launch-priority tiers after a full business-decisions session with the user (rationale logged in `docs/private/VISION_AND_CONTEXT.md` §5). Added a Hosting & Infrastructure subsection to §1 (Hetzner architecture, provisioned but not yet wired up) and three new TODO items: SendGrid→Brevo email swap, manual per-slot technician assignment, and the Custom Projects service category. Previous update, 2026-07-25: three of the original ten items had shipped — hero WebP migration §3j, cache-busting, and the Areas SVG coverage map §3l — plus the boot-time JPG sweep removed as unsafe §3k and Service Areas admin CRUD added §3m.)

---

## 1. Tech Stack & Core Architecture

### Backend
- **ASP.NET Core MVC on .NET 10**, structured as Clean Architecture with strict layer separation:
  - `Data/` — `HandyFix.Data.Models` (entities), `HandyFix.Data.Common` (repository interfaces, base model classes), `HandyFix.Data` (EF Core `ApplicationDbContext`, migrations, repositories, seeders).
  - `Services/` — `HandyFix.Services.Data` (business logic services, one per domain area: Bookings, Availability, Payments, Services, Reviews, Inquiries, Categories), `HandyFix.Services.Mapping` (object mapping), `HandyFix.Services.Messaging` (email), `HandyFix.Services` (cross-cutting: image upload, R2 storage).
  - `Web/` — `HandyFix.Web` (MVC controllers, views, `Program.cs` composition root, background workers), `HandyFix.Web.ViewModels` (DTOs/view models, kept out of the Data layer).
  - `Tests/` — `HandyFix.Services.Data.Tests` (service-layer unit tests), `HandyFix.Web.Tests` (full-stack `WebApplicationFactory` integration tests).
- **Object mapping**: [Mapster](https://github.com/MapsterMapper/Mapster), via a custom `IMapFrom<T>`/`IMapTo<T>`/`IHaveCustomMappings` convention registered once at startup (`MappingConfig.RegisterMappings`). *Note: the README's "AutoMapper"/"CQRS (MediatR)" claims are aspirational leftovers from the original template — neither AutoMapper nor MediatR is actually present anywhere in the codebase. Services are plain classes with direct async methods, not CQRS command/query handlers.*
- **Repository pattern**: `IRepository<T>` / `IDeletableEntityRepository<T>`, generic EF Core-backed implementations (`EfRepository<T>`, `EfDeletableEntityRepository<T>`). Soft-delete (`IsDeleted`/`DeletedOn`) and audit fields (`CreatedOn`/`ModifiedOn`) are applied globally via `ApplicationDbContext.OnModelCreating`/`SaveChanges` overrides, not per-entity boilerplate.

### Database
- **SQL Server**, run locally via a Docker container (`MSSQLServer`, port 1433) for development.
- **Entity Framework Core 10.0.5**, code-first migrations under `src/Data/HandyFix.Data/Migrations/`.
- All entities use **`Guid` primary keys** (`BaseModel<Guid>` / `BaseDeletableModel<Guid>`), generated client-side in entity constructors.
- **Centralized package management**: `src/Directory.Packages.props` (all NuGet package versions pinned in one place, `ManagePackageVersionsCentrally=true`) and `src/Directory.Build.props` (shared `TargetFramework=net10.0`, StyleCop analyzers, language version) — individual `.csproj` files only declare `<PackageReference Include="..." />` with no version.

### Storage & Static Assets
- **Cloudflare R2** (S3-compatible object storage) via `CloudflareR2Service`/`ICloudflareR2Service`, used for *user-uploaded* content: booking problem photos and inquiry photos (`ImageService.UploadImagesAsync`, capped at 5 files / 15MB each, JPEG/PNG/WebP only).
- **Local optimized WebP assets** under `wwwroot/images/`, used for *admin-curated* content: service category hero images. Pipeline is `ImageStorageService` (SkiaSharp-based): resizes uploads >1920px wide, re-encodes to WebP at quality 80, deletes legacy JPG/PNG on save. **This pipeline is scoped only to `images/services/`** and only runs on an actual admin upload — it does not touch the homepage hero image or any area/marketing images. There is no longer any startup sweep over the images directory (see §3k). Assets produced outside the app are expected to arrive already WebP-encoded and correctly named; nothing converts them on boot.

### Frontend
- Razor views + Bootstrap 5 + vanilla JS. CSS is organized under `wwwroot/css/` as `base/`, `components/`, `pages/` partials imported into a thin `site.css` (not a single monolithic stylesheet). A shared `page-container` class provides a consistent boxed hero/content width across every public page. WebOptimizer is registered for CSS/JS bundling.
- Design language documented separately in `DESIGN.md` (color palette, typography, spacing, animation conventions).

### Testing
- **xUnit + Moq**, service-layer focus (no controller-level mock test harness exists yet — that's a Sprint 4 gap).
- Tests run against **EF Core InMemory** for simple CRUD-style assertions, and **EF Core Sqlite (`:memory:`)** for anything that depends on real transactions or concurrency-token enforcement — InMemory silently no-ops both `BeginTransaction`/`Commit`/`Rollback` and optimistic-concurrency checks, so it cannot verify rollback or race-condition behavior. This distinction is load-bearing; don't "simplify" a Sqlite-backed test back to InMemory without checking why it was Sqlite first.

### Hosting & Infrastructure (provisioned 2026-07-30, not yet wired up)

> Real IPs are deliberately not in this file — see `docs/private/INFRASTRUCTURE.md` (gitignored) for the actual values behind the placeholders below.

- **Provider**: Hetzner Cloud, Helsinki region. **Three isolated VPS instances** (CX23 — 2 vCPU / 4 GB RAM / 40 GB NVMe each), one per tier, all attached to a private network (`handyfix-internal`, `10.0.0.0/16` — see the private file for real octets):
  - Database host (`<DB_PUBLIC_IP>` / `<DB_PRIVATE_IP>`) — Dockerized MS SQL Server 2022 (container `handyfix-sql`), memory-capped at 3 GB (`MSSQL_MEMORY_LIMIT_MB=3072`) to prevent OS starvation on a 4 GB host. Two empty databases already created via local SSMS: `handyfix_prod`, `handyfix_staging`.
  - Staging web host (`<STAGING_PUBLIC_IP>` / `<STAGING_PRIVATE_IP>`).
  - Production web host (`<PROD_PUBLIC_IP>` / `<PROD_PRIVATE_IP>`).
  - Both web servers have Docker + Docker Compose installed with auto-start on boot.
- **Network isolation, deliberately**: web apps reach SQL Server exclusively over the private interface (`<DB_PRIVATE_IP>`) — database traffic never touches the public internet. Root password login is disabled fleet-wide; SSH access is public-key only.
- **Not yet done** — tracked as §4 Tier 4: app-level connection strings for `appsettings.Staging.json`/`appsettings.Production.json` against `<DB_PRIVATE_IP>`, `Database.Migrate()`/seeding running automatically on startup (already the pattern for local dev — just needs pointing at the new hosts), and the GitHub Actions deploy pipelines (`deploy-dev.yml` on push to `dev`, `deploy-prod.yml` on push to `main`), including a dedicated Actions SSH keypair and repo secrets (real IPs and credentials live in GitHub encrypted secrets at that point, never in a committed file).
- **Domain**: not yet wired up, and entangled with the pending rebrand decision — see `docs/private/VISION_AND_CONTEXT.md` §5.3/§5.6 and §4 Tier 3 item 14 below. Staging (and initial production, if needed) can run on Hetzner's free reverse-DNS hostname in the meantime, so this does not block standing up staging.

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
- `IEmailSender` resolves to `SendGridEmailSender` when `SendGrid:ApiKey` is configured, falls back to a no-op `NullMessageSender` only in development, and throws at resolution time otherwise (same fail-loud-outside-dev pattern as Stripe).
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

Closes Pre-Sprint 4 TODO item 2 (see §4) — the first of those items to ship, and the single biggest performance win on the site.

- **`hero.png` (6.6 MB PNG, 2752×1536) is retired and replaced by `hero.webp` (90 KB, 1920×1072) — a 98.6% reduction** on an image that is the homepage LCP candidate, the About page image, the Trust section image, and the `onerror` fallback for every service and area image.
- **Method, deliberately reusing the existing pipeline rather than building a new one**: the admin service-image upload cap was raised from 5 MB to 20 MB (`ServiceAdminInputModel.ImageFile`'s `[MaxFileSize]`, plus the matching help text on the Create/Edit views) because the 6.6 MB original could not otherwise pass validation. The PNG was then uploaded through the existing admin Services form so `ImageStorageService`'s SkiaSharp pipeline did the conversion — resize to 1920 wide since the source exceeded that threshold, then WebP at quality 80 — and the output was moved to `wwwroot/images/` root. `ConvertExistingJpgServiceImages` was left untouched at the time; it was removed shortly afterwards — see §3k.
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

Closes Pre-Sprint 4 Tier 1 item 3 (see §4) — started ahead of the client call, since none of this required business input. Full reasoning in `docs/private/VISION_AND_CONTEXT.md` §4/§5.12; this section records what shipped.

- **`ReviewSeeder` deleted outright** (not just unregistered) and its registration removed from `ApplicationDbContextSeeder.cs` — it seeded 5 generic e-commerce-filler reviews (fake `IsApproved = true` rows averaging 4.2 against the "4.9/5" claimed elsewhere) that had no purpose left once local submission is gone.
- **Local review submission removed entirely**: `ReviewsController` (public, held only the `Submit` action), `IReviewsService.AddReviewAsync`/`ReviewsService.AddReviewAsync`, and `ReviewInputModel` are all deleted; `ReviewsListViewModel.NewReview` is gone. `Home/Reviews.cshtml`'s form, star-rating JS, and validation partial are removed along with it — there is no longer a POST path that creates a `Review` row from the public site. `Admin`'s own approve/delete workflow (`Areas/Administration/Controllers/ReviewsController.cs`, `ReviewsService.ApproveReviewAsync`/`DeleteReviewAsync`) is untouched, since that's the intended path for whatever a future Business Profile API import lands (§4 Tier 3, not started).
- **"Verified Client" removed from all three display surfaces** (`Home/Index.cshtml`, `Home/Reviews.cshtml`, `Areas/Details.cshtml`) along with the sidebar claim *"Every review is manually verified by our support team"* — submission required no booking, no account, and no email, so the site was asserting a verification step it never performed.
- **Reviews page sidebar rebuilt as a Google CTA**, replacing the submission form: a "Loved our work?" card linking out to a new `Business:GoogleReviewsUrl` config key (`appsettings.json`, empty by default — deliberately not guessed, since no Business Profile exists yet, §4 Tier 3 item 13). `HomeController.Reviews()` now takes `IConfiguration` and passes the value through; the view shows the link when set, or a plain "coming soon" line when it's blank, so this becomes a one-line config edit once the profile exists rather than a code change (matches the "5-minute edit at the end" principle from `VISION_AND_CONTEXT.md` §7). The homepage's "Leave a Review" button is renamed **"Read All Reviews"** — it still routes to `/Reviews`, but the text no longer promises an on-site submission that doesn't exist.
- **Dead CSS removed alongside the markup**: `.reviews-trust-icon-box`, `.rating-selector-buttons`, `.star-btn`(`.star-active`), `.verified-badge`, and `.testimonial-role-label` — confirmed each had no other caller before deleting. `.info-bottom-action` was checked and kept — `Home/Terms.cshtml` still uses it.
- **Explicitly not touched**, per the existing scope split in §4: the fabricated `4.9`/`2.4k Verified Reviews` stat pills on `Home/Reviews.cshtml` and the `12k+ Jobs Completed`/`4.9/5 Rating`/`Based on 2,500 reviews` trust-stats card on `Home/Index.cshtml` — those are Tier 3 item 16, blocked on real business facts from the client call, not this item.
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors); full suite now 46 service-layer (one test removed, `AddReviewAsyncShouldAddUnapprovedReview`, since the method it covered no longer exists) + 9 web-integration, all passing. Live local run + Playwright-driven screenshots of Home, Reviews, and an Areas details page confirmed no "Verified Client" text anywhere, the submission form is gone, the Google CTA renders its "coming soon" fallback correctly with an empty config value, and review cards still render normally.
- **Resolved 2026-07-31**: the local dev database had the 5 old seeded rows (orphaned once the seeder was deleted) plus a manually-added test row (`"test" / "TestTest"`) from earlier QA of the submission form. The user deleted all of them via `Areas/Administration/Reviews` once the bug in §3o was fixed — confirmed gone, no reviews remain in the local dev database.

**Follow-up the same day, on-site display hidden entirely pending the Google API import**: showing a "be the first" empty state (or, worse, a stray Admin-approved row) still implied on-site reviews are a live feature, which they no longer are per the agreed strategy. Rather than rely on "zero rows happens to look empty," on-site review display is now gated behind an explicit `Business:ShowOnSiteReviews` config key (`appsettings.json`, default `false`) so it can't accidentally reappear before the Google Business Profile API import is actually built (§4 Tier 3, not started) — flipping one value turns it back on everywhere at once, same "5-minute edit later" pattern as `GoogleReviewsUrl`.
  - `HomeIndexViewModel`, `ReviewsListViewModel`, and `ServiceAreaDetailsPageViewModel` each gained a `ShowOnSiteReviews` bool; `HomeController` (`Index`, `Reviews`) and `AreasController` (`Details`, which now also takes `IConfiguration`) read the config value once and skip the `GetLatestApprovedAsync` call entirely when it's off, rather than fetching data that won't render.
  - **Home/Index**: the whole "What Our Customers Say" testimonials band (including the "Read All Reviews" button) is hidden when off.
  - **Reviews page**: the fabricated `4.9`/`2.4k Verified Reviews` stat pills and the review-feed grid are hidden when off — bundling their removal in here rather than waiting on Tier 3 item 16, since hiding fabricated numbers needs no real data, unlike replacing them. The "Loved our work?" Google CTA becomes the page's sole content, re-centered as a standalone card (was built as a sidebar next to the feed) rather than left stranded next to empty space.
  - **Areas/Details**: already hid itself once `Reviews.Any()` was false; added the same toggle check for defense-in-depth so a stray Admin-approved row can't leak onto an area page before the toggle is deliberately flipped.
  - **The Reviews page and its nav/footer link stay reachable on purpose** — confirmed with the user — since the centered CTA doubles as a "reviews are coming soon" signal rather than a dead link.
  - **Verified**: `dotnet build` (0 errors) and the full suite (46 + 9, unchanged) after this change; live local run + Playwright screenshots confirmed all three surfaces render nothing review-related with the default (off) config, and the Reviews page shows a single, non-redundant "coming soon" message.

---

## 3o. Bug Fix: Admin "Delete Review" Silently Did Nothing (2026-07-31)

Found while the user tried to delete the leftover fake/test review rows through the admin panel per §3n's follow-up — the Delete button appeared to have no effect no matter how many times it was clicked.

- **Root cause**: `EfDeletableEntityRepository<T>.Delete()` performs a soft delete (`IsDeleted = true`, `DeletedOn` set) — the standard pattern everywhere in this app. But `ReviewsService.GetAllAsync` (which powers the Admin Reviews list) queried `AllWithDeleted()` (`IgnoreQueryFilters()`), so a "deleted" row kept reappearing in the list immediately on the next page load, with no visual indicator that anything had changed — `ReviewViewModel` doesn't even expose `IsDeleted`. `ApproveReviewAsync` had the same `AllWithDeleted()` call.
- **Traced through git history**: `AllWithDeleted()` was there from the very first commit that created `ReviewsService` (`6278115`), before any sorting/filtering existed. The Sprint 3 commit that added sorting/status-filtering (`37b43af`) extended that same query with `OrderBy`/`Where` logic without reconsidering its source — it inherited the bug rather than introducing it. No test or manual QA pass ever specifically exercised the Delete button; the Sprint 3 verification note for that feature covered only sorting, filtering, and the summary cards.
- **Fix**: both `GetAllAsync` and `ApproveReviewAsync` now query `All()` instead of `AllWithDeleted()`, so a soft-deleted review actually disappears from the admin list and can't be re-approved after deletion — consistent with how every other soft-deleted entity in the app behaves, and with the fact that this feature has no restore/undelete UI to justify reaching past the filter.
- **Regression test added**: `DeleteReviewAsyncShouldRemoveReviewFromGetAllAsyncResults` (`ReviewsServiceTests.cs`) — deletes a review, then asserts `GetAllAsync` no longer returns it. This is the test that would have caught the original bug.
- **Verified**: `dotnet test` on `HandyFix.Services.Data.Tests` — 47 passing (was 46, plus the new regression test). The Web test project wasn't rebuilt this pass since it has no code changes here and Visual Studio had the Web project's build output locked via a running IIS Express session at the time.
- **Confirmed fixed by the user 2026-07-31**, live in the admin panel: Delete now actually removes a review from the list, no longer requiring a workaround.

---

## 3p. Broken Links & Metadata Cleanup (2026-07-31)

Closes Pre-Sprint 4 Tier 1 item 5 (see §4).

- **`Services/Index.cshtml`'s `quality-proof-image`** pointed at `/images/handyfix-proof.jpg`, which has never existed on disk — it always fell through to an external `gstatic.com` placeholder SVG (a third-party dependency for what's meant to be a trust-building image). Now points directly at `/images/hero.webp`, the sitewide fallback image already used the same way by `Home/Index.cshtml` and `Services/Category.cshtml`; the dead `onerror` chain was removed rather than kept pointed at a file that doesn't exist.
- **`Home/Index.cshtml`'s `LocalBusiness` JSON-LD** carried an `"image": "https://handyfix.co.uk/images/logo.png"` entry — `logo.png` doesn't exist and won't until the rebrand name lands and the wordmark is built (Tier 3 item 15). Removed rather than guessed at; add it back when the real wordmark exists.
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors).

---

## 3q. Email Provider Swap: SendGrid → Brevo (2026-07-31)

Closes Pre-Sprint 4 Tier 1 item 7 (see §4). Reasoning in `docs/private/VISION_AND_CONTEXT.md` §5.11 — Twilio retired SendGrid's permanent free tier, leaving a $19.95/month minimum for capacity HandyFix will never use; Brevo's free tier (300 emails/day, permanent, no card) comfortably covers the real volume (roughly two emails per booking).

- **New `BrevoEmailSender`** (`HandyFix.Services.Messaging`) implements `IEmailSender` with a direct `HttpClient` POST to Brevo's transactional email API (`https://api.brevo.com/v3/smtp/email`, `api-key` header) — no new NuGet dependency, since the request/response bodies are plain JSON handled with `System.Text.Json` (already in the BCL). `SendGridEmailSender` is deleted outright, along with the `Sendgrid` package reference (`HandyFix.Services.Messaging.csproj`, `Directory.Packages.props`) — this is a swap, not a dual-provider abstraction.
- **One correctness fix picked up along the way**: `SendGridEmailSender` only `Console.WriteLine`'d the response status/body and never checked it, so a failed send (bad recipient, suspended account, quota exceeded) was silently swallowed. `BrevoEmailSender` checks `response.IsSuccessStatusCode` and throws with the response body on failure, consistent with the "fail loud" pattern already used for Stripe and the missing-key checks (§5 below).
- **DI registration** (`Program.cs`) now reads `Brevo:ApiKey` instead of `SendGrid:ApiKey`, same fail-loud-outside-Development fallback to `NullMessageSender`. `BookingsService`/`PaymentsService` are untouched — both already depend only on `IEmailSender`, never the concrete sender.
- **`README.md`** updated: the documented `dotnet user-secrets set` command is now `Brevo:ApiKey` (Brevo keys are prefixed `xkeysib-`), and the fail-loud paragraph now names Brevo.
- **Not done here, operational not engineering**: creating the actual Brevo account and verifying a sender domain — needed before `Brevo:ApiKey` can be set for real in any environment. The `bookings@handyfix.co.uk`/`no-reply@handyfix.co.uk`/`admin@handyfix.co.uk` sender addresses are unchanged and will need revisiting alongside the rebrand (Tier 3 item 10), same as they would have under SendGrid.
- **Verified**: `dotnet build src/HandyFix.sln` (0 errors, no orphaned `Sendgrid` reference). Not live-tested against a real Brevo account — no API key exists yet (see above) — so this is verified by build/compile correctness, not an actual send.

---

## 3r. Technicians Decoupled from Capacity Slots; Admin Technician Roster (2026-07-31)

Closes Pre-Sprint 4 Tier 1 item 8 (see §4) — but with a **larger and different scope than that item described**, decided with the user this session. The item as written asked for a technician picker on the Calendar's slot-generation form. That was rejected in favour of removing technicians from slots altogether: technicians are fluid (largely self-employed contractors) and must not dictate booking capacity. A slot is business capacity; the technician enters the picture only once there is a real `Booking`.

### The model change: `AvailabilitySlot.TechnicianId` is gone

- **Column, FK, index and nav property all dropped** (migration `20260731200235_RemoveTechnicianFromAvailabilitySlot`, with a working `Down`). `TechnicianId` now lives on `Booking` and nowhere else.
- **This removes a whole class of bug rather than patching it.** Two nullable columns held one fact, and they were already drifting: `BookingsService.AssignTechnicianAsync` wrote only `Booking.TechnicianId`, leaving the slot's copy stale. The originally-planned fix was to sync them; deleting the duplicate makes the sync structurally unnecessary. Same reasoning as §3k — delete rather than repair, once the purpose is gone.
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

- `ITechniciansService`/`TechniciansService` + `TechniciansController` + Index/Create/Edit views and a `_TechnicianForm` partial, following the §3m Service Areas pattern throughout. Sidebar entry added.
- **Soft delete here, the opposite of §3m's hard delete, for the opposite reason.** A technician squats on no unique index, but `Booking.TechnicianId` is a live FK pointing at real job history. `DeleteAsync` therefore **refuses when any booking references the technician** and returns `false`, and the admin is told to deactivate instead; the Index hides the delete button entirely for anyone with bookings. Deleting is only for a row created in error.
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
- **Not verified visually** — the admin pages are auth-gated and this session drove them over authenticated HTTP rather than a browser, same as §3m. Layout risk is low: the new views reuse existing admin classes throughout, with no new CSS.
- **Leftover to clean up**: live verification left a soft-deleted `Zapryan Petrov` test row in the local dev `Technicians` table. It is invisible to the app (global query filter) and harmless; removing it needs a manual `DELETE` against the dev database.

---

## 4. Current Standing & Remaining Roadmap

### Pre-Sprint 4 TODOs — resequenced by launch-blocking priority (updated 2026-07-30)

> Supersedes the original chronological ordering (items 1–10, first logged 2026-07-25). The business decisions behind every item below — and the full reasoning — live in `docs/private/VISION_AND_CONTEXT.md` §5–6; this section tracks only what's left to *build*. Target launch is aggressive: roughly 4 weeks out from 2026-07-30. Ordering follows one rule now — **what's actually on the critical path to launch, not the order things were discovered in.**

**Audit findings that drove these decisions** (verified against the code on 2026-07-25, still accurate):
- `wwwroot/images/services/` holds 23 files but only **16 unique images** — the original Gemini run hit a rate limit and left 7 byte-identical duplicates across four groups: `door-repairs`/`furniture-assembly`/`minor-home-repairs`; `curtain-and-blind-fitting`/`handyman-category`/`painting-touch-ups`/`property-maintenance`; `tv-mounting`/`wall-mounting`; `minor-electrical-tasks`/`shelf-installation`. Plus one junk file, `test-image-from-admin-edit-hero.webp` (670×458, gitignored by `**/wwwroot/images/**/test*`).
- **Every existing image has a garbled, inconsistent "HandyFix" logo rendered on the technician's polo** — an AI text artifact that differs in every frame. The business is now confirmed rebranding (name TBD, see `VISION_AND_CONTEXT.md` §5.3), so the logo-free/text-free constraint on future artwork is a certainty now, not a hedge.
- The plumbing/handyman two-tone is currently **baked into the artwork pixels**. There is no division-scoped image tint anywhere in `wwwroot/css/` — every existing overlay (`.hero-bg-overlay`, `.bento-card-overlay`, `.service-hero-overlay`) is chromatically neutral. This is what motivates item 1's move to CSS.
- `wwwroot/images/areas/` does not exist; all 15 of its paths are already wired into the Areas feature with graceful `onerror` fallback to `/images/hero.webp` (was `hero.png` until §3j).
- **There is no `aggregateRating` / `Review` structured data anywhere in the repo.** The invented technicians, £5M insurance claim, and job counts are all in ordinary HTML markup on `Home/Index.cshtml` and `Services/Details.cshtml`. The JSON-LD's actual problem is fake NAP data (Tier 3 item 18).

---

#### Tier 0 — unlocks everything else (this week)

**0. The client call.** A single structured session with Zaprqn collecting everything Tier 2/3 below is blocked on. Confirmed happening this week (week of 2026-07-30). See `VISION_AND_CONTEXT.md` §6 for the canonical checklist. **Do not start Tier 2/3 work before this happens** — guessing at any of it means redoing it.

---

#### Tier 1 — no client input needed, start immediately in parallel

**1. Image generation batch.** Bulk-generate and download images via a terminal script kept **outside the repo** — no committed generator project. The plumbing/handyman two-tone gets applied **via CSS later**, not baked into the generated artwork, so the tone can be retuned without regenerating anything.
  - **Provider: Google Gemini API** (2.5 Flash Image / "Nano Banana"), decided 2026-07-30 — real API access rather than the rate-limited consumer app that produced the original 16 keepers. Same model family, so new output should match their style without a visible seam.
  - **Budget: $30** (raised 2026-07-30 from the original $10–15 estimate), comfortably covers 30–60+ images at Gemini's per-image pricing with room for retries — and now also covers whatever the Custom Projects category (Tier 2 item 12) turns out to need.
  - **Prompting strategy**: structured/JSON-style prompts (a fixed schema per shot — subject, style, lighting, camera angle, palette, negative prompt) to keep a large batch visually consistent, using the 16 existing keeper images and 4–5 client-supplied background photos as style anchors once shared. Full prompt-schema design is implementation-session work, not designed yet.
  - **The agreed workflow, end to end:** generate outside the repo → **convert to WebP outside the repo** (quality 80, to match what the app's own pipeline produces) → **name each file exactly per the conventions below** → copy the finished `.webp` files straight into `wwwroot/images/`. The app is not involved at any step: no admin upload needed, no startup conversion, and no database changes, because `ServicesSeeder.cs:79` already created `ServiceImage` rows pointing at the convention path `/images/services/{slug}-hero.webp`.
  - **Do not copy `.jpg` or `.png` into `wwwroot/images/`.** Nothing converts them any more (see §3k) — they will simply sit there while the views, which expect `.webp`, fall through to their `onerror` fallback. Failure is visible and harmless, but it wastes time.
  - No resize is needed on your side for service images: at 1024×1024 they are under the app pipeline's 1920px threshold, so it would not have resized them either. Only area heroes (1600×700) and any large marketing images need attention to dimensions.
  - The admin upload form remains available as an alternative for one-off replacements — it still does resize + WebP + correct naming automatically, and it is the path that produced `hero.webp` in §3j. It is just not the efficient route for a 40+ image batch.
  - Filename conventions are fixed and must be matched exactly: services `/images/services/{slug}-hero.webp` (hardcoded in four independent places — `ImageStorageService`, `ServicesSeeder.cs:79`, admin `ServicesController.cs:168`, and the `ServiceViewModel`/`ServiceDetailsViewModel` Mapster fallback); areas `/images/areas/{slug}-hero.webp` (resolved by convention at the view layer — `ServiceArea` deliberately has no image column). Note the two category tiles break the pattern: `{slug}-category-hero.webp`.
  - Target dimensions the markup already declares: service/category 1024×1024, area heroes 1600×700. (The coverage map is no longer an image asset — see §3l.)
  - **New scope, folded into the same batch rather than a second round**: whatever the Custom Projects category (Tier 2 item 12) needs once scoped with Zaprqn — likely one category/service hero for full bathroom installs and one for full kitchen installs.

**2. Hero image handling.** ~~Done — see §3j.~~

**3. Reviews cleanup.** ~~Done — see §3n.~~ Engineering complete; the actual Google Business Profile URL still needs filling in once it exists (Tier 3 item 13).

**4. Area map.** ~~Done — see §3l.~~

**5. Broken links & metadata.** ~~Done — see §3p.~~

**6. Caching.** ~~Done.~~

**7. Email provider swap — SendGrid → Brevo.** ~~Done — see §3q.~~

**8. Manual per-slot technician assignment.** ~~Done — see §3r.~~ **Resolved differently than this item proposed**: rather than adding a technician picker to slot generation, technicians were removed from `AvailabilitySlot` entirely. Slots are pure capacity; assignment happens on the `Booking` after the customer has booked and paid. Auto-generation of slots from the public booking page was removed in the same pass, and a non-production-only capacity seeder added so fresh clones and staging still have a working booking flow.

---

#### Tier 2 — blocked on the client call (Tier 0)

**9. Real business facts.** Insurance certificate (coverage itself confirmed real by Zaprqn 2026-07-30 — see item 16 — exact figure/certificate still needed), real years trading, real phone number, real trading address, his photo and name. Feeds directly into Tier 3's NAP/stats work — nothing there can be finalized before this lands.

**10. Rebrand — the business name itself.** Confirmed happening at this week's call. The single highest-leverage fact in this tier: it unblocks Tier 3's Google Business Profile creation, domain decision, wordmark/logo (item 15), and final JSON-LD/NAP values (item 18) all at once — nothing in Tier 3 can start before it lands.

**11. Real technician roster.** Names and phone numbers for Zaprqn and his worker(s) — however many are actually going active at launch. **No longer a code change**: since §3r there is full admin CRUD at `/Administration/Technicians`, so this is now data entry through the UI. Note this was a hard prerequisite, not a convenience — `TechniciansSeeder` only inserts when the table is empty, so adding a second technician by editing the seeder would never have worked. The seeded placeholder (`John Doe / 07123456789`) should be edited into a real person or deactivated once real names land; it can't be deleted once it has bookings, by design.

**12. Custom Projects category scope.** New service category for launch — full bathroom installation, full kitchen installation. Needs Zaprqn's input on what he's actually delivered under this banner before, and what he wants to promote/rank for, before any `ServiceCategory`/`Service` rows or copy get written. Once scoped: standard new-category engineering (seeder rows, images per Tier 1 item 1, category page wiring) — small, once the scope question is answered.

---

#### Tier 3 — blocked on Tier 2 outputs landing

**13. Google Business Profile creation & verification.** Cannot start until the business name (item 10) is settled. Start the moment it lands — verification (commonly postcard-based, 1–2+ weeks in transit) is the single longest lead time on the whole launch-blocker list and shouldn't wait behind other Tier 3 work.

**14. Domain finalization.** Depends on the rebrand decision (item 10) — no point fully recovering the existing name.com account for a name that may be retired. Recovery path, if the existing domain is still wanted: confirm whether Zaprqn can log into name.com at all. If yes, the locked mailbox is likely a separate hosted-email product, resettable from the account's own control panel. If no — and the account's recovery email is itself unreachable (a common circular lock when the recovery address is hosted on the same domain) — the only path is name.com support with proof of ownership (invoice/receipt number, the payment card's last 4 digits, or ID matching the WHOIS registrant). Once back in: point the recovery email at a non-domain-hosted address (e.g. a personal Gmail) and enable 2FA with stored backup codes, so this can't recur. **Not a hard launch blocker** — staging, and initial production if needed, can run on the Hetzner-assigned hostname (§1) while this is sorted.

**15. Wordmark/logo.** Blocked on item 10. Build as SVG from the existing Outfit font + navbar cyan accent dot once the name lands — not AI-generated, which cannot render text reliably (the exact problem with the current garbled-logo images, item 1).

**16. Fake statistics → real or removed.** Replace fabricated figures across `Home/Index`, `Home/Reviews`, `Home/About`, `Services/Index`, `Services/Details`, `Services/Category`:
  - **Resolved 2026-07-30, no longer blocked**: `Up to £5M Public Liability insurance` → **"Comprehensive Public Liability insurance"**, confirmed genuine coverage (exact figure still pending, item 9) — `Home/Index.cshtml:238` keeps its existing "…covering every single visit" tail unchanged; `Services/Details.cshtml:217`'s shorter sidebar line and `Services/Details.cshtml:153`'s FAQ-prose version get the equivalent swap adapted to each sentence's shape, not a literal paste. The **"100% satisfaction guarantee"** bundled into that same FAQ sentence (`Services/Details.cshtml:153`) is confirmed **not real — remove it outright**, don't reword it.
  - **Still blocked on item 9 (real facts)**: `12k+ Jobs Completed`, `4.9/5 Rating`, `Based on 2,500 reviews` (`Home/Index.cshtml:251,262,263`); `4.9` + `2.4k Verified Reviews` (`Home/Reviews.cshtml:96,109-113`); `4.9/5 Rating` + `Over 1,200 services completed` (`Services/Details.cshtml:170,173`); `4.9/5 Average Rating` (`Services/Index.cshtml:95`); `5,000+ Successful Fixes`, `15+ Specialist Techs`, `Crafting Quality Since 2018` (`Home/About.cshtml:14,48-53`); `3 Active Technicians Nearby` (`Services/Category.cshtml:140`). The counts contradict each other (12k+ vs 5,000+ vs 1,200 jobs; 2,500 vs 2.4k reviews vs 5 rows in the database), and most are rating/review claims that no longer make sense once Reviews goes GBP-link-only (item 3) — expect most to become the honest, already-identified substitutes rather than real numbers: *"Direct to your technician — no call centre"*, *"Covering 15 areas from Chessington"* (real `ServiceArea` rows), *"Fixed hourly rates, quoted upfront"* (real `BasePrice`), *"Pay securely online — deposit only"* (real Stripe integration).
  - Worth noting on the upside, still true: no Gas Safe, NICEIC, TrustMark, Which? or Checkatrade badges appear anywhere — the highest-severity fabrication category (Gas Safe numbers are legally regulated) is clean.

**17. Technicians — public-facing bio block.** Remove the hardcoded `"David"` / `"Mark"` Razor variables at `Views/Services/Details.cshtml:29-34` — invented names, roles, boroughs, first-person bios, external placeholder avatar. Bind dynamically to the real `Technician` entity, seeded with the owner's actual name/experience once item 11 lands. Do **not** AI-generate a face here. Confirmed 2026-07-30: no public technician roster/grid UI is needed for now beyond this bind-to-real-data fix — that's a "grow into it later" feature. A small technician-detail card **in the booking confirmation email** is separate, small new work — the email already renders the technician's name as a plain list item (`PaymentsService.SendBookingConfirmationEmailsAsync`); turning that into a styled card with name/phone (photo only once item 9's photo lands) is the actual remaining task here.

**18. NAP consistency.** The site must state one identity before the Google Business Profile is claimed — blocked on items 9 and 10 landing together. Currently: phone `07123456789` (fake/sequential) appears in three JSON-LD blocks (`Home/Index.cshtml`, `Services/Details.cshtml`, `Areas/Details.cshtml`); `addressLocality` is `Croydon` on Home but `Chessington` on Area pages (Chessington is correct per the seeded drive-time data — 0 minutes, "Home Turf"); `streetAddress` is the non-address `"South London Dispatch Office"` with invalid partial postcode `"CR0 1XX"`; `sameAs` points at two probably-nonexistent social profiles — remove until real profiles exist. Bad NAP/social data actively harms local SEO and will conflict with the real profile once claimed.

---

#### Tier 4 — deployment (content-independent, runs in parallel with Tiers 1–3)

**19. Hosting & CI/CD.** Infrastructure is already provisioned — see §1 "Hosting & Infrastructure" for the full Hetzner architecture. Remaining work, none of it blocked on business facts:
  - Configure `appsettings.Staging.json`/`appsettings.Production.json` connection strings against the private DB IP (`<DB_PRIVATE_IP>` — see `docs/private/INFRASTRUCTURE.md`), with `Database.Migrate()` and seeding running automatically on startup (already the local-dev pattern — just needs pointing at the new hosts).
  - `.github/workflows/deploy-dev.yml` (push/merge to `dev` → staging) and `deploy-prod.yml` (push/merge to `main` → production), with a dedicated GitHub Actions SSH keypair and repo secrets (`DEV_SERVER_IP`, `PROD_SERVER_IP`, `SSH_KEY`, DB passwords).
  - **Staging should be reachable before real business facts exist.** Confirmed goal (2026-07-30): filling in Zaprqn's real data should be a 5-minute edit at the end, not a blocker for standing up staging and demoing on an actual phone. Use the Hetzner-assigned reverse-DNS hostname for the staging URL if the domain (item 14) isn't settled yet — free, works over HTTPS, no dependency on name.com.

### Sprint 3 — Admin & Polish — **CLOSED** (2026-07-24)
- ~~Admin panel list refinements (sortable/queryable "Order by" on Bookings/Enquiries/Reviews lists).~~ **Done** — Bookings in Section 3d, Enquiries in Section 3e, Reviews in Section 3f.
- ~~Usability enhancements across the admin area.~~ **Dropped** — stayed unscoped with no concrete items identified; user confirmed the list-refinement work above satisfies Sprint 3's usability goals and closed the sprint without further items here.
- ~~Admin-area inline-style cleanup (343 occurrences, deferred here from Sprint 2 to avoid mixing scope).~~ **Done** — see Section 3a below.

### Pricing Page Migration & Pricing System Integration — **CLOSED** (2026-07-24)
Next initiative after Sprint 3, not part of the original Sprint 4 plan below. See Section 3g for the full scope of what shipped (new `_PricingCard` partial, rebuilt Pricing page, Service Details integration, `.glass-card` dedupe fix). Confirmed complete — no further work planned under this initiative.

### Sprint 4 — Testing, Documentation & Deployment
- Comprehensive unit test coverage beyond what Sprint 1 required (controller-level tests, broader service coverage).
- Architecture documentation and a completed GitHub README (current README has "Architecture (coming soon)" placeholders and some inaccurate tech-stack claims to correct — see note in Section 1).
- CI/CD pipeline setup (`.github/workflows/` currently exists but is empty) — infrastructure now provisioned (§1 Hosting & Infrastructure), remaining work tracked as §4 Tier 4 item 19.
- Production deployment — see §4 Tier 4 item 19.

---

## 5. Architectural Decisions Worth Remembering

- **Optimistic concurrency needs a provider that actually enforces it.** EF Core's InMemory provider silently ignores both transactions and `RowVersion` concurrency checks — tests that need to prove rollback or double-booking rejection use Sqlite in-memory (`Microsoft.Data.Sqlite`, `DataSource=:memory:`, open connection kept alive for the test's duration), not InMemory.
- **`IDbQueryRunner.BeginTransactionAsync`** is the standard way to wrap multi-repository mutations atomically; nested `SaveChangesAsync` calls from different repositories sharing the same scoped `DbContext` automatically join the ambient transaction — no need to pass a transaction object around explicitly.
- **The "fail loud outside development, fall back safely inside it" pattern** is now used twice (Stripe key, Brevo key) and should be the default template for any future third-party integration key: never let a missing production secret silently degrade to mock/no-op behavior.
- **Capacity and assignment are separate concerns, and only one of them is a slot.** `AvailabilitySlot` is business capacity and carries no technician; `Booking.TechnicianId` is the single home of "who does this job", set by an admin after payment (§3r). Don't reintroduce a technician (or any other assignment-shaped field) onto the slot to make a query convenient — that duplication is exactly what caused the stale-copy and wiped-on-reschedule bugs §3r removed.
- **Read paths must not write.** `GetAvailableDatesAsync` used to generate 30 days of slots as a side effect of being read, so merely browsing the public booking page created capacity nobody had decided to offer. Generation is now only ever triggered deliberately — by an admin, or by the non-production capacity seeder. Treat any "get" that mutates as a bug, not a convenience.
- **`SlotUnavailableException`** exists specifically so controllers can distinguish "the resource you wanted is gone" from generic `InvalidOperationException` validation failures — reuse this pattern rather than string-matching exception messages.
- **`wwwroot/css/base/utilities.css`** (added in Sprint 2) holds the small, generic spacing/typography/opacity/radius classes shared across every page (`mb-*`, `fs-*`, `lh-*`, `opacity-*`, `rounded-*`, `icon-fill`, etc.) — check here before inventing a new one-off class or reaching for an inline `style=`. Anything page-specific still belongs in that page's own `pages/*.css` file.
- **The sitemap is generated, not static** — `SeoController.Sitemap()` queries categories/services live via `ICategoriesService`/`IServicesService` rather than hardcoding URLs, specifically so it can't go stale as services are added or removed through the admin panel. Follow the same approach for any future sitemap-like listing.
- **Commit hygiene**: this project follows Conventional Commits with a `type(scope): title` subject line followed by a `- ` bullet per notable change (not prose paragraphs) — see recent `git log` for the established style before writing commit messages.
