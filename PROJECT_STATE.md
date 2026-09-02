# HandyFix — Project State & Architecture Roadmap

> **Purpose**: This is the permanent architectural memory for HandyFix. It records what the system actually is (not aspirational template boilerplate), what's been built and verified, and what's left. Update it at the close of each sprint rather than letting it drift out of sync with the code.
>
> **Last updated**: 2026-09-01 — **First sitting of the Tier 0 client call: the rebrand name and domain are resolved.** Name is "Plumbing Handyman Surrey," domain bought fresh (`plumbing-handyman-surrey.co.uk` + `.com` backup via `names.co.uk`), and the public liability insurance figure question is closed for good (no number will ever be published, not merely still pending). No code changed — see Section 3ah for the full record, including what's still open (years trading, phone, address, his photo/name, worker roster, Custom Projects scope, jobs count) and a newly diagnosed staging bug (no bookable slots — `DevelopmentCapacitySeeder` needs the container restarted to top up capacity, unrelated to the call). (Previous update, 2026-08-05: **Test coverage thickened for `CategoriesService`/`ServicesService` (20 new tests), which surfaced and fixed a latent test-infrastructure gap**: `HandyFix.Services.Data.Tests` had never called `MappingConfig.RegisterMappings`, so every custom Mapster mapping (`IHaveCustomMappings`) had silently never run in this test project — invisible until today's first assertion on a custom-mapped property (`CategoryViewModel.BasePrice`) came back `0`. Fixed with a `[ModuleInitializer]` in a new `MappingTestSetup.cs` so the whole project now runs against the real mapping config. That fix immediately un-masked three more failures in pre-existing `BookingsServiceTests`, all from the same root cause plus a separate EF Core InMemory limitation (it doesn't short-circuit a ternary over a correlated collection, so `Payments.First()` throws on bookings with zero payments — a normal state, not a bug); fixed by moving that test helper to Sqlite in-memory, the same treatment already used elsewhere in the suite for InMemory's known gaps. **Purely test-side — no production code changed, and nothing here was ever live-reachable.** Full suite now 150/150. See Section 3ag. (Earlier the same day: **Live client-side validation was silently broken app-wide, not just on Booking** — jQuery was pinned to 4.0.0, which removed `$.parseJSON`; the bundled `jquery-validation-unobtrusive` still calls it when displaying any error, so every form's validation crashed silently the moment it tried to show a message (the same "unrelated" `parseJSON` console warning already noted from the login page back in Section 3f — it wasn't unrelated). Fixed by pinning jQuery back to 3.7.1, the version the validation library is actually built against; verified live on Booking and Contact that real DataAnnotations-driven messages now render correctly as the user types/blurs. Booking also got a plain visible hint for service/time-slot selection specifically, since those are custom widgets with no typed input for a validation message to attach to. One approach was tried and reverted: routing the submit button through jQuery's own `.valid()` called on every keystroke looked more "correct" but empirically corrupted jQuery Validate's internal per-field state — kept the simpler original presence check instead. See Section 3af. (Earlier the same day: **Live mobile-testing pass on staging surfaced three real bugs, all fixed and deployed same day**: a horizontal-scroll/zoom-drift bug on mobile (Home's Trust-section decorative blur circles bled past an unclipped container; fixed by extending `overflow-x: hidden` to `html` as well as `body` in `reset.css`, verified via Playwright at 375px/320px — Booking's reported footer break could not be independently reproduced), a booking form that silently reloaded with zero feedback whenever a photo upload failed (two-layered root cause — CloudflareR2 wasn't configured on staging at all, and separately `Booking/Index.cshtml` had no `asp-validation-summary` to render *any* page-level error even once one exists — both fixed), and CloudflareR2 itself wired up for staging (reuses the existing dev bucket by decision, real credentials added to the server's `.env` over SSH with a backup taken first, recorded in `docs/private/INFRASTRUCTURE.md`, never in a tracked file). **A live retest by the user then surfaced a second, deeper bug**: the R2 error persisted because `deploy-dev.yml` never syncs `docker-compose.staging.yml`/`Caddyfile` from the repo to the server — only the app image is auto-deployed, the compose file is static, hand-placed config that silently drifted the moment it was edited in the repo. Fixed the same way as the `.env` gap (corrected file written to the server over SSH, old version backed up) and, more importantly, corrected `docs/WORKFLOW_DEPLOYMENT.md` and `docs/private/STAGING_RUNBOOK.md`, both of which shared the identical blind spot and never stated this — see Section 3ae. Deployed via the normal `dev` push, build+test+deploy all green. **Both confirmed working live on staging as of 2026-08-05**: a booking with a photo attached completes successfully end to end (the next `dev` push re-triggered the deploy automatically, no manual restart needed), and the mobile horizontal-scroll fix holds up on a real device. (Previous update, 2026-08-04: **Documentation accuracy audit: `PROJECT_STATE.md` verified against the actual code, six stale cross-references found and fixed** (a corrected controller→service reference, a rewritten Hosting section still describing staging as unwired, an outdated hardcoded-image-path count in two places, an unmarked SendGrid→Brevo bullet, and the matching fix in `docs/WORKFLOW_SERVICES.md`) — nothing in the code changed, see Section 3ad. (Earlier the same day: **Fixed: cookie consent never actually persisted on staging.** Root cause was one missing piece of infrastructure config — no `UseForwardedHeaders()` — so the app behind Caddy always read requests as plain HTTP regardless of what the browser used. That silently broke two things at once: the consent cookie needed (but never got) `Secure` because it was set `SameSite=None`, so browsers discarded it every time, reshowing the banner on every page; and separately, `PaymentController`'s Stripe success/cancel URLs (built from `Request.Scheme`) would have pointed at `http://` on a real payment. Fixed both with one change — added correctly-scoped `ForwardedHeadersOptions` + `UseForwardedHeaders()` as the first middleware, and relaxed the cookie to `SameSite=Lax` so it no longer depends on Secure at all. Verified live by forging an `X-Forwarded-Proto: https` header locally and confirming the generated cookie and sitemap URLs (a stand-in for the Stripe URLs, built the same way) flip correctly — see Section 3ac.) (Earlier the same day: **Code cleanup pass complete, all 4 phases.** Phase 1: the CSS/slug Known Issues (Section 3y). Phase 2: production code moved from `var` to explicit types, with a new `.editorconfig` to keep it that way (Section 3z). Phase 3: eight controllers thinned — business logic, raw repository queries, and (most carefully) the Stripe SDK orchestration in `PaymentController` all moved into their services, following the pattern from the user's own earlier FindATrade project (Section 3aa). Phase 4: the unauthenticated `SettingsController` template leftover deleted outright, and `BaseController` flipped to `[Authorize]`-by-default with `[AllowAnonymous]` added only to the six controllers that must stay public (Section 3ab). 13 commits total, each independently built and tested; the Phase 3/4 items were verified live as well as by the test suite, not just by a green build.) (Earlier the same day: **Documentation caught up with the code.** `DESIGN.md` fully rewritten (was last touched 2026-07-16, before most of the front-end work landed) and the five remaining `docs/WORKFLOW_*.md` gaps (services & categories, technicians, reviews, enquiries, deployment) written, closing both open Sprint 4 documentation items and the README's "in progress" note — see Section 3x. No code changed; several real discrepancies found along the way (a fifth hardcoded `{slug}-hero.webp` location, two duplicate CSS class-name pairs, one dead CSS variable) are recorded as known issues, not fixed.) (Earlier the same day: **Staging is live.** Roadmap Tier 4 item 19 (Hosting & CI/CD) is done end-to-end: `dev` branch, `.github/workflows/deploy-dev.yml` (build, test, push to GHCR, SSH deploy), Caddy reverse proxy (HTTPS via the Hetzner reverse-DNS hostname, HTTP Basic Auth, `X-Robots-Tag: noindex`), and a dedicated least-privilege SQL login all provisioned and verified working against the real `handyfix_staging` database — see Section 3w. Two real bugs only surfaced by the first live CI runs, both fixed: `SqliteWebApplicationFactory` wasn't actually forcing Development for `AdminUserSeeder` (which reads the raw `ASPNETCORE_ENVIRONMENT` process variable, not the hosting abstraction `UseEnvironment()` sets), and the deploy script silently did nothing for two runs because `docker compose` couldn't find a non-default-named compose file and the script had no `set -e` to catch it.) (Earlier the same day: Docker containerization for Tier 4 item 19 started: `Dockerfile` + `.dockerignore` added and verified end-to-end against a throwaway local SQL Server container (empty database, exactly the state `handyfix_staging` is actually in) — build, `Database.Migrate()`, seeding, and a full booking-to-confirmation flow all succeed from a cold start. Found and fixed three things the local test surfaced: a Stripe sandbox-bypass opt-in (`Stripe:AllowSandboxOutsideDevelopment`) so staging can demo bookings before a real Stripe account exists, without weakening the same guard in production; and the four hardcoded `@handyfix.co.uk` email sender addresses (a domain nobody owns yet) made configurable since Brevo — like any real provider — refuses to send from an unverified sender. See Section 3v.) (Previous update, 2026-08-01: Agent workflow unified into a single root `AGENTS.md` with `CLAUDE.md`/`GEMINI.md` as pointers, resolving a real contradiction between two rulebooks; public-repo security audit run (no credential ever committed) and the unwritten `appsettings.Staging/Production.json` files gitignored before they can leak the private DB IP — see Section 3u. Also queued: bring `DESIGN.md` back in sync with the CSS that exists. Earlier the same day: controller test coverage shipped (Section 3t): 52 mocked-service controller tests, and the integration tests moved off the real dev database onto Sqlite in-memory, making them CI-viable. Suite is now 118. The `SQLitePCLRaw.lib.e_sqlite3` advisory (CVE-2025-6965) that came with the Sqlite dependency was fixed in the same pass, pinned to 2.1.12 in `Directory.Packages.props` — the vulnerability audit is now clean across all 14 projects, including one that was already affected beforehand. Earlier the same day: README accuracy pass (Section 3s) — the template's AutoMapper/MediatR/FluentAssertions claims corrected, the wrong service-area marketing block cut, and the Documentation placeholders replaced with real links; added a Sprint 4 item to document every remaining admin workflow and complete the README index once they exist, plus a new Tier 5 in the roadmap parking the dev-database QA leftovers. (Previous update, 2026-07-31: Tier 1 item 8 shipped, resolved differently than planned: technicians are decoupled from capacity slots entirely, slot auto-generation is removed, and there is now admin CRUD for the technician roster; see Section 3r. (Earlier the same day: Tier 1 items 3, 5 and 7 shipped — Reviews cleanup Section 3n, broken links Section 3p, Brevo email swap Section 3q.) (Previous update, 2026-07-30: Pre-Sprint 4 TODOs resequenced into launch-priority tiers after a full business-decisions session with the user (rationale logged in `docs/private/VISION_AND_CONTEXT.md` Section 5). Added a Hosting & Infrastructure subsection to Section 1 (Hetzner architecture, provisioned but not yet wired up) and three new TODO items: SendGrid→Brevo email swap, manual per-slot technician assignment, and the Custom Projects service category. Previous update, 2026-07-25: three of the original ten items had shipped — hero WebP migration Section 3j, cache-busting, and the Areas SVG coverage map Section 3l — plus the boot-time JPG sweep removed as unsafe Section 3k and Service Areas admin CRUD added Section 3m.)))))))

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

