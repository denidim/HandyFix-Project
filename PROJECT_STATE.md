# HandyFix — Project State & Architecture Roadmap

> **Purpose**: This is the permanent architectural memory for HandyFix. It records what the system actually is (not aspirational template boilerplate), what's been built and verified, and what's left.
>
> **How it is organised**: this file is kept short enough to be read whole at the start of a session. Section 1 is the architecture. Sections 2 and 3 are the index of every finished piece of work; the full text of each entry is in [`docs/history/`](docs/history/), one file per month, so "Section 3cp" in a code comment, a test or a document names an entry there. Section 4 lists what is still open. Section 5 holds the decisions worth remembering. Update it with every change, not at the close of a sprint (`AGENTS.md` Section 5).
>
> **Last updated**: 2026-11-16 — **a laptop draws the page a wide monitor draws**: the public stylesheets' sizes are in rem and one rule shrinks them with the screen's width, and the home gallery strip is taller (Section 3ff). Before it, on 2026-11-14, the history moved out of this file into `docs/history/` (Section 3fe).

---

## 1. Tech Stack & Core Architecture

### Backend
- **ASP.NET Core MVC on .NET 10**, structured as Clean Architecture with strict layer separation:
  - `Data/` — `HandyFix.Data.Models` (entities), `HandyFix.Data.Common` (repository interfaces, base model classes), `HandyFix.Data` (EF Core `ApplicationDbContext`, migrations, repositories, seeders).
  - `Services/` — `HandyFix.Services.Data` (business logic services, one per domain area: Bookings, Availability, Payments, Services, Reviews, Inquiries, Categories, Gallery, SiteCounts), `HandyFix.Services.Mapping` (object mapping), `HandyFix.Services.Messaging` (email), `HandyFix.Services` (cross-cutting: image upload, R2 storage).
  - `Web/` — `HandyFix.Web` (MVC controllers, views, `Program.cs` composition root, background workers), `HandyFix.Web.ViewModels` (DTOs/view models, kept out of the Data layer).
  - `Tests/` — `HandyFix.Services.Data.Tests` (service-layer unit tests), `HandyFix.Web.Tests` (full-stack `WebApplicationFactory` integration tests).
