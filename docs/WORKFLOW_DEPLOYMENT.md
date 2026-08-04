# 🚀 Deployment — Staging Pipeline

How a push to `dev` ends up running on the staging server. This doc is deliberately public-repo-safe
— no real hostnames, IPs, or credential values anywhere in it, matching the rest of this repo (see
`PROJECT_STATE.md`'s Hosting section for the same `<PLACEHOLDER>` convention). If you need real
values, they live in `docs/private/INFRASTRUCTURE.md` and `docs/private/STAGING_RUNBOOK.md` — both
gitignored, neither reproduced here.

> **Production deployment (`deploy-prod.yml`) does not exist yet.** Deliberately deferred until
> staging has been in use for a while rather than built alongside it — see `PROJECT_STATE.md` Tier 4.

---

## The pipeline, step by step

**`.github/workflows/deploy-dev.yml`** — triggers on every push to `dev`:

1. Checkout, `.NET 10.0.x` setup.
2. `dotnet restore` / `build -c Release` / **`dotnet test`** — a failing test blocks the deploy
   entirely, there's no `continue-on-error`.
3. Log in to `ghcr.io` using the automatic `secrets.GITHUB_TOKEN` (no separate PAT needed).
4. Build and push the Docker image, tagged both `latest` and `${{ github.sha }}`.
5. SSH into the staging host (`secrets.STAGING_HOST` / `secrets.STAGING_SSH_KEY`) and run:
   ```bash
   set -euo pipefail
   cd /opt/handyfix/deploy
   docker compose -f docker-compose.staging.yml pull
   docker compose -f docker-compose.staging.yml up -d
   docker image prune -f
   ```
   `set -euo pipefail` matters here: an earlier version of this script had no `set -e` and could
   silently do nothing for multiple runs when `docker compose` failed to find the compose file.

**GitHub Secrets this depends on** (names only): `STAGING_HOST`, `STAGING_SSH_KEY`, plus the
automatic `GITHUB_TOKEN`.

---

## The image — `Dockerfile` (repo root)

Multi-stage build. The build stage copies just the `.sln`, `Directory.Build.props`,
`Directory.Packages.props`, `Rules.ruleset`, `stylecop.json`, and every `.csproj` **before** copying
the rest of the source tree — this makes `dotnet restore` its own cached Docker layer, so a
source-only change doesn't re-download the whole dependency graph on every build. Runtime stage is
`mcr.microsoft.com/dotnet/aspnet:10.0`, listens on port `8080` (`ASPNETCORE_HTTP_PORTS=8080`).

`.dockerignore` excludes `bin/`/`obj/`/`.git/`/docs/`*.md` (README kept) from the build context.

---

## The stack — `deploy/docker-compose.staging.yml` + `deploy/Caddyfile`

Two services on a private `internal` Docker network:

- **`web`** — the app container. **No ports exposed to the host** — only reachable through `caddy`.
- **`caddy`** — `caddy:2-alpine`, the only service with `80`/`443` published. Terminates TLS
  (via the Hetzner reverse-DNS hostname), enforces HTTP Basic Auth, and sets
  `X-Robots-Tag: noindex, nofollow` as a second line of defense against the site being indexed while
  it still carries fabricated placeholder business content (see `PROJECT_STATE.md` Section 4 item
  16) — belt and braces alongside the Basic Auth itself.

### Environment variables the compose file maps into the `web` container

Names only — real values live in `.env` on the server, itself gitignored, generated from
`deploy/.env.example`:

| Variable | Maps to app config key |
| --- | --- |
| `DB_CONNECTION_STRING` | `ConnectionStrings:DefaultConnection` |
| `ADMIN_SEED_PASSWORD` | `Admin:SeedPassword` |
| `BREVO_API_KEY` | `Brevo:ApiKey` |
| `EMAIL_BOOKINGS_FROM_ADDRESS` / `EMAIL_SYSTEM_FROM_ADDRESS` | `Email:BookingsFromAddress` / `Email:SystemFromAddress` |
| `STAGING_HOSTNAME`, `BASIC_AUTH_USER`, `BASIC_AUTH_HASH` | consumed by the `caddy` service, not `web` |

`ASPNETCORE_ENVIRONMENT=Staging` and `Stripe__AllowSandboxOutsideDevelopment=true` are hardcoded
literals in the compose file itself, not pulled from `.env` — the Stripe flag exists because there's
no real Stripe account yet, letting staging demo the full booking flow through the sandbox bypass;
remove it and set `Stripe__SecretKey` once a real test-mode account exists.
**`CloudflareR2:*` is deliberately not set for staging** — photo upload is untested there pending a
decision on whether staging reuses the dev bucket or gets its own. Uploads simply fail until this is
added; nothing at startup depends on it.

---

## Startup sequence — `Program.cs`

On every boot, before the app starts serving requests:

1. `dbContext.Database.Migrate()` if running against SQL Server (both Staging and Production go
   through this branch — real EF migrations, not `EnsureCreated`).
2. Nine seeders run in a **fixed order**: Roles → Admin User → Booking Statuses → Payment Statuses →
   Service Categories → Services → Service Areas → Technicians → Settings.
3. `DevelopmentCapacitySeeder` — seeds 14 days of booking capacity, gated on `!IsProduction()` (so
   it **does** run in Staging, deliberately, so a freshly-deployed staging box has a working booking
   flow without any manual admin steps).

**`AdminUserSeeder` fails app startup entirely outside Development if `Admin:SeedPassword` is
unset** — it throws before `app.Run()`, and only Development falls back to a hardcoded password
with a warning. This is the exact CI/startup failure mode covered in the troubleshooting table below.

---

## Quick troubleshooting

| Symptom | Cause |
| --- | --- |
| CI fails at the "Test" step | A real test regression — the deploy step never runs when this happens, by design. Fix the test before anything reaches staging. |
| Deploy step succeeds but the site doesn't change | Check the SSH script actually found `docker-compose.staging.yml` at `/opt/handyfix/deploy` on the server — a missing/misnamed file used to fail silently before `set -euo pipefail` was added. |
| App container crashes on boot in Staging | Almost always `Admin:SeedPassword` (`ADMIN_SEED_PASSWORD` in `.env`) missing or unset — see the startup sequence above. |
| Caddy won't start / Basic Auth rejects a known-correct password | The hash in `BASIC_AUTH_HASH` doesn't match — regenerate with `docker run --rm caddy:2-alpine caddy hash-password --plaintext '<password>'` and update `.env`. |
| SSH deploy step fails with a key error | `STAGING_SSH_KEY` secret is missing, malformed, or the corresponding public key isn't authorized on the staging host. |
| Photo uploads fail on staging | Expected — `CloudflareR2:*` isn't configured there yet. |

For real hostnames/credentials and a step-by-step walkthrough of each failure above with actual
values, see `docs/private/STAGING_RUNBOOK.md`.

---

## Related

- Full architectural history and what's still open (production pipeline, Stripe/R2 for staging,
  DB host `sa` password rotation): `PROJECT_STATE.md` Section 3v–3w and Section 4 Tier 4.
- Real infrastructure values, SSH keys, and the SQL login's password:
  `docs/private/INFRASTRUCTURE.md`.
