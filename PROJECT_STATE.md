# HandyFix — Project State & Architecture Roadmap

> **Purpose**: This is the permanent architectural memory for HandyFix. It records what the system actually is (not aspirational template boilerplate), what's been built and verified, and what's left. Update it at the close of each sprint rather than letting it drift out of sync with the code.
>
> **Last updated**: 2026-07-25 (Pre-Sprint 4 TODOs section added — image-generation, reviews/Google Business, and fabricated-trust-content cleanup all scoped and decided. TODO item 2 shipped: hero image migrated to WebP, 6.6 MB → 90 KB, see §3j. The boot-time JPG sweep was then removed as completed migration code carrying a data-loss bug, see §3k)

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

## 4. Current Standing & Remaining Roadmap

### Pre-Sprint 4 TODOs — agreed decisions, not yet implemented (logged 2026-07-25)

A planning pass on visual assets and pre-launch trust content. **Nothing below has been built yet** — this section exists so the decisions and their reasoning survive until the work is picked up. Items 3 and 7–10 are launch blockers; items 1–2 and 4–6 are quality/perf.

**Audit findings that drove these decisions** (all verified against the code on 2026-07-25):
- `wwwroot/images/services/` holds 23 files but only **16 unique images** — the original Gemini run hit a rate limit and left 7 byte-identical duplicates across four groups: `door-repairs`/`furniture-assembly`/`minor-home-repairs`; `curtain-and-blind-fitting`/`handyman-category`/`painting-touch-ups`/`property-maintenance`; `tv-mounting`/`wall-mounting`; `minor-electrical-tasks`/`shelf-installation`. Plus one junk file, `test-image-from-admin-edit-hero.webp` (670×458, gitignored by `**/wwwroot/images/**/test*`).
- **Every existing image has a garbled, inconsistent "HandyFix" logo rendered on the technician's polo** — an AI text artifact that differs in every frame. The business name may still change, so all future artwork must be logo-free and text-free.
- The plumbing/handyman two-tone is currently **baked into the artwork pixels**. There is no division-scoped image tint anywhere in `wwwroot/css/` — every existing overlay (`.hero-bg-overlay`, `.bento-card-overlay`, `.service-hero-overlay`) is chromatically neutral. This is what motivates decision 1's move to CSS.
- `wwwroot/images/areas/` does not exist; all 16 of its paths are already wired into the Areas feature with graceful `onerror` fallback to `/images/hero.webp` (was `hero.png` until §3j).
- **There is no `aggregateRating` / `Review` structured data anywhere in the repo.** Section 3's Sprint 2 SEO note describes the fabricated content as "JSON-LD" — that is imprecise. The invented technicians, £5M insurance claim, and job counts are all in ordinary HTML markup on `Home/Index.cshtml` and `Services/Details.cshtml`. The JSON-LD's actual problem is fake NAP data (item 10).