- **Object mapping**: [Mapster](https://github.com/MapsterMapper/Mapster), via a custom `IMapFrom<T>`/`IMapTo<T>`/`IHaveCustomMappings` convention registered once at startup (`MappingConfig.RegisterMappings`). Services are plain classes with direct async methods, not CQRS command/query handlers — there is no AutoMapper and no MediatR anywhere in the codebase. *(The README used to claim both; corrected 2026-08-01, see Section 3s.)*
- **Repository pattern**: `IRepository<T>` / `IDeletableEntityRepository<T>`, generic EF Core-backed implementations (`EfRepository<T>`, `EfDeletableEntityRepository<T>`). Soft-delete (`IsDeleted`/`DeletedOn`) and audit fields (`CreatedOn`/`ModifiedOn`) are applied globally via `ApplicationDbContext.OnModelCreating`/`SaveChanges` overrides, not per-entity boilerplate.
- **Code style**: StyleCop analyzers run on every build (`src/Directory.Build.props`, `stylecop.json`). The build has no warnings, and in Release, which CI, staging and the live site build, a warning fails it (Section 3df).

- **postcodes.io** (free, no key or account), asked through `PostcodesIoLocator` when an admin saves an area: for where its first postcode district is, to place it on the coverage diagram (Section 3dm), and for a town's district from its name when the districts box is left empty (Section 3dn). Five seconds, then the admin is told. Tests never ask it.
- **OSRM** (the public route service at router.project-osrm.org: free, no key or account, no traffic), asked through `OsrmDriveTimeCalculator` once when an admin adds an area, for the drive time from Chessington (Section 3dn). Five seconds, then the time is estimated from the distance. Tests never ask it.

### Database
- **SQL Server**, run locally via a Docker container (`MSSQLServer`, port 1433) for development.
- **Entity Framework Core 10.0.13**, code-first migrations under `src/Data/HandyFix.Data/Migrations/`.
- All entities use **`Guid` primary keys** (`BaseModel<Guid>` / `BaseDeletableModel<Guid>`), generated client-side in entity constructors.
- **Centralized package management**: `src/Directory.Packages.props` (all NuGet package versions pinned in one place, `ManagePackageVersionsCentrally=true`) and `src/Directory.Build.props` (shared `TargetFramework=net10.0`, StyleCop analyzers, language version) — individual `.csproj` files only declare `<PackageReference Include="..." />` with no version.

### Storage & Static Assets
- **Cloudflare R2** (S3-compatible object storage) via `CloudflareR2Service`/`ICloudflareR2Service`, used for everything uploaded on a running site: booking problem photos and inquiry photos (`ImageService.UploadImagesAsync`, capped at 5 files / 15MB each, JPEG/PNG/WebP only), **since Section 3cq the service pictures the admin uploads** (`ImageStorageService`, SkiaSharp: made no wider than 1920px, WebP at quality 80, with a 320px copy, under a new name each time, `services/{random}_{slug}.webp`), **since Section 3dl an area's picture the admin uploads** (cut to 1600x700 from its middle, with a 928-wide phone strip, `areas/{random}_{slug}.webp`), and **since Section 3fb completed-work gallery photos the admin uploads** (EXIF orientation applied and EXIF metadata stripped on re-encode: at most `1600px` on the longest side in WebP at quality 82 under `gallery/{random}.webp`, plus a `640×480` 4:3 middle-cropped card copy under `gallery/{random}-640.webp`). The database keeps each picture's address; a rename moves nothing.
- **The site's own WebP pictures** under `wwwroot/images/`, made outside the app and committed: among them the forty built-in service pictures in `images/services/` with their 320px copies, each service's default. **The code never writes to `wwwroot`** (Section 3cq): a deploy starts a new container from what git holds, so anything written there was lost at the next deploy. There is no startup sweep over the images directory (see Section 3k). Assets produced outside the app are expected to arrive already WebP-encoded and correctly named; nothing converts them on boot.

### Frontend
- Razor views + Bootstrap 5 + vanilla JS. CSS is organized under `wwwroot/css/` as `base/`, `components/`, `pages/` files (not a single monolithic stylesheet), listed in load order in `SiteStylesheets.cs`. On the live site WebOptimizer combines and minifies them into one `/css/site.min.css` whose address carries a hash of its content; in Development each file is linked by itself (Section 3bs). A shared `page-container` class provides a consistent boxed hero/content width across every public page. Every size in the public stylesheets is in `rem`, and `base/scale.css` shrinks the whole page with the screen's width between 1920 and 1280 wide, down to 80%, so a laptop draws the page a wide monitor draws (Section 3ff); the admin panel does not scale.
- Design language documented separately in `DESIGN.md` (color palette, typography, spacing, animation conventions).

### Public Forms & Abuse Protection (Section 3cb)
- The three public forms (Contact, Join Our Team, Booking) share one set of checks, described in full in `docs/WORKFLOW_FORMS.md`: ASP.NET Core's rate limiter (`RateLimits`), the antiforgery token, the form's own rules (`HandyFix.Web.ViewModels/Validation/`), a hidden box and a signed time stamp (`FormGuard`, the `_FormGuard` partial), and Cloudflare Turnstile (`TurnstileVerifier`), whose script `site.js` fetches on a form's first touch, not with the page (Section 3cq). A new public form gets all of them by rendering `_FormGuard` and calling `IFormGuard` in its POST action.
- `ErrorsController` at `/StatusPage/{code}` shows the site's own page for 404, 429, 500 and any other error status, for a request that asked for a page. It takes no services on purpose.
- Every email that follows a save goes through `TrySendEmailAsync`, and everything a customer typed goes through `EmailText.Encode` before it reaches an email's HTML.

### Email & the Admin Account (Section 3cc)
- **Email**: every address the site sends from or to is a role address on the business's domain, never a person's; `docs/WORKFLOW_EMAIL.md` lists the seven emails and the settings per environment. **Every email has one look, built in `EmailLayout`** (Section 3cl), and **each kind of email's words are in a class of its own** in `Services.Data/Emails` (`BookingEmails`, `EnquiryEmails`, `AccountEmails`; Section 3di), put together from `EmailLayout`'s pieces. The service that sends an email decides when, and what is written on the job when it does not go. The logo they show is a file on the live site, `wwwroot/images/email/logo-header.png`, whichever copy of the site sends; a button that opens the admin panel takes its address from `Site:PublicUrl` (`EmailOptions.SiteUrl`), which staging sets to itself. Staging sends from the real address and marks its subjects through `SubjectPrefixEmailSender`.
- **An email that does not go is kept** (Section 3cq): a row in `EmailsNotSent` on the job or enquiry it was about, written by the service that tried to send it, in the same save as the job's history line. The admin dashboard lists the last 7 days' (`EmailsNotSentService`), and an enquiry's page shows its own. The reset link alone fails into the log only.
- **A copy of the site that is not the live one says so** (Section 3cq): `EnvironmentMark` puts a strip across the top of every page, the admin panel's too, and `[STAGING]` before the tab's title, wherever the environment is not `Production`.
- **The admin account** is the site's only account, described in `docs/WORKFLOW_ADMIN_ACCOUNT.md`. `AdminUserSeeder` makes it at the one start that finds no administrator, from `Admin:SeedEmail` and `Admin:SeedPassword`; after that the admin changes both on `/Administration/Account` (`AdminAccountService`) and gets back in through "Forgot password?".
- **Only six pages of the Identity area have a route** (`IdentityPages`); the rest of what the Identity UI package brings answer 404. A new Identity page has to be added to that list to be reachable at all.

### Jobs (Section 3ce)
- **Every job has a page** at `/Administration/Bookings`, described in `docs/WORKFLOW_BOOKINGS.md`: one booked on the website (`BookingSource.Website`: a slot, a £50 deposit, two emails) and one the admin writes in (`JobsService.CreateWrittenInJobAsync`: no slot, no deposit, no email). What the admin does to a job is in `JobsService`; the website's side of a booking, the lists and the calendar are in `BookingsService` (Section 3di). The admin reads "Jobs"; addresses and code keep "Booking".
- **A job's details are put right on a form of their own** (Section 3cf, `JobsService.EditDetailsAsync`): the name, the phone number, the email, the address, the service and what the job is. Its boxes and their rules are the ones "Write a job in" uses (`JobDetailsInputModel` and two shared partial views). The day and time are not on it; "Move" changes those.
- **What may be done to a job is asked in one place**, `HandyFix.Common/BookingRules`, by the page (which buttons) and again by the service (before it acts). **What a person reads is made in one place** too: `JobLabels` (the job: Waiting for deposit, Booked, Done, Cancelled, Abandoned; the money: Not paid, Deposit paid, Part paid, Paid in full, Deposit refunded; the first job label since Section 3ch), `BookingReference.Short` (eight characters), `UkTime.FromUtc` (saved moments as a UK clock shows them).
- **A job keeps its own day and hour** (`Booking.ScheduledStart`), so one that gave its slot back still says when it was for. **Its money is its payments**: the website deposit (`DepositPaid`), what the admin writes on it (`Completed`, with a `Method`), a deposit sent back (`Refunded`); nothing is stored that could be added up instead. **Its history** is `BookingHistoryEntry` rows, written through `JobHistory` by the code that makes each change.

### The deposit (Section 3cg)
- **The site believes Stripe, never the browser.** A website booking's £50 deposit is paid on Stripe's own page. The site hears of it twice, when the customer comes back and when Stripe calls the webhook (`api/payment/webhook`), and both go through `PaymentsService.ConfirmCheckoutAsync`, which asks Stripe whether that page was paid. `docs/WORKFLOW_BOOKINGS.md` has the steps.
- **`IStripeGateway` is the one place the site talks to Stripe**: open a page, ask about one, close one, read a message. The tests put a stand-in there, so none of them calls Stripe.
- **A payment page is closed at Stripe before its hour goes back on sale** (`PaymentsService.CloseCheckoutsAsync`): by the sweep that drops unpaid bookings, by an admin's "Cancel", and by "Retry Payment" before it opens another. Money that still arrives for a booking that is not waiting for it is written on the job and emailed to the company; it switches nothing back on.
- **What a customer without a paid booking sees is decided in one place**, `PaymentController.Cancel`: retry, no longer held, deposit received, or cancelled.
- **With no key the site pretends, in Development only.** Outside it a missing `Stripe:SecretKey` fails loudly, and the setting that allowed pretending elsewhere (`Stripe:AllowSandboxOutsideDevelopment`) must never be given to the live site.

### What visitors do (Section 3fa)
- **The site counts for itself; it uses no analytics service.** The pages' script (`site.js`) sends one word to `POST /count` (`CountController`) for a visit, the booking page opened, a free time picked, and a tap on Call or WhatsApp, each once for a page shown. A day's sum of each kind is one row of `SiteCounts` (`SiteCountsService`), and nothing else is kept: no address, no browser, nothing that tells one visitor from another. The script writes nothing to a visitor's browser and reads nothing kept there.
- **Whose browser is counted is decided in one place**, `SiteCounting`: not a program that reads pages, not the admin signed in, not a browser the admin panel has marked as the business's own (`AdministrationController` marks each browser that opens the panel; a button on the Dashboard puts one back), and not a request sent by a page of another site.
- **The admin Dashboard shows the sums** beside the website's bookings, the deposits paid and the enquiries sent, which it reads from the panel's own records.

### Testing
- **xUnit + Moq**, at two levels: service-layer tests (`HandyFix.Services.Data.Tests`) and controller tests (`HandyFix.Web.Tests/Controllers/`, added Section 3t — controllers constructed directly with mocked services, asserting on the returned `IActionResult`, no database).
- **`HandyFix.Web.Tests` also holds full-stack integration tests** driven through `SqliteWebApplicationFactory`, which boots the real `Program` against Sqlite in-memory rather than the configured SQL Server. Do not replace it with a bare `WebApplicationFactory<Program>`: that runs migrations and seeding against the developer's actual dev database and cannot run in CI at all (Section 3t).
- Tests run against **EF Core InMemory** for simple CRUD-style assertions, and **EF Core Sqlite (`:memory:`)** for anything that depends on real transactions or concurrency-token enforcement — InMemory silently no-ops both `BeginTransaction`/`Commit`/`Rollback` and optimistic-concurrency checks, so it cannot verify rollback or race-condition behavior. This distinction is load-bearing; don't "simplify" a Sqlite-backed test back to InMemory without checking why it was Sqlite first.

### Hosting & Infrastructure (provisioned 2026-07-30; staging half wired up 2026-08-04, see Section 3w)

> Real IPs are deliberately not in this file — see `docs/private/INFRASTRUCTURE.md` (gitignored) for the actual values behind the placeholders below.

- **Provider**: Hetzner Cloud, Helsinki region, in a project owned by the client's limited company since 2026-10-07 (Section 3bt; the servers were first built in the developer's personal account). **Three isolated VPS instances** (CX23 — 2 vCPU / 4 GB RAM / 40 GB NVMe each), one per tier, all attached to a private network (`handyfix-internal`, `10.0.0.0/16` — see the private file for real octets):
  - Database host (`<DB_PUBLIC_IP>` / `<DB_PRIVATE_IP>`) — Dockerized MS SQL Server 2022 (container `handyfix-sql`), memory-capped at 3 GB (`MSSQL_MEMORY_LIMIT_MB=3072`) to prevent OS starvation on a 4 GB host. `handyfix_staging` is live and in use, and `handyfix_prod` holds the live site's data since 2026-10-09 (Section 3cj). **Rebuilt 2026-10-09 (Section 3ci)**: Express edition (free and allowed for a live business; the first container ran Developer, which is for development and testing only), its data on a Docker volume and not inside the container, both databases on the SIMPLE recovery model, and a backup file of each written by SQL Server every night, 14 nights kept.
  - Staging web host (`<STAGING_PUBLIC_IP>` / `<STAGING_PRIVATE_IP>`) — **live**, running `web` + `caddy` via `deploy/docker-compose.staging.yml`, deployed automatically on every push to `dev` (Section 3w).
  - Production web host (`<PROD_PUBLIC_IP>` / `<PROD_PRIVATE_IP>`) — **running the site since 2026-10-09 (Section 3cj)**: `web` + `caddy` via `deploy/docker-compose.prod.yml`, deployed on every merge into `main`, against `handyfix_prod` with a login of its own. The real domain points at it. Open to the public since 2026-10-23 (Section 3cu); every page asked for a password until then.
  - Both web servers have Docker + Docker Compose installed with auto-start on boot.
