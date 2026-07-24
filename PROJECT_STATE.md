# HandyFix — Project State & Architecture Roadmap

> **Purpose**: This is the permanent architectural memory for HandyFix. It records what the system actually is (not aspirational template boilerplate), what's been built and verified, and what's left. Update it at the close of each sprint rather than letting it drift out of sync with the code.
>
> **Last updated**: 2026-07-24 (end of Sprint 2)

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
- **Local optimized WebP assets** under `wwwroot/images/`, used for *admin-curated* content: service category hero images. Pipeline is `ImageStorageService` (SkiaSharp-based): resizes uploads >1920px wide, re-encodes to WebP at quality 80, deletes legacy JPG/PNG on save. A one-time startup sweep (`ConvertExistingJpgServiceImages`, called from `Program.cs`) converts any leftover legacy JPGs in `wwwroot/images/services/` on app boot. **This pipeline is scoped only to `images/services/`** — it does not touch the homepage hero image or any future area/marketing images (see Sprint 2 gaps below).

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

## 4. Current Standing & Remaining Roadmap

### Images (carried over from Sprint 2 — needs real assets, not more engineering)
- Only 24 images exist (all under `wwwroot/images/services/`), not the ~50 originally assumed. More area/marketing images need sourcing before the site can lean on real photography site-wide.
- `hero.png` (6.6MB PNG) should be re-encoded to WebP and brought into a resize pipeline the way `images/services/` already is — that's an engineering task once someone confirms it's fine to touch the source file.
- `wwwroot/images/handyfix-proof.jpg`, referenced by `Services/Index.cshtml`, doesn't exist and needs to be sourced or the reference removed.
- Real business input still needed for the JSON-LD structured data (see Sprint 2 SEO notes above) before launch.

### Sprint 3 — Admin & Polish
- Admin panel list refinements (sortable/queryable "Order by" on Bookings/Enquiries/Reviews lists).
- Usability enhancements across the admin area.
- Admin-area inline-style cleanup (343 occurrences, deferred here from Sprint 2 to avoid mixing scope).

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
- **Commit hygiene**: this project follows Conventional Commits with prose-paragraph bodies (not bullet lists) — see recent `git log` for the established style before writing commit messages.