**1. Image generation strategy.** Bulk-generate and download images via a terminal script kept **outside the repo** — no committed generator project. The plumbing/handyman two-tone gets applied **via CSS later**, not baked into the generated artwork, so the tone can be retuned without regenerating anything.

  **The agreed workflow, end to end:** generate outside the repo → **convert to WebP outside the repo** (quality 80, to match what the app's own pipeline produces) → **name each file exactly per the conventions below** → copy the finished `.webp` files straight into `wwwroot/images/`. The app is not involved at any step: no admin upload needed, no startup conversion, and no database changes, because `ServicesSeeder.cs:79` already created `ServiceImage` rows pointing at the convention path `/images/services/{slug}-hero.webp`.
  - **Do not copy `.jpg` or `.png` into `wwwroot/images/`.** Nothing converts them any more (see §3k) — they will simply sit there while the views, which expect `.webp`, fall through to their `onerror` fallback. Failure is visible and harmless, but it wastes time.
  - No resize is needed on your side for service images: at 1024×1024 they are under the app pipeline's 1920px threshold, so it would not have resized them either. Only area heroes (1600×700) and any large marketing images need attention to dimensions.
  - The admin upload form remains available as an alternative for one-off replacements — it still does resize + WebP + correct naming automatically, and it is the path that produced `hero.webp` in §3j. It is just not the efficient route for a 40-image batch.
  - Filename conventions are fixed and must be matched exactly: services `/images/services/{slug}-hero.webp` (hardcoded in four independent places — `ImageStorageService`, `ServicesSeeder.cs:79`, admin `ServicesController.cs:168`, and the `ServiceViewModel`/`ServiceDetailsViewModel` Mapster fallback); areas `/images/areas/{slug}-hero.webp` (resolved by convention at the view layer — `ServiceArea` deliberately has no image column). Note the two category tiles break the pattern: `{slug}-category-hero.webp`.
  - Target dimensions the markup already declares: service/category 1024×1024, area heroes 1600×700, coverage map 1200×600.

**2. Hero image handling.** ~~Keep `hero.png`; re-upload it manually through the admin panel so the existing SkiaSharp pipeline compresses it and converts it to WebP, rather than adding a new pipeline for it.~~ **Done 2026-07-25 — see §3j.** Shipped as `hero.webp` (90 KB, 1920×1072, down from 6.6 MB), converted through the existing admin upload path after raising the upload cap to 20 MB. All 8 references repointed and the stale declared dimensions corrected.

**3. Reviews strategy.** **Remove local review submission entirely.** Point users at the Google Business Profile instead, and pull reviews back via the Google API later. **Do not add `Source`, `ExternalId`, or `BookingId` columns to `Review` yet** — deferred until the import is actually built.
  - Immediate cleanup this implies: delete the 5 seeded fake reviews (`ReviewSeeder`, unregistered at `ApplicationDbContextSeeder.cs:36`; the `if (dbContext.Reviews.Any()) return;` guard means the rows must also be removed from the live DB). They are generic e-commerce filler — *"delivery took a little longer than expected"*, *"fits the description perfectly"* — on a plumbing site, all seeded `IsApproved = true`, all sharing one `CreatedOn` timestamp, averaging 4.2 against the "4.9/5" claimed elsewhere.
  - Also remove the **"Verified Client" badges** (`Home/Index.cshtml:306`, `Home/Reviews.cshtml:160-163`, `Areas/Details.cshtml:160-163`) and the sidebar claim *"Every review is manually verified by our support team"* (`Home/Reviews.cshtml:84`) — submission requires no booking, no account and no email, so the site asserts verification it does not perform.
  - All three display surfaces already degrade gracefully with zero reviews (Home and Reviews show a "be the first" prompt; Areas hides its section), so removal needs no layout work.
  - Constraints for the future import: Google's structured-data policy **forbids marking up third-party reviews as your own `aggregateRating`**, and the Places API terms restrict caching review content. The correct route — since we will be managing the client's profile — is the **Business Profile API with owner OAuth**, not Places.
  - Sequencing note: real reviews should be collected on the Google Business Profile *first*, since that is where they compound for local SEO and what the map pack ranks on.

**4. Area map.** Replace the static `overview-coverage-map.webp` (`Views/Areas/Index.cshtml:37`) with an **inline interactive SVG map**. Town labels become real `<a href="/Areas/{slug}">` links to the 15 seeded area pages, using existing design tokens. Rejected AI generation for this: image models garble text and geography, and misspelled Surrey town names would actively undermine the local-SEO story. Removes the last remaining `onerror`-hides-the-section hack.

**5. Broken links & metadata.** Fix `handyfix-proof.jpg` (`Views/Services/Index.cshtml:105`) — the file has never existed on disk, so it always falls through to an external gstatic placeholder SVG. Remove the missing `logo.png` from the JSON-LD (`Views/Home/Index.cshtml:351`, `"image": "https://handyfix.co.uk/images/logo.png"`) rather than generating one, since the business name may change. When branding does settle, build the wordmark as SVG from the existing Outfit font and the navbar's cyan accent dot — not as AI output, which cannot render text reliably.

**6. Caching.** ~~Add `asp-append-version="true"` to the image tags that lack it.~~ **Done 2026-07-25.** Added to all six: the three `hero.webp` usages (`Home/Index.cshtml:11,248`, `Home/About.cshtml:31`), the two hardcoded category tiles (`Home/Index.cshtml:101,126`), and the dynamic area hero (`Areas/Details.cshtml:18`). Service images already had it.
  - **Deliberately skipped three tags.** `Areas/Index.cshtml:37` is being replaced wholesale by the inline SVG in item 4, so cache-busting a doomed tag is pointless churn. `Services/Index.cshtml:105` (`handyfix-proof.jpg`) and `Services/Details.cshtml:161` (the external gstatic placeholder) are both handled by items 5 and 9 — and the tag helper does not apply to absolute external URLs anyway.
  - The `hero.png` → `hero.webp` rename in §3j was self-busting because the URL itself changed. That will *not* be true of the bulk image drop in item 1, which overwrites files in place at unchanged paths — which is exactly why this was worth doing first.
  - Safe on files that do not exist yet: ASP.NET Core's `FileVersionProvider` returns the path unchanged when the file is missing rather than throwing, so the 15 not-yet-created area heroes render fine and pick up their version hash once the files land and the app restarts.

**7. Controller bug — review submission errors are invisible.** `ReviewsController` sets `TempData["ErrorMessage"]` on validation failure, but `Views/Home/Reviews.cshtml:22` only renders `TempData["SuccessMessage"]`. Because it is a redirect, the `asp-validation-for` spans are empty too — so a failed submission looks to the user like nothing happened at all. (Scope depends on item 3: if local submission is removed, this may disappear with it.)

**8. Fake statistics.** Replace fabricated figures with real, verifiable facts across six files — `Home/Index`, `Home/Reviews`, `Home/About`, `Services/Index`, `Services/Details`, `Services/Category`:
  - `12k+ Jobs Completed`, `4.9/5 Rating`, `Based on 2,500 reviews` (`Home/Index.cshtml:251,262,263`); `4.9` + `2.4k Verified Reviews` (`Home/Reviews.cshtml:96,109-113`); `4.9/5 Rating` + `Over 1,200 services completed` (`Services/Details.cshtml:170,173`); `4.9/5 Average Rating` (`Services/Index.cshtml:95`); `5,000+ Successful Fixes`, `15+ Specialist Techs`, `Crafting Quality Since 2018` (`Home/About.cshtml:14,48-53`); `3 Active Technicians Nearby` (`Services/Category.cshtml:140`).
  - The counts contradict each other (12k+ vs 5,000+ vs 1,200 jobs; 2,500 vs 2.4k reviews vs 5 rows in the database).
  - `Up to £5M Public Liability insurance` (`Home/Index.cshtml:238`, `Services/Details.cshtml:153,217`) — insurance is probably genuine, just likely not £5M.
  - Each removal needs honest replacement copy, not just deletion: a site showing zero reviews under "Based on 2,500 reviews" is worse than one with five bad ones. Defensible substitutes available from real data: *"Direct to your technician — no call centre"*, *"Covering 15 areas from Chessington"* (real `ServiceArea` rows), *"Fixed hourly rates, quoted upfront"* (real `BasePrice`), *"Pay securely online — deposit only"* (real Stripe integration).
  - **Blocked on client input:** real public liability figure + certificate, real years in business, real phone number, real trading address.
  - Worth noting on the upside: no Gas Safe, NICEIC, TrustMark, Which? or Checkatrade badges appear anywhere — the highest-severity category (Gas Safe numbers are legally regulated) is clean.

**9. Technicians.** Remove the hardcoded `"David"` / `"Mark"` Razor variables at `Views/Services/Details.cshtml:29-34` — invented names, roles, boroughs and first-person bios (*"I've spent 15 years servicing homes across Sutton, Croydon, and Epsom…"*), rendered with an external gstatic placeholder avatar. Bind the block dynamically to the real `Technician` entity instead (`TechniciansSeeder` already exists, currently seeding a placeholder `John Doe / 07123456789`), seeded with the owner's actual name, real experience and a real photo. This is also the platform-aligned fix — the long-term vision has many vetted technicians, so this block should have been data-driven from the start. Do **not** AI-generate a face here.

**10. NAP consistency.** The site must state one identity before the Google Business Profile is claimed. Currently: phone `07123456789` (fake/sequential) appears in three JSON-LD blocks (`Home/Index.cshtml`, `Services/Details.cshtml`, `Areas/Details.cshtml`); `addressLocality` is `Croydon` on Home but `Chessington` on Area pages; `streetAddress` is the non-address `"South London Dispatch Office"` with invalid partial postcode `"CR0 1XX"`; and `sameAs` points at two probably-nonexistent social profiles. Chessington is correct per the seeded drive-time data (0 minutes, "Home Turf"). Remove `sameAs` until real profiles exist. Bad NAP/social data actively harms local SEO and will conflict with the real profile once claimed.

---

### Images — **IN PROGRESS** (carried over from Sprint 2 — blocked on real assets, not more engineering)

> Superseded in part by the Pre-Sprint 4 TODOs above — items 1, 2 and 5 cover the strategy for closing this gap. The asset inventory below remains accurate.
- Only 24 images exist (all under `wwwroot/images/services/`), not the ~50 originally assumed. More area/marketing images need sourcing before the site can lean on real photography site-wide.
- ~~`hero.png` (6.6MB PNG) should be re-encoded to WebP and brought into a resize pipeline the way `images/services/` already is.~~ **Done 2026-07-25 — see §3j.** Now `hero.webp`, 90 KB / 1920×1072.
- `wwwroot/images/handyfix-proof.jpg`, referenced by `Services/Index.cshtml`, doesn't exist and needs to be sourced or the reference removed.
- Real business input still needed for the JSON-LD structured data (see Sprint 2 SEO notes above) before launch.
- `wwwroot/images/areas/` needs 16 new assets: one hero per area (`{slug}-hero.webp`, 15 areas — see Section 3h) plus `overview-coverage-map.webp` for the Areas index hero. All 16 paths are already wired into the Area pages' markup with graceful `onerror` fallback to `/images/hero.webp` in the meantime. Per Pre-Sprint 4 TODO item 4, the coverage map will be an inline interactive SVG rather than a raster asset, so only 15 files are actually needed here.
- **Not yet resolved** — pending the user supplying the physical asset files. Do not mark this item done until the files actually exist on disk.

### Sprint 3 — Admin & Polish — **CLOSED** (2026-07-24)
- ~~Admin panel list refinements (sortable/queryable "Order by" on Bookings/Enquiries/Reviews lists).~~ **Done** — Bookings in Section 3d, Enquiries in Section 3e, Reviews in Section 3f.
- ~~Usability enhancements across the admin area.~~ **Dropped** — stayed unscoped with no concrete items identified; user confirmed the list-refinement work above satisfies Sprint 3's usability goals and closed the sprint without further items here.
- ~~Admin-area inline-style cleanup (343 occurrences, deferred here from Sprint 2 to avoid mixing scope).~~ **Done** — see Section 3a below.

### Pricing Page Migration & Pricing System Integration — **CLOSED** (2026-07-24)
Next initiative after Sprint 3, not part of the original Sprint 4 plan below. See Section 3g for the full scope of what shipped (new `_PricingCard` partial, rebuilt Pricing page, Service Details integration, `.glass-card` dedupe fix). Confirmed complete — no further work planned under this initiative.

### Sprint 4 — Testing, Documentation & Deployment
- Comprehensive unit test coverage beyond what Sprint 1 required (controller-level tests, broader service coverage).
- Architecture documentation and a completed GitHub README (current README has "Architecture (coming soon)" placeholders and some inaccurate tech-stack claims to correct — see note in Section 1).
- CI/CD pipeline setup (`.github/workflows/` currently exists but is empty).
- Production deployment.

---

## 5. Architectural Decisions Worth Remembering

- **Optimistic concurrency needs a provider that actually enforces it.** EF Core's InMemory provider silently ignores both transactions and `RowVersion` concurrency checks — tests that need to prove rollback or double-booking rejection use Sqlite in-memory (`Microsoft.Data.Sqlite`, `DataSource=:memory:`, open connection kept alive for the test's duration), not InMemory.
- **`IDbQueryRunner.BeginTransactionAsync`** is the standard way to wrap multi-repository mutations atomically; nested `SaveChangesAsync` calls from different repositories sharing the same scoped `DbContext` automatically join the ambient transaction — no need to pass a transaction object around explicitly.
- **The "fail loud outside development, fall back safely inside it" pattern** is now used twice (Stripe key, SendGrid key) and should be the default template for any future third-party integration key: never let a missing production secret silently degrade to mock/no-op behavior.
- **`SlotUnavailableException`** exists specifically so controllers can distinguish "the resource you wanted is gone" from generic `InvalidOperationException` validation failures — reuse this pattern rather than string-matching exception messages.
- **`wwwroot/css/base/utilities.css`** (added in Sprint 2) holds the small, generic spacing/typography/opacity/radius classes shared across every page (`mb-*`, `fs-*`, `lh-*`, `opacity-*`, `rounded-*`, `icon-fill`, etc.) — check here before inventing a new one-off class or reaching for an inline `style=`. Anything page-specific still belongs in that page's own `pages/*.css` file.
- **The sitemap is generated, not static** — `SeoController.Sitemap()` queries categories/services live via `ICategoriesService`/`IServicesService` rather than hardcoding URLs, specifically so it can't go stale as services are added or removed through the admin panel. Follow the same approach for any future sitemap-like listing.
- **Commit hygiene**: this project follows Conventional Commits with a `type(scope): title` subject line followed by a `- ` bullet per notable change (not prose paragraphs) — see recent `git log` for the established style before writing commit messages.