- **Network isolation, deliberately**: web apps reach SQL Server exclusively over the private interface (`<DB_PRIVATE_IP>`) — database traffic never touches the public internet. Root password login is disabled fleet-wide; SSH access is public-key only. **The second half of this was not true until 2026-10-09 (Section 3ci)**: the site's own traffic did use the private interface, but the SQL container published its port on every interface, so the database also answered from the internet, from the day the server was made. A Hetzner Cloud Firewall on the database host now lets in SSH and nothing else; Hetzner's firewalls do not filter the private network, so the web hosts reach the database as before.
- **Backups**: Hetzner's automatic backups are on for all three servers and have been since they were bought (a daily copy of the whole disk, seven kept). The database host also keeps its own nightly backup files, described above.
- **Staging is done** (Section 3w): `.github/workflows/deploy-dev.yml` builds/tests/deploys on every push to `dev`; `Database.Migrate()`/seeding run automatically on startup against `handyfix_staging` via a dedicated least-privilege SQL login; config arrives as environment variables into the container (`deploy/.env` on the server), **not** `appsettings.Staging.json`/`appsettings.Production.json` files — that was the original plan here, revised once the Docker approach was built (see Section 3w).
- **Production is deployed** (Sections 3ci and 3cj, 2026-10-09): `deploy-prod.yml` builds, tests and deploys on every merge into `main`, with the production compose file and Caddyfile copied to the server each time. What is on the server by hand: its `.env`, Cloudflare's origin certificate and a deploy key of its own. **Launched 2026-10-23** (Section 3cu, Launch Sprint L4 item 24): the password and the `noindex` header are off the real domain.
- **Domain**: `plumbing-handyman-surrey.co.uk` + `.com` (Section 3ah), registered to the client's limited company since 2026-10-07 (Section 3bt). DNS for the `.co.uk` is on Cloudflare. Email is live there: Cloudflare Email Routing receives for the domain's addresses (it replaced the Zoho Mail plan), and Brevo's sender records are in place for what the site sends. **The website record points at the production host since 2026-10-09 (Section 3cj)**, through Cloudflare's proxy, with the SSL mode "Full (strict)", Cloudflare's origin certificate on the server and "Always Use HTTPS" on; `www` follows the bare domain and is redirected to it. Until then both pointed at the registrar's parked page. The `.com` is on Cloudflare too since 2026-10-23 and sends every visitor to the `.co.uk` with a permanent redirect (Section 3cx). Staging keeps running on Hetzner's free reverse-DNS hostname.

