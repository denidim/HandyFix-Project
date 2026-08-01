# Handy Fix - Plumbing & Handyman Services

![.NET](https://img.shields.io/badge/.NET-10.0-blue)
![License](https://img.shields.io/badge/License-MIT-green)

**Professional web application** for a local plumbing and handyman business based in **Chessington**, covering South West London and Surrey.

**Goal**: High-conversion website with strong local SEO, online booking system and payment integration.

---

## 🚀 Features (In Progress)

- [x] Clean Architecture with strict layer separation
- [x] ASP.NET Core 10 MVC + Razor Pages (Identity)
- [x] Entity Framework Core + SQL Server
- [x] Identity + Role-based authorization
- [x] Central Package Management
- [x] Booking calendar with concurrency-safe slot claiming
- [x] Online payment + deposit (Stripe)
- [x] Structured data / schema markup for local SEO
- [x] Admin dashboard for managing bookings
- [x] Responsive design + mobile-first
- [ ] Google Business Profile integration
- [ ] CI/CD pipeline

---

## 🛠 Tech Stack

- **Backend**: .NET 10, ASP.NET Core MVC + Razor Pages
- **Database**: SQL Server + EF Core (code-first migrations)
- **Mapping**: [Mapster](https://github.com/MapsterMapper/Mapster), via an `IMapFrom<T>` / `IMapTo<T>` convention registered at startup
- **Frontend**: Bootstrap 5 + jQuery / vanilla JS
- **Architecture**: Clean Architecture — layered projects, MVC Areas for the admin panel
- **Testing**: xUnit + Moq
- **CI/CD**: GitHub Actions (planned)

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
    ├── HandyFix.Services.Data.Tests/# Unit tests for business services
    ├── HandyFix.Web.Tests/          # Full-stack WebApplicationFactory integration tests
    └── Sandbox/                     # Console app for prototyping against the real services
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
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
dotnet user-secrets set "Brevo:ApiKey" "xkeysib-..."
dotnet user-secrets set "Admin:SeedPassword" "<a strong password you choose>"
dotnet user-secrets set "CloudflareR2:AccessKeyId" "..."
dotnet user-secrets set "CloudflareR2:SecretAccessKey" "..."
dotnet user-secrets set "CloudflareR2:ServiceUrl" "..."
dotnet user-secrets set "CloudflareR2:PublicUrl" "..."
dotnet user-secrets set "CloudflareR2:BucketName" "..."
```

In staging/production these are supplied as environment variables instead (`Admin__SeedPassword`, `CloudflareR2__AccessKeyId`, etc. — double underscore is ASP.NET Core's section separator), injected via GitHub Actions repo secrets at deploy time, never committed.

If `Stripe:SecretKey` / `Brevo:ApiKey` are unset, the app falls back to Mock/Sandbox mode (Stripe) or a no-op sender (Brevo) **only in Development** — both throw on startup outside Development, so a missing key can't silently ship a broken (or, for Stripe, fake-successful) production flow. `Admin:SeedPassword` follows the same rule: unset outside Development throws; unset in Development seeds a fixed dev-only fallback password, never used elsewhere.

---

## 📄 Documentation

- **[PROJECT_STATE.md](PROJECT_STATE.md)** — architectural memory: what the system is, what's been built and verified, what's left, and the decisions worth remembering. Start here.
- **[DESIGN.md](DESIGN.md)** — design language: color palette, typography, spacing, animation conventions.
- **[docs/WORKFLOW_BOOKINGS.md](docs/WORKFLOW_BOOKINGS.md)** — the booking pipeline end to end: generate capacity → customer books and pays → admin assigns a technician → admin approves.
- **[docs/WORKFLOW_SERVICE_AREAS.md](docs/WORKFLOW_SERVICE_AREAS.md)** — how a service area is created, seeded and published.

> **In progress**: the remaining admin workflows are not documented yet — services & categories (including the image pipeline), technicians, reviews, enquiries, and deployment. Each gets its own `docs/WORKFLOW_*.md`, and this section becomes the complete index once they all exist.

---

*Made with ❤️ for a real client project*