**12. Custom Projects category scope.** New service category for launch — full bathroom installation, full kitchen installation. Needs Zaprqn's input on what he's actually delivered under this banner before, and what he wants to promote/rank for, before any `ServiceCategory`/`Service` rows or copy get written. Once scoped: standard new-category engineering (seeder rows, images per Tier 1 item 1, category page wiring) — small, once the scope question is answered. **Booking mechanism, flagged 2026-08-03**: intended to route through an inquiry/quote-request flow rather than the standard slot-based instant-booking flow, since a multi-day install doesn't fit an hourly `AvailabilitySlot` — not yet confirmed with Zaprqn, see `docs/private/VISION_AND_CONTEXT.md` Section 5.13.

---

#### Tier 3 — blocked on Tier 2 outputs landing

**13. Google Business Profile creation & verification.** ~~Cannot start until the business name (item 10) is settled.~~ **Unblocked 2026-09-01 — start now.** Verification (commonly postcard-based, 1–2+ weeks in transit) is the single longest lead time on the whole launch-blocker list and shouldn't wait behind other Tier 3 work.

**14. Domain finalization.** ~~Depends on the rebrand decision.~~ **Done 2026-09-01 — see Section 3ah.** The old name.com/Zap Construction recovery question is moot — a fresh domain was bought instead: `plumbing-handyman-surrey.co.uk` (primary) + `.com` (backup), via `names.co.uk`, Denislav's account, Zaprqn paid. **New follow-up, not yet done**: point the domain's DNS at the Hetzner staging box (currently reachable only via its free reverse-DNS hostname), and set up Zoho Mail (free tier, up to 5 branded addresses) — MX records + mailbox creation, chosen over the registrar's own paid email add-on.