---

## 2 and 3. Completed Work: the Index of Every Entry

Every finished piece of work has an entry: what shipped, what was verified and how, and what was deliberately not done. The entries are in [`docs/history/`](docs/history/), one file per month, each under the heading it has always had (`## 3cp. ...`). A reference to "Section 3cp" in a code comment, a test or a document names that entry: find it below, then open the month's file at that heading. Entries 2 and 3 are the first two sprints, written before the lettered entries began.

### July 2026 and Before: [`docs/history/2026-07.md`](docs/history/2026-07.md)

- **2.** Completed Milestones — Sprint 1: Core Booking Engine & Payments
- **3.** Completed Milestones — Sprint 2: UI/UX & SEO
- **3a.** Completed Milestones — Sprint 3 (partial): Admin Inline-Style Cleanup & CSS Bugfixes
- **3b.** Availability Slots Logic Audit (2026-07-24)
- **3c.** Admin Layout Polish: Prominent Public-Site Link (2026-07-24)
- **3d.** Admin Bookings Index: Submitted-On Column, Sorting, and Status Filter (2026-07-24)
- **3e.** Admin Enquiries List: Sorting (2026-07-24)
- **3f.** Admin Reviews List: Sorting and Status Filtering (2026-07-24)
- **3g.** Pricing Page Rebuild: Stitch Design Migration (2026-07-24)
- **3h.** Our Areas: Dynamic Service-Area Pages (2026-07-24)
- **3i.** Public-Site UI Consistency Pass (2026-07-24)
- **3j.** Hero Image WebP Migration (2026-07-25)
- **3k.** Removal of `ConvertExistingJpgServiceImages` (2026-07-25)
- **3l.** Areas Coverage Map: Inline Interactive SVG (2026-07-25)
- **3m.** Admin CRUD for Service Areas (2026-07-25)
- **3n.** Reviews Cleanup: Remove Fabricated Trust Content (2026-07-31)
- **3o.** Bug Fix: Admin "Delete Review" Silently Did Nothing (2026-07-31)
- **3p.** Broken Links & Metadata Cleanup (2026-07-31)
- **3q.** Email Provider Swap: SendGrid → Brevo (2026-07-31)
- **3r.** Technicians Decoupled from Capacity Slots; Admin Technician Roster (2026-07-31)

### August 2026: [`docs/history/2026-08.md`](docs/history/2026-08.md)

- **3s.** README Accuracy Pass (2026-08-01)
- **3t.** Controller Test Coverage & CI-Ready Integration Tests (2026-08-01)
- **3u.** Unified Agent Workflow & Public-Repo Security Audit (2026-08-01)
- **3v.** Docker Containerization Started; Staging-Readiness Gaps Found and Fixed (2026-08-04)
- **3w.** Staging Is Live: Tier 4 Item 19 Complete (2026-08-04)
- **3x.** Documentation Sync: `DESIGN.md` Rewrite + 5 New `docs/WORKFLOW_*.md` Files (2026-08-04)
- **3y.** Known Issues Fixed: CSS Duplicates + the Slug Bug (2026-08-04)
- **3z.** Code Cleanup Phase 2: `var` → Explicit Types (2026-08-04)
- **3aa.** Code Cleanup Phase 3: Thin Controllers (2026-08-04)
- **3ab.** Code Cleanup Phase 4: Remove `SettingsController`; `[Authorize]`-by-Default (2026-08-04)
- **3ac.** Bug Fix: Cookie Consent Never Actually Persisted Behind Caddy (2026-08-04)
- **3ad.** Documentation Accuracy Audit: Cross-Reference Drift Caught and Fixed (2026-08-04)
- **3ae.** Bug Fixes: Mobile Horizontal-Scroll Overflow, Silent Booking-Form Failures, CloudflareR2 Wired Up for Staging (2026-08-05)
- **3af.** Bug Fix: Live Client-Side Validation Was Silently Broken App-Wide (2026-08-05)
- **3ag.** Test Coverage: `CategoriesServiceTests`/`ServicesServiceTests`, and a Latent Test-Infrastructure Gap Found Along the Way (2026-08-05)

### September 2026: [`docs/history/2026-09.md`](docs/history/2026-09.md)

