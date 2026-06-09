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
- [ ] Booking calendar with real-time availability
- [ ] Online payment + deposit (Stripe)
- [ ] Local SEO optimization (Google Business, schema markup)
- [ ] Admin dashboard for managing bookings
- [ ] Responsive design + mobile-first

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