**15. Wordmark/logo.** Blocked on item 10. Build as SVG from the existing Outfit font + navbar cyan accent dot once the name lands — not AI-generated, which cannot render text reliably (the exact problem with the current garbled-logo images, item 1).

**16. Fake statistics → real or removed.** Replace fabricated figures across `Home/Index`, `Home/Reviews`, `Home/About`, `Services/Index`, `Services/Details`, `Services/Category`:
  - **Resolved 2026-07-30, closed for good 2026-09-01**: `Up to £5M Public Liability insurance` → **"Comprehensive Public Liability insurance"**, confirmed genuine coverage (exact figure will not be published at all, per Zaprqn — not merely still pending, item 9) — `Home/Index.cshtml:238` keeps its existing "…covering every single visit" tail unchanged; `Services/Details.cshtml:217`'s shorter sidebar line and `Services/Details.cshtml:153`'s FAQ-prose version get the equivalent swap adapted to each sentence's shape, not a literal paste. The **"100% satisfaction guarantee"** bundled into that same FAQ sentence (`Services/Details.cshtml:153`) is confirmed **not real — remove it outright**, don't reword it.
  - **Still blocked on item 9 (real facts)**: `12k+ Jobs Completed`, `4.9/5 Rating`, `Based on 2,500 reviews` (`Home/Index.cshtml:251,262,263`); `4.9` + `2.4k Verified Reviews` (`Home/Reviews.cshtml:96,109-113`); `4.9/5 Rating` + `Over 1,200 services completed` (`Services/Details.cshtml:170,173`); `4.9/5 Average Rating` (`Services/Index.cshtml:95`); `5,000+ Successful Fixes`, `15+ Specialist Techs`, `Crafting Quality Since 2018` (`Home/About.cshtml:14,48-53`); `3 Active Technicians Nearby` (`Services/Category.cshtml:140`). The counts contradict each other (12k+ vs 5,000+ vs 1,200 jobs; 2,500 vs 2.4k reviews vs 5 rows in the database), and most are rating/review claims that no longer make sense once Reviews goes GBP-link-only (item 3) — expect most to become the honest, already-identified substitutes rather than real numbers: *"Direct to your technician — no call centre"*, *"Covering 15 areas from Chessington"* (real `ServiceArea` rows), *"Fixed hourly rates, quoted upfront"* (real `BasePrice`), *"Pay securely online — deposit only"* (real Stripe integration).
  - Worth noting on the upside, still true: no Gas Safe, NICEIC, TrustMark, Which? or Checkatrade badges appear anywhere — the highest-severity fabrication category (Gas Safe numbers are legally regulated) is clean.