- **3ah.** Tier 0 Client Call (First Sitting): Rebrand Name and Domain Resolved (2026-09-01)
- **3ai.** Site-Wide Visual Design Refresh: Body Texture, Glass-Morphism Consolidation, Photography Gaps Closed (2026-09-02)
- **3aj.** Homepage Popular Services Mobile UX Redesign: Strategy 4 (2×2 Compact Tile Grid) (2026-09-02)
- **3ak.** Service Catalog Refresh: Flat Hourly Pricing + 8 New Services (2026-09-08)
- **3al.** Category Card Price Display Fix and Flat-Rate Pricing Finalized (2026-09-08)
- **3am.** Small Building & Refurbishments Category & Quote-Based Routing (2026-09-08)
- **3an.** Plumbing Hourly Rate Raised to £90 (2026-09-08)
- **3ao.** Surrey-First Rebrand, Croydon/Mitcham Purge, and Small-Building Pricing/Insurance Copy Fixes (2026-09-08)
- **3ap.** Real "Local Expert" Added for Small Building & Refurbishments: Zap (2026-09-08)
- **3aq.** Navbar Rework: Category Links, Mobile Scroll Fix, Login Greeting Removed (2026-09-08)
- **3ar.** Small Building & Refurbishments: Site-Wide Hardcoded-Price Audit (2026-09-08)
- **3as.** Mobile Sticky CTA Stripped to a Bare Floating Button (2026-09-08)
- **3at.** Home Page Trust Stats Card Replaced with a "Join Our Team" Invitation (2026-09-08)
- **3au.** About Page Redesign: Variation 3 (Property Solutions Hub) Finalized (2026-09-10)
- **3av.** Mobile UX Fixes, Join Our Team Form, and Pricing Consistency Pass (2026-09-10)
- **3aw.** Follow-ups: Seeder Category Sync, Validation Partials, Essential TempData Cookie (2026-09-10)
- **3ax.** Contact Form Validates What the Visitor Typed; Categories from Real Data; QA Rows Removed (2026-09-10)
- **3ay.** Services Index Redesign with Home Division Cards (2026-09-11)
- **3az.** Footer Emergency Link Cleanup & Mobile Menu All Services Link (2026-09-11)
- **3ba.** Fix Mobile Hero Text Clipping on Services Pages (2026-09-11)
- **3bb.** Launch Planning: Audit Against the Launch Task List, Roadmap Reorganised into Launch Sprints (2026-09-21)
- **3bc.** Branding/Image-Gen Pipeline Built, Division Colour System Site-Wide, Category Page Background Prototyped (2026-09-22)
- **3bd.** Bright Image Regeneration: All 59 Site Images (2026-09-22)
- **3be.** Category Pages: Real Coverage Map Instead of the Placeholder (2026-09-22)
- **3bf.** Font Scale-Up: Nothing Under 14px on Public Pages (2026-09-22)
- **3bg.** Site Fixes: Registration Closed, Footer Areas, Clickable Card Pictures (2026-09-22)
- **3bh.** Visible Rename to "Plumbing Handyman Surrey" (2026-09-22)
- **3bi.** The Logo: Hand-Drawn SVG from the User's Artwork, Used Site-Wide (2026-09-23)
- **3bj.** Header Background: Watercolour Strip Behind the Logo (2026-09-23)
- **3bk.** Reviews Page: Made-Up Rating Figures Deleted (2026-09-23)
- **3bl.** L1 Copy Batch: Footer, Contact, FAQ, Category Text, Billing Rule, Dashes, Legal Pages (2026-09-23)
- **3bm.** Header Logo Smaller on Phones (2026-09-24)
- **3bn.** White Header, Smaller Desktop Logo and Print Styles (2026-09-24)
- **3bo.** Home Booking Widget: The Site's Calendar Instead of the Browser's Date Picker; Small Building Out of Its List (2026-09-24)
- **3bp.** Booking Page: Enquiry Button When No Slot Suits (2026-09-24)

### October 2026: [`docs/history/2026-10.md`](docs/history/2026-10.md)

- **3bq.** New Logo: Navy Shield Emblem with the Name Beside It (2026-10-01)
- **3br.** Service Cards: "Book Now" Carries the Service and Its Category (2026-10-01)
- **3bs.** Stylesheets: One Bundled, Fingerprinted File Instead of an `@import` Chain (2026-10-01)
- **3bt.** Hosting and Accounts Moved to the Client's Company; Zoho Mail Dropped for Cloudflare Email Routing (2026-10-07)
- **3bu.** Real Phone Number, WhatsApp, Email and Company Details from One Place; Made-Up Social Profiles Removed (2026-10-07)
- **3bv.** Values in Script Blocks Written as JSON, Not as HTML (2026-10-07)
- **3bw.** Booking Link: The Date Is Read by the Model Binder (2026-10-07)
- **3bx.** WhatsApp in the Phone Menu, the Service Sidebar and the FAQ Card (2026-10-07)
- **3by.** WhatsApp Beside Every Call Link; a Controller Test That Checked Nothing; Staging and Phone Checks (2026-10-07)
- **3bz.** General Handyman Service; Small Building in the Home Booking Box as a Quote; Booking Links for a Category with No Tab; Hero Highlight (2026-10-07)
- **3ca.** Service Order and "Popular" as Admin Fields; Six Faults Found on the Way (2026-10-08)
- **3cb.** Launch Sprint L3: Abuse Protection, Validation and Notifications; Nineteen Faults Found on the Way (2026-10-08)
- **3cc.** Email on Role Addresses; the Admin Account Changed and Recovered Without a Developer; Four Faults Found on the Way (2026-10-08)
- **3cd.** One Technician at Launch, Under a First Name Alone; a New Database No Longer Starts with "John Doe" (2026-10-08)
- **3ce.** The Job Book: Every Job Has a Page, Booked on the Website or Written In by the Admin; Thirteen Faults Fixed on the Way (2026-10-09)
- **3cf.** A Job's Details Can Be Put Right; Two Faults Fixed on the Way (2026-10-09)
- **3cg.** The Deposit Goes Through Stripe: Confirmed by Asking Stripe, Its Page Closed Before the Hour Goes Back on Sale; Nine Faults Fixed on the Way (2026-10-09)
- **3ch.** An Unpaid Website Booking Reads "Waiting for Deposit"; the Admin Pages Lose Their Made-Up Content; the Calendar Gets Day Arrows (2026-10-09)
- **3ci.** The Database Server Closed to the Internet and Rebuilt; the Production Pipeline Written; a Deploy No Longer Signs the Admin Out; a UK Clock on the Servers (2026-10-09)
- **3cj.** The Site Runs on the Live Server and Answers on the Real Domain, Behind a Password Until Launch (2026-10-09)
- **3ck.** The README Brought Up to Date, with the Hosting Diagram (2026-10-10)
- **3cl.** One Shared Look for Every Email the Site Sends (2026-10-10)
- **3cm.** Every Page in the Sitemap Names Its Own Address; the Category Pages Tell Search Engines What They Hold (2026-10-19)
- **3cn.** Pages Lighter on a Phone; an Uploaded Picture Can Be Read on the Servers (2026-10-19)
- **3co.** What Lighthouse Found About Accessibility on the Home, Booking and Category Pages (2026-10-19)
- **3cp.** The Code Items Left Before Launch, Looked Into, and What Was Decided (2026-10-19)
- **3cq.** The Code Items Decided in Section 3cp, Built (2026-10-19)
- **3cr.** The Launch Checklist Run on Staging and the Live Site; Three Faults Fixed (2026-10-23)
- **3cs.** The Job List's Filter No Longer Takes Seconds; the Calendar's Arrows Lead to the Nearest Day with Jobs (2026-10-23)
- **3ct.** The Line Under "Mark as Done" in the Owner's Words; Launch Sprint L4 Item 30 Dropped (2026-10-23)
- **3cu.** The Site Is Opened to the Public (2026-10-23)
- **3cv.** Google Search Console Set Up; the Sitemap Handed In (2026-10-23)
- **3cw.** The Pre-Launch Password's Two Lines Taken Off the Live Server (2026-10-23)
- **3cx.** The `.com` Sends Every Visitor to the `.co.uk`; the Old Database Container Deleted; Launch Sprint L4 Item 31 Dropped (2026-10-23)
- **3cy.** The Specialised Divisions Photos in Their Own Colours; Launch Sprint L5 Item 3 Closed (2026-10-23)
- **3cz.** Google's Phone Speed Test Above 90: the Fonts from the Site Itself, Lighter Copies of the Pictures (2026-10-23)
- **3da.** A Share Picture for Every Page Without One of Its Own (2026-10-24)

