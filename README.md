# Plumbing Handyman Surrey - Plumbing & Handyman Services

![.NET](https://img.shields.io/badge/.NET-10.0-blue)
![License](https://img.shields.io/badge/License-MIT-green)

A booking website for a real plumbing and handyman business based in **Chessington**, covering Surrey and South West London. A customer picks a service and a time, pays a £50 deposit through Stripe, and the job lands in the admin's job book.

`HandyFix` is the project's internal codename: namespaces, the solution, the databases and the repository keep it, while everything a visitor sees says Plumbing Handyman Surrey.

**Status**: the site runs on its live server and answers on `plumbing-handyman-surrey.co.uk`, behind a password until the business launches it. A staging copy runs beside it and takes every change first.

---

## 🚀 What It Does

- **Online booking with a deposit.** The admin opens time slots; a customer picks one and pays a £50 deposit on Stripe's hosted payment page. Two customers cannot take the same slot: claiming one runs in a transaction, guarded by a `RowVersion` concurrency token. The site believes a payment only after asking Stripe, both when the customer comes back and when Stripe's webhook calls. A booking left unpaid for 15 minutes is dropped, its payment page is closed at Stripe and its slot goes back on offer.
- **Quotes for bigger work.** Building and refurbishment services go through an enquiry form with photos, not the slot calendar.
- **The Job Book.** The admin panel gives every job a page, whether it was booked on the website or written in by hand after a phone call: the technician, the final price, each payment, private notes and a history line for every change. Around it sit a calendar, slot generation, and editing for services, categories, service areas, technicians, enquiries and reviews.
- **Public forms that hold up to abuse.** Rate limiting, a hidden-field and time check, Cloudflare Turnstile, and server-side validation of UK phone numbers and postcodes, including a check that the postcode is in an area the business serves.
- **Email.** Booking, payment and enquiry emails go through Brevo, from role addresses on the business's domain.
- **Local SEO.** A page for each of the 15 service areas, built from data the admin can edit; JSON-LD structured data; a generated `sitemap.xml`.
- **Photos.** Customers' photos are stored in Cloudflare R2. Service pictures uploaded by the admin are resized and re-encoded to WebP.
- **Automated tests and deploys.** Over 900 tests run on every push. A push to `dev` deploys to staging; a merged pull request into `main` deploys to production.

---

## 🏗️ Architecture

The application is a layered ASP.NET Core MVC solution (see [Project Structure](#-project-structure)). It runs on three small Hetzner Cloud servers, one each for production, staging and the database, joined by a private network. Cloudflare stands in front of production.

```text
             VISITORS                       DEVELOPER, TESTERS
 (UK customers, mostly on phones)      (staging, behind a password)
                 │                                   │
                 │ HTTPS                             │ HTTPS
                 ▼                                   │
┌─────────────────────────────────┐                  │
│           CLOUDFLARE            │                  │
│ ─────────────────────────────── │                  │
│ • DNS and proxy for the domain  │                  │
│ • DDoS protection, TLS at edge  │                  │
│ • Turnstile (form person check) │                  │
│ • R2 (customers' job photos)    │                  │
└────────────────┬────────────────┘                  │
                 │ Full (strict) TLS with            │
                 │ Cloudflare's origin certificate   │
====================== HETZNER CLOUD (HELSINKI) =======================
                 │                                   │
                 ▼                                   ▼
   ┌───────────────────────────┐       ┌───────────────────────────┐
   │      PRODUCTION HOST      │       │       STAGING HOST        │
   │ ───────────────────────── │       │ ───────────────────────── │
   │ Caddy (reverse proxy)     │       │ Caddy (reverse proxy)     │
   │  ├─ CF origin certificate │       │  ├─ HTTP Basic Auth       │
   │  └─ www → bare domain     │       │  └─ X-Robots: noindex     │
   │             │             │       │             │             │
   │             ▼             │       │             ▼             │
   │ ASP.NET Core app          │       │ ASP.NET Core app          │
   │  ├─ .NET 10 MVC           │       │  ├─ .NET 10 MVC           │
   │  ├─ Data Protection keys  │       │  ├─ Stripe sandbox        │
   │  ├─ TZ: Europe/London     │       │  └─ [STAGING] emails      │
   │  └─ Stripe, live          │       │                           │
   └─────────────┬─────────────┘       └─────────────┬─────────────┘
                 │          PRIVATE NETWORK          │
                 │        (handyfix-internal)        │
                 │    no route from the internet     │
                 └─────────────────┬─────────────────┘
                                   │ port 1433, private address only
                                   ▼
                 ┌───────────────────────────────────┐
                 │           DATABASE HOST           │
                 │ ───────────────────────────────── │
                 │ Hetzner Cloud Firewall            │
                 │  • from the internet: SSH only    │
                 │  • port 1433: closed              │
                 │                                   │
                 │ Docker: SQL Server 2022 Express   │
                 │  • handyfix_prod                  │
                 │  • handyfix_staging               │
                 │                                   │
                 │ Storage and backups               │
                 │  • data on a Docker volume        │
                 │  • nightly .bak, 14 nights kept   │
                 │  • Hetzner daily backups, 7 kept  │
                 └─────────────────▲─────────────────┘
                                   │ SSH tunnel, key only
                                   │
                     ┌───────────────────────────┐
                     │     DEVELOPER MACHINE     │
                     │  SSMS → localhost,14330   │
                     └───────────────────────────┘
```

Staging is reached directly, on the server's own hostname, with a certificate Caddy gets from Let's Encrypt. It uses the same Turnstile and R2 services. Until the site launches, production's Caddy asks for a password and sends `noindex` as well, the way staging's always does.

### The database is kept off the internet

- A Hetzner Cloud Firewall in front of the database server lets in SSH and nothing else. SQL Server's port does not answer from the internet.
- Both web servers reach SQL Server over the private network only.
- Each site signs in to SQL Server with a login of its own, never the administrator's. The live site's login has rights in the live database and nowhere else.
- SSH is key-only on all three servers. Database administration from a developer's machine goes through an SSH tunnel.

### Every change is tested before it is deployed

```text
push to dev             →  build  →  900+ tests  →  Docker image  →  GHCR  →  staging
merged PR, dev → main   →  build  →  900+ tests  →  Docker image  →  GHCR  →  production
```

- **GitHub Actions** runs the whole test suite on every push: service tests, controller tests, and full-stack tests that boot the real application against SQLite in memory. A failing test stops the deploy.
- **Multi-stage Docker build.** Images are pushed to GitHub Container Registry and tagged with the commit id, so the live site can be put back on an earlier version with one command.
- **The live server's key is out of reach of other branches.** It is a secret of a GitHub environment that only `main` may use, and nothing reaches `main` except a pull request from `dev` that was checked on staging.
- **A deploy does not sign anybody out.** The keys behind the login cookie, the form tokens and the password reset links (ASP.NET Core Data Protection) are kept on a Docker volume, so they outlive the container. On production Caddy holds a request that arrives while the container is being replaced, and hands it to the new one.

### Backups

- SQL Server writes a backup file of each database every night; 14 nights are kept.
- Hetzner's automatic backups copy each server's whole disk every day; seven are kept.

More detail: [docs/WORKFLOW_DEPLOYMENT.md](docs/WORKFLOW_DEPLOYMENT.md) for both pipelines, and [PROJECT_STATE.md](PROJECT_STATE.md) Section 1 for the full architecture.

---

## 🛠 Tech Stack

- **Backend**: .NET 10, ASP.NET Core MVC + Razor Pages (Identity)
- **Database**: SQL Server 2022 + EF Core 10 (code-first migrations)
- **Mapping**: [Mapster](https://github.com/MapsterMapper/Mapster), via an `IMapFrom<T>` / `IMapTo<T>` convention registered at startup
- **Frontend**: Bootstrap 5 + jQuery / vanilla JS
- **Payments**: Stripe Checkout (hosted payment page) with a signed webhook
- **Email**: Brevo
- **File storage**: Cloudflare R2 (S3-compatible)
- **Abuse protection**: ASP.NET Core rate limiter, Cloudflare Turnstile
- **Architecture**: Clean Architecture — layered projects, MVC Areas for the admin panel
- **Testing**: xUnit + Moq; EF Core SQLite in memory where a test needs real transactions or concurrency checks
- **Hosting**: Hetzner Cloud, Docker Compose, Caddy as the reverse proxy, Cloudflare in front of production
- **CI/CD**: GitHub Actions, building to GitHub Container Registry and deploying to staging (`dev`) and production (`main`)

---

## 📋 Project Structure

```text
src/                                 # Root source folder
├── HandyFix.Common/                 # Shared constants and cross-cutting concerns
├── Data/                            # Data access layer (DAL)
│   ├── HandyFix.Data/               # EF Core DbContext, migrations, repositories, seeders
│   ├── HandyFix.Data.Common/        # Repository interfaces and base model classes
│   └── HandyFix.Data.Models/        # Database entities
├── Services/                        # Business logic layer (BLL)
│   ├── HandyFix.Services/           # Cross-cutting services (image storage, Cloudflare R2)
│   ├── HandyFix.Services.Data/      # Business logic services, one per domain area
│   ├── HandyFix.Services.Mapping/   # Mapster mapping conventions and configuration
│   └── HandyFix.Services.Messaging/ # Transactional email (Brevo)
├── Web/                             # Presentation layer
│   ├── HandyFix.Web/                # Main ASP.NET Core MVC web application
│   └── HandyFix.Web.ViewModels/     # ViewModels and Data Transfer Objects (DTOs)
└── Tests/                           # Automated tests
    ├── HandyFix.Services.Data.Tests/# Tests for the business services
    ├── HandyFix.Web.Tests/          # Controller tests and full-stack WebApplicationFactory tests
    └── Sandbox/                     # Console app for prototyping against the real services

deploy/                              # Compose files and Caddyfiles for staging and production
.github/workflows/                   # deploy-dev.yml (staging) and deploy-prod.yml (production)
Dockerfile                           # Multi-stage build of the web application
docs/                                # One WORKFLOW_*.md per part of the system
```

---


## 🏁 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server — development runs against a Docker container on `localhost,1433`. LocalDB or a full local instance work too; just point the connection string at them.

### Setup

```bash
# Clone the repo
git clone https://github.com/denidim/HandyFix-Project.git
cd HandyFix-Project/src

# Restore packages
dotnet restore

# Run the application
dotnet run --project Web/HandyFix.Web
```

The committed `appsettings.json` connection string is a placeholder — set your real one via User Secrets alongside the keys below:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=HandyFix;..."
```

**No manual migration step is needed.** `Program.cs` runs `dbContext.Database.Migrate()` on startup and then seeds, so a fresh clone builds its own database on first run. Outside Production it also seeds 14 days of booking capacity, so the booking flow works immediately without an admin generating slots first.

### Required local secrets

None of the values below live in any committed `appsettings.json` — set them locally via **.NET User Secrets** (the project already has a `UserSecretsId`, so this needs no extra setup):

```bash
cd src/Web/HandyFix.Web
dotnet user-secrets set "Stripe:SecretKey" "rk_test_..."
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
dotnet user-secrets set "Brevo:ApiKey" "xkeysib-..."
dotnet user-secrets set "Admin:SeedEmail" "<the email you will sign in with>"
dotnet user-secrets set "Admin:SeedPassword" "<a password of at least 10 characters>"
dotnet user-secrets set "CloudflareR2:AccessKeyId" "..."
dotnet user-secrets set "CloudflareR2:SecretAccessKey" "..."
dotnet user-secrets set "CloudflareR2:ServiceUrl" "..."
dotnet user-secrets set "CloudflareR2:PublicUrl" "..."
dotnet user-secrets set "CloudflareR2:BucketName" "..."
```

In staging/production these are supplied as environment variables instead (`Admin__SeedPassword`, `CloudflareR2__AccessKeyId`, etc. — double underscore is ASP.NET Core's section separator), read from a `.env` file that lives on the server only and is never committed ([docs/WORKFLOW_DEPLOYMENT.md](docs/WORKFLOW_DEPLOYMENT.md)).

The Stripe key is a restricted key from a Stripe sandbox with one permission, Checkout Sessions: Write; the site uses Stripe's hosted payment page, so it needs no publishable key. The webhook secret matters only where Stripe can reach the site, which a local run is not: locally a deposit is confirmed when the browser comes back from Stripe.

If `Stripe:SecretKey` / `Brevo:ApiKey` are unset, the app falls back to a pretend payment (Stripe) or a no-op sender (Brevo) **only in Development** — outside Development the email sender refuses to start and paying for a booking throws, so a missing key can't silently ship a broken (or, for Stripe, fake-successful) production flow. `Admin:SeedEmail` and `Admin:SeedPassword` follow the same rule: unset outside Development throws; unset in Development seeds fixed dev-only fallbacks, never used elsewhere. The Turnstile keys (`Turnstile:SiteKey`, `Turnstile:SecretKey`) follow it too: with neither set, the person check on the forms is off in Development, and outside Development the form pages fail ([docs/WORKFLOW_FORMS.md](docs/WORKFLOW_FORMS.md)).

**The two `Admin:Seed*` settings are read once in a database's life**: at the first start, which makes the admin account. Setting or changing them later changes nothing. After that the admin changes the password and the login email in the admin panel, on the Account page ([docs/WORKFLOW_ADMIN_ACCOUNT.md](docs/WORKFLOW_ADMIN_ACCOUNT.md)).

---

## 📄 Documentation

- **[PROJECT_STATE.md](PROJECT_STATE.md)** — architectural memory: what the system is, what's been built and verified, what's left, and the decisions worth remembering. Start here.
- **[DESIGN.md](DESIGN.md)** — design tokens, utility classes, page-shell templates, and the admin/card/area component families.
- **[docs/WORKFLOW_BOOKINGS.md](docs/WORKFLOW_BOOKINGS.md)** — every job end to end, booked on the website or written in by the admin: capacity → the booking and its deposit, or the job written in → the technician → done, with the final price → each payment on the job; plus cancelling, moving, notes and the job's history.
- **[docs/WORKFLOW_SERVICE_AREAS.md](docs/WORKFLOW_SERVICE_AREAS.md)** — how a service area is created, seeded and published.
- **[docs/WORKFLOW_SERVICES.md](docs/WORKFLOW_SERVICES.md)** — services & categories admin CRUD and the local WebP image pipeline.
- **[docs/WORKFLOW_TECHNICIANS.md](docs/WORKFLOW_TECHNICIANS.md)** — the technician roster: create, deactivate vs. delete, the technician a new database starts with.
- **[docs/WORKFLOW_REVIEWS.md](docs/WORKFLOW_REVIEWS.md)** — admin approve/delete and the config gate that controls public display.
- **[docs/WORKFLOW_ENQUIRIES.md](docs/WORKFLOW_ENQUIRIES.md)** — contact-form submissions and job applications: the emails they send, list, view, delete.
- **[docs/WORKFLOW_FORMS.md](docs/WORKFLOW_FORMS.md)** — what a public form submission goes through: rate limit, hidden-field and time checks, Cloudflare Turnstile, and the site's own error pages.
- **[docs/WORKFLOW_EMAIL.md](docs/WORKFLOW_EMAIL.md)** — every email the site sends, from which address and to whom, and the email settings per environment.
- **[docs/WORKFLOW_ADMIN_ACCOUNT.md](docs/WORKFLOW_ADMIN_ACCOUNT.md)** — the admin account: where it comes from, what protects its login, changing its password and email, and resetting a forgotten password.
- **[docs/WORKFLOW_DEPLOYMENT.md](docs/WORKFLOW_DEPLOYMENT.md)** — the two CI/CD pipelines, staging and production, public-repo-safe (no real hosts/credentials).

---

*Made with ❤️ for a real client project*
