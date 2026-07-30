# Handy Fix - Plumbing & Handyman Services

![.NET](https://img.shields.io/badge/.NET-10.0-blue)
![License](https://img.shields.io/badge/License-MIT-green)

**Professional web application** for a local plumbing and handyman business based in **South London** and operating across Kent.

**Goal**: High-conversion website with strong local SEO, online booking system and payment integration.

---

## 🚀 Features (In Progress)

- [x] Clean Architecture + CQRS (MediatR)
- [x] ASP.NET Core 10 MVC + Razor Pages
- [x] Entity Framework Core + SQL Server
- [x] Identity + Role-based authorization
- [x] Central Package Management
- [x] Booking calendar with real-time availability
- [x] Online payment + deposit (Stripe)
- [x] Local SEO optimization (Google Business, schema markup)
- [x] Admin dashboard for managing bookings
- [x] Responsive design + mobile-first

---

## 🛠 Tech Stack

- **Backend**: .NET 10, ASP.NET Core MVC, MediatR, AutoMapper
- **Database**: SQL Server + EF Core
- **Frontend**: Bootstrap 5 + jQuery / vanilla JS
- **Architecture**: Clean Architecture + Feature Folders
- **Testing**: xUnit + Moq + FluentAssertions
- **CI/CD**: GitHub Actions (planned)

---

## 📋 Project Structure

```text
src/                                 # Root source folder
├── Common/                          # Shared constants, helpers, and cross-cutting concerns
├── Data/                            # Data access layer (DAL)
│   ├── HandyFix.Data/               # EF Core DbContext and database migrations
│   ├── HandyFix.Data.Common/        # Repository interfaces and base classes
│   └── HandyFix.Data.Models/        # Database entities
├── Services/                        # Business logic layer (BLL)
│   ├── HandyFix.Services/           # Core business interfaces and general services
│   ├── HandyFix.Services.Data/      # Data-centric services and CQRS handlers
│   ├── HandyFix.Services.Mapping/   # AutoMapper profiles and configurations
│   └── HandyFix.Services.Messaging/ # Email and SMS notification services
├── Web/                             # Presentation layer
│   ├── HandyFix.Web/                # Main ASP.NET Core MVC web application
│   └── HandyFix.Web.ViewModels/     # ViewModels and Data Transfer Objects (DTOs)
├── Tests/                           # Automated tests
│   ├── HandyFix.Services.Data.Tests/# Unit tests for business services
│   └── HandyFix.Web.Tests/          # Tests for web controllers and endpoints
└── Sandbox/                         # Console apps for prototyping and database seeding
```

---


## 🏁 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (LocalDB or full)

### Setup

```bash
# Clone the repo
git clone https://github.com/denidim/HandyFix-Project.git
cd HandyFix-Project/src

# Restore packages
dotnet restore

# Update database
dotnet ef database update --project Web/HandyFix.Web

# Run the application
dotnet run --project Web/HandyFix.Web
```

### Required local secrets

None of the values below live in any committed `appsettings.json` — set them locally via **.NET User Secrets** (the project already has a `UserSecretsId`, so this needs no extra setup):

```bash
cd src/Web/HandyFix.Web
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."
dotnet user-secrets set "SendGrid:ApiKey" "SG...."
dotnet user-secrets set "Admin:SeedPassword" "<a strong password you choose>"
dotnet user-secrets set "CloudflareR2:AccessKeyId" "..."
dotnet user-secrets set "CloudflareR2:SecretAccessKey" "..."
dotnet user-secrets set "CloudflareR2:ServiceUrl" "..."
dotnet user-secrets set "CloudflareR2:PublicUrl" "..."
dotnet user-secrets set "CloudflareR2:BucketName" "..."
```

In staging/production these are supplied as environment variables instead (`Admin__SeedPassword`, `CloudflareR2__AccessKeyId`, etc. — double underscore is ASP.NET Core's section separator), injected via GitHub Actions repo secrets at deploy time, never committed.

If `Stripe:SecretKey` / `SendGrid:ApiKey` are unset, the app falls back to Mock/Sandbox mode (Stripe) or a no-op sender (SendGrid) **only in Development** — both throw on startup outside Development, so a missing key can't silently ship a broken (or, for Stripe, fake-successful) production flow. `Admin:SeedPassword` follows the same rule: unset outside Development throws; unset in Development seeds a fixed dev-only fallback password, never used elsewhere.

---

## 📄 Documentation

- Architecture (coming soon)
- Database Schema (coming soon)

---

## 📞 Business Information

**Handy Fix**  
*Plumbing & Handyman Services*  
South London & Kent  
📍 **Serving:** Croydon, Bromley, Orpington, Dartford, Sevenoaks and surrounding areas

---

*Made with ❤️ for a real client project*