### November 2026: [`docs/history/2026-11.md`](docs/history/2026-11.md)

- **3db.** The Home Page's Hero Text Darker and Bolder; the Corner Tint Off Its Photo (2026-11-04)
- **3dc.** The Tab Icon Is the Full Coloured Emblem, Versioned (2026-11-04)
- **3dd.** The Cancellation Rules in the Booking Confirmation; the Technician Email Says the Technician Will Call (2026-11-05)
- **3de.** Buttons on the Reviews Page to Read Our Google Reviews and to Leave One (2026-11-05)
- **3df.** A Clean Build: No Warnings, and a Warning Now Fails the Release Build (2026-11-06)
- **3dg.** One Name Each for the Statuses, the Deposit and the Categories (2026-11-06)
- **3dh.** Faults the Code Audit Found, Put Right (2026-11-06)
- **3di.** Emails and the Admin's Job Actions in Classes of Their Own (2026-11-06)
- **3dj.** Page Scripts in Their Own Files, and Logic Out of the Views (2026-11-06)
- **3dk.** Typed Settings and Up-to-Date Packages (2026-11-07)
- **3dl.** An Area's Picture Uploaded in the Admin Panel; Smaller Names on the Coverage Diagram (2026-11-07)
- **3dm.** The Coverage Diagram Places a New Town by Itself (2026-11-07)
- **3dn.** A Plain Area Form for the Owner (2026-11-07)
- **3do.** The Boiler Service, with the Gas Safe Logo (2026-11-07)
- **3dp.** Invoices: One per Job, Downloaded or Emailed as a PDF, and a Month's List for the Accountant (2026-11-07)
- **3dq.** Due on Completion, Online Invoice Payment Link with Optional Tip, and Admin Job Workflow Guide (2026-11-08)
- **3dr.** An Invoice Paid Online Is Written on the Job by Itself; Cash and Late Payments in the Terms (2026-11-08)
- **3ds.** On a Done Job, the Invoice Before the Payment; the Paid Invoice by Itself After Add Payment (2026-11-08)
- **3dt.** A Written-In Job Emails the Customer Once a Technician Is Picked (2026-11-08)
- **3du.** On a Phone, the Booking Page's Service Menu Lists Only the Chosen Category (2026-11-09)
- **3dv.** Quote-Only Services in Any Category; the Boiler Service Switched to Quote-Only (2026-11-09)
- **3dw.** A Window Opened from Another Site Gets No Hold on This One (2026-11-10)
- **3dx.** A Service Page Leads to the Booking First (2026-11-11)
- **3dy.** The Cookie Notice Becomes One Slim Line (2026-11-11)
- **3dz.** No More Leak Detection or Thermal Imaging on the Site (2026-11-11)
- **3ea.** The FAQ Drops HVAC, Electrical Failures and the "Dashboard" (2026-11-11)
- **3eb.** Esher's Page Opens with Esher, Not Cobham (2026-11-11)
- **3ec.** The Workflow Documents Catch Up with the Services (2026-11-11)
- **3ed.** Times and Dates Written the UK Way (2026-11-11)
- **3ee.** The Calendar's Last American Time; "Release" on a Blocked Day (2026-11-11)
- **3ef.** A Service a Job Has Is Not Deleted (2026-11-11)
- **3eg.** Emergencies by Phone and WhatsApp; One Business Record for Google (2026-11-12)
- **3eh.** Our Google Reviews on the Site (2026-11-12)
- **3ei.** "Leave a Google Review" in the Paid-in-Full Invoice Email (2026-11-12)
- **3ej.** "How Did You Hear About Us?" (2026-11-12)
- **3ek.** Online Booking Within 15 Miles of the Base (2026-11-12)
- **3el.** A Town's Page Shows Its Own Four Services, in Short (2026-11-12)
- **3em.** Six More Towns Get a Page: Twickenham, Richmond, Molesey, New Malden, Ashtead, Cheam (2026-11-13)
- **3en.** More Local Text for Chessington, Surbiton, Kingston, Esher and Worcester Park & Ewell (2026-11-13)
- **3eo.** More Local Text for the Other Ten Older Town Pages (2026-11-13)
- **3ep.** Service Pages Get Blocks of Their Own, and the Narrow Questions Fixed (2026-11-13)
- **3eq.** The First Five Service Pages Get Their Own Text (2026-11-13)
- **3er.** The Next Five Service Pages Get Their Own Text (2026-11-13)
- **3es.** Five More Plumbing Pages Get Their Own Text (2026-11-13)
- **3et.** The Dashboard Counts the Enquiries' Answers Too (2026-11-13)
- **3eu.** Five More Service Pages Get Their Own Text (2026-11-13)
- **3ev.** Five More Service Pages Get Their Own Text (2026-11-13)
- **3ew.** Five More Service Pages Get Their Own Text (2026-11-13)
- **3ex.** Five More Service Pages Get Their Own Text (2026-11-13)
- **3ey.** The Last Three Service Pages Get Their Own Text (2026-11-13)
- **3ez.** Titles That Say What a Visitor Can Do, the Price Questions and a New About Page (2026-11-14)
- **3fa.** The Site Counts What Its Visitors Do (2026-11-14)
- **3fb.** Public Work Gallery, Homepage Crystal Strip & Admin Batch Upload (2026-11-14)
- **3fc.** The Gallery on a Phone: the Close Button and Caption Stay on Screen; More Room Around the Home Strip (2026-11-14)
- **3fd.** The Admin's Gallery Page: Each Action Returns to It; a Photo Form Waits for Its Photos; New Photos First (2026-11-14)
- **3fe.** The History Moved Out of `PROJECT_STATE.md` into `docs/history/` (2026-11-14)
- **3ff.** A Laptop Draws the Page a Wide Monitor Draws; a Taller Gallery Strip on the Home Page (2026-11-16)