**17. Technicians — public-facing bio block.** Remove the hardcoded `"David"` / `"Mark"` Razor variables at `Views/Services/Details.cshtml:29-34` — invented names, roles, boroughs, first-person bios, external placeholder avatar. Bind dynamically to the real `Technician` entity, seeded with the owner's actual name/experience once item 11 lands. Do **not** AI-generate a face here. Confirmed 2026-07-30: no public technician roster/grid UI is needed for now beyond this bind-to-real-data fix — that's a "grow into it later" feature. A small technician-detail card **in the booking confirmation email** is separate, small new work — the email already renders the technician's name as a plain list item (`PaymentsService.SendBookingConfirmationEmailsAsync`); turning that into a styled card with name/phone (photo only once item 9's photo lands) is the actual remaining task here.

**18. NAP consistency.** The site must state one identity before the Google Business Profile is claimed — blocked on items 9 and 10 landing together. Currently: phone `07123456789` (fake/sequential) appears in three JSON-LD blocks (`Home/Index.cshtml`, `Services/Details.cshtml`, `Areas/Details.cshtml`); `addressLocality` is `Croydon` on Home but `Chessington` on Area pages (Chessington is correct per the seeded drive-time data — 0 minutes, "Home Turf"); `streetAddress` is the non-address `"South London Dispatch Office"` with invalid partial postcode `"CR0 1XX"`; `sameAs` points at two probably-nonexistent social profiles — remove until real profiles exist. Bad NAP/social data actively harms local SEO and will conflict with the real profile once claimed.

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