---

## 4. Current Standing & Remaining Roadmap

> **The site is live and open to the public** (Section 3cu, 2026-10-23). The Launch Sprints, the Code Quality Sprints, the invoices, the areas work and the eight website items of the SEO plan are finished, and each is recorded in its Section 3 entries. This section lists only what is still open. The roadmap as it stood until 2026-11-14, with every finished item, its dated notes and the tier list of July 2026 that came before it, is kept whole in [`docs/history/roadmap-until-2026-11.md`](docs/history/roadmap-until-2026-11.md). When an item here is finished it is taken off this list; its record is its Section 3 entry.

### Open

None of these blocks anything.

1. **Facebook and Instagram on the site** (Launch Sprint L4 item 13). When the business has a page on each: their addresses go into `GlobalConstants` beside the other contact details, `sameAs` comes back in the home page JSON-LD with those two real addresses (and the test that forbids `sameAs` is changed to expect them), and the footer's round buttons get one for each.
2. **Trustpilot** (Launch Sprint L4 item 11): after Google reviews are collecting.
3. **Test leftovers in the staging and local databases** (Launch Sprint L4 item 10): the "Quality Check" jobs and the soft-deleted rows left by browser checks. Left for later by the developer's decision (Section 3cx), as one job with wiping both databases. A wiped database needs `ADMIN_SEED_EMAIL` to make its admin, and staging's `.env` has none yet. Production started empty and has none of them.
4. **The phone's "Book Now" bar on a quote-only page** (Launch Sprint L2 item 10). On a bookable service's page the bar opens the booking with that service chosen (Section 3dx). On a quote-only service's page and on the Small Building category page it still reads "Book Now" and opens the plain booking page, though that work is quoted for, not booked.

### Decided, not to be raised again

- **No alert when a nightly database backup fails.** The developer reads the backup log's last line each day (Section 3cx); the two ways an alert could be built are written down in Section 3cp.
- **Bootstrap's stylesheet is not trimmed** (Section 3cp).
- **The category card's picture on a phone stays as it is** (Section 3cy).

### Recorded for later, not planned

- **Tier 3 item 17, both halves**: reading the owner's card on the service pages from the `Technician` record, and a technician card in the booking confirmation email. Both were marked post-launch on 2026-09-21.
- **Tier 6 items 21 and 22**: matching a technician's trade to the service's category, and a guard on approving a job that has no technician. Both matter only once there is more than one technician (one at launch, Section 3cd). Check the code before taking either up: the Job Book (Section 3ce) rewrote the admin's job actions after they were written down.

### Large pieces of work

A large piece of work is listed here in a few lines while it runs, with its step-by-step plan kept in `docs/private/` (`AGENTS.md` Section 5). None is running now.

---

## 5. Architectural Decisions Worth Remembering

- **Optimistic concurrency needs a provider that actually enforces it.** EF Core's InMemory provider silently ignores both transactions and `RowVersion` concurrency checks — tests that need to prove rollback or double-booking rejection use Sqlite in-memory (`Microsoft.Data.Sqlite`, `DataSource=:memory:`, open connection kept alive for the test's duration), not InMemory.
- **`IDbQueryRunner.BeginTransactionAsync`** is the standard way to wrap multi-repository mutations atomically; nested `SaveChangesAsync` calls from different repositories sharing the same scoped `DbContext` automatically join the ambient transaction — no need to pass a transaction object around explicitly.
- **The "fail loud outside development, fall back safely inside it" pattern** is now used three times (Stripe key, Brevo key, Turnstile keys) and should be the default template for any future third-party integration key: never let a missing production secret silently degrade to mock/no-op behavior.
- **Capacity and assignment are separate concerns, and only one of them is a slot.** `AvailabilitySlot` is business capacity and carries no technician; `Booking.TechnicianId` is the single home of "who does this job", set by an admin after payment (Section 3r). Don't reintroduce a technician (or any other assignment-shaped field) onto the slot to make a query convenient — that duplication is exactly what caused the stale-copy and wiped-on-reschedule bugs Section 3r removed.
- **Read paths must not write.** `GetAvailableDatesAsync` (removed in Section 3dj) used to generate 30 days of slots as a side effect of being read, so merely browsing the public booking page created capacity nobody had decided to offer. Generation is now only ever triggered deliberately — by an admin, or by the non-production capacity seeder. Treat any "get" that mutates as a bug, not a convenience.
- **An answer the page has to react to is a result, not an exception.** `JobMoveResult`, `BookingCreateResult` and `JobCancelOutcome` say "taken", "not found" or "paid just now" as values the controller switches on, with its own words for each, so nothing string-matches an exception's message and no exception's text reaches a customer. Exceptions are left for what nobody can put right on the page (a status missing from the database, the database out of reach), which the controller logs and answers with its general message. `SlotUnavailableException` did this job, less well, until Section 3dh.
- **`wwwroot/css/base/utilities.css`** (added in Sprint 2) holds the small, generic spacing/typography/opacity/radius classes shared across every page (`mb-*`, `fs-*`, `lh-*`, `opacity-*`, `rounded-*`, `icon-fill`, etc.) — check here before inventing a new one-off class or reaching for an inline `style=`. Anything page-specific still belongs in that page's own `pages/*.css` file.
- **The sitemap is generated, not static** — `SeoController.Sitemap()` queries categories/services live via `ICategoriesService`/`IServicesService` rather than hardcoding URLs, specifically so it can't go stale as services are added or removed through the admin panel. Follow the same approach for any future sitemap-like listing.
- **The business's contact and company details live once, in `GlobalConstants`** (phone in both its forms, the WhatsApp link, the email, the company's name, number and incorporation date; Section 3bu). A view never types them out. The line is a VoIP landline: offer "call" and "WhatsApp", never SMS. A value written inside a `<script>` block goes through `Json.Serialize`, because Razor's HTML encoding is the wrong one there (Section 3bv has what went wrong without it).
- **A seeder fills an empty table; it does not keep a table in step with its list** (Section 3cb). Once an admin can edit, rename or delete a row, a seeder that looks rows up and writes them again cannot tell "renamed" from "missing" or "deleted on purpose" from "never there", and soft delete makes it worse: the lookup cannot see the deleted row and the unique index can. "Empty" counts deleted rows (`IgnoreQueryFilters()`). A change to seeded data after the first start is a migration or an admin's edit.
- **An email is never the reason a save looks failed** (Section 3cb). The save is the fact; the email reports it. Send after the save through `TrySendEmailAsync`, which logs `Email not sent` and carries on. And nothing a visitor typed goes into an email's HTML without `EmailText.Encode`; an acknowledgement to an address typed into a public form repeats nothing from the form at all.
- **An email's words are in its own class; when it goes is the service's call** (Section 3di). `BookingEmails`, `EnquiryEmails` and `AccountEmails` hold every word, the sender and the addresses of their emails, and answer whether the email went. The service that sends one decides when, and writes the history line and the `EmailsNotSent` row in its own save. To change what an email says, change its email class; to change when it goes, change the service. This replaced Section 3cl's "the words stay in the service that sends them", once the services had grown too long to read.
- **A view draws; it does not work things out or look things up** (Section 3dj). A page's script is a file in `wwwroot/js/`, given what it needs from the server in `data-` attributes, which Razor encodes, never in a value Razor writes into a script. What a page shows that takes a rule or a sum (a label's colour, what is still owed, a picture's copy on disk) is worked out by the view model or the controller, where a test can reach it; data a page needs travels in its model, not in `ViewData`, which keeps the title, description and address (`SetPageMeta`). Inline on purpose: the form guard's few lines, image `onerror` fallbacks and JSON-LD.
- **A setting the business code reads comes in a class of its own** (Section 3dk): `BusinessOptions`, `EmailOptions` and `StripeOptions`, bound when first asked for, never checked at start-up, a blank value meaning the default. A new setting of that kind goes into one of them or a new class beside them, not into a controller or service that reads `IConfiguration`. The infrastructure (Turnstile, rate limits, the form guard, R2, the admin seed, Brevo) keeps its keys as named constants in one class each, read late on purpose.
- **A library that makes files is updated on what it makes, not on what its notes say** (Section 3dk). SkiaSharp 4's replacement for the old "high quality" setting looked equivalent and was not: its picture copies were grainy. Put a few real files through the old version and the new, and compare the outputs (size and pixels) before the commit.
- **A validation rule the browser should show as the visitor types is a `RegularExpressionAttribute`** (Section 3cb), because that is what ASP.NET Core writes into the page for jQuery Validation with no script of ours. Its pattern then runs in two engines, so keep to what means the same in both: `[0-9]` for `\d`, no named groups, no lookbehind. A rule that cannot be a pattern (`NotMostlyLinks`) is checked on the server only.
- **Checks on a public form run cheapest first, and a check a person can fail says so** (Section 3cb). The hidden box and the time check catch only programs, so Contact and Join Our Team answer them with the usual thank-you and save nothing. Turnstile and the rate limit can stop a person, so they give a message and the phone number. When Cloudflare cannot be reached the submission is let through and logged: an outage must not close the forms.
- **A rule about what may be done to a job is asked twice, from one place** (Section 3ce). `BookingRules` is asked by the job's page, to decide which buttons to show, and again by the service before it acts. Hiding a button is not a check: a page left open while the job was cancelled, or a form sent without the page, reaches the service all the same, and one of these actions emails a customer. Add a new action's rule there, and use it in both places.
- **What a person reads about a job is derived, not stored** (Section 3ce). The two labels (`JobLabels`), what is still owed, and "time promised" are worked out from the job's status, its final price and its payments each time. A stored "amount paid" or "money status" would be a second copy that a missed update leaves wrong. The database's status names (`Approved`, `Completed`) are older than the business's words for them and stay as they are; only `JobLabels` translates.
- **Something added to the collection of an entity that is already loaded must arrive without a key** (Section 3ce). Entity Framework takes a new object it finds through a navigation for an existing row when its key is already set, and sends an UPDATE for a row that is not there. Every other model here sets its id in its constructor and is added through a repository, which is fine. `BookingHistoryEntry` is added to `booking.History` and so sets none.
- **Commit hygiene**: this project follows Conventional Commits with a `type(scope): title` subject line followed by a `- ` bullet per notable change (not prose paragraphs) — see recent `git log` for the established style before writing commit messages.
