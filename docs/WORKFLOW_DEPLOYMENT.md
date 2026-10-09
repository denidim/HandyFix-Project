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

## What's actually automated, and what isn't — read this before editing `deploy/`

Two different things live under `deploy/`, and only one of them is kept in sync by the pipeline:

- **The application image** — fully automated. Every push to `dev` rebuilds it from source and the
  SSH step always `pull`s the freshest one.
- **`docker-compose.staging.yml`, `Caddyfile`, and `.env`** — placed on the server **once, by hand**,
  when staging was first stood up. The SSH step never copies any of these three files from the repo
  — it only runs `docker compose -f docker-compose.staging.yml pull && up -d` **using whatever is
  already sitting in `/opt/handyfix/deploy/` on the server itself.**

That means editing `docker-compose.staging.yml` or `Caddyfile` in this repo and pushing to `dev`
**does nothing to staging by itself.** The deploy will report success — it did successfully restart
the containers — just using the old, unchanged file. This is exactly what happened when
`CloudflareR2:*` was first added to the compose file (2026-08-05): the env var mapping was correct
in the repo and the push succeeded, but staging kept throwing "not fully configured" because the
compose file that actually restarted was still the pre-change version. See `PROJECT_STATE.md`
Section 3ae for the full incident.

**If you change `docker-compose.staging.yml` or `Caddyfile`, you must also manually update the
server's copy** — SSH in and either edit the file directly or copy the new version over it, then run
`docker compose -f docker-compose.staging.yml up -d` yourself (or just push any commit to `dev`
afterward, since the next automated deploy will pick up the now-updated file). `.env` has always
worked this way and that part is documented — the two compose files were the gap.

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
  `X-Robots-Tag: noindex, nofollow` as a second line of defense against a search engine indexing
  staging: a copy of the real site under another address — belt and braces alongside the Basic
  Auth itself.

### Environment variables the compose file maps into the `web` container

Names only — real values live in `.env` on the server, itself gitignored, generated from
`deploy/.env.example`:

| Variable | Maps to app config key |
| --- | --- |
| `DB_CONNECTION_STRING` | `ConnectionStrings:DefaultConnection` |
| `ADMIN_SEED_EMAIL` / `ADMIN_SEED_PASSWORD` | `Admin:SeedEmail` / `Admin:SeedPassword` — the admin account's login email and password. **Read at the first start of a database only**; see "Startup sequence" below. |
| `BREVO_API_KEY` | `Brevo:ApiKey` |
| `EMAIL_BOOKINGS_FROM_ADDRESS` / `EMAIL_SYSTEM_FROM_ADDRESS` | `Email:BookingsFromAddress` / `Email:SystemFromAddress` — empty on staging and on the live site, which means `bookings@` on the real domain. |
| `EMAIL_SUBJECT_PREFIX` | `Email:SubjectPrefix` — a mark in front of every subject. `[STAGING]` on staging; empty on the live site. |
| `ADMIN_NOTIFICATION_EMAIL` | `Admin:NotificationEmail` — where notices to the company go (paid deposits, enquiries, job applications). Empty means `info@` on the real domain; staging sets a role address of its own. `WORKFLOW_EMAIL.md` has every email setting in one table. |
| `TURNSTILE_SITE_KEY` / `TURNSTILE_SECRET_KEY` | `Turnstile:SiteKey` / `Turnstile:SecretKey` — the person check on the public forms. **The form pages fail without both** outside Development; see `WORKFLOW_FORMS.md`. |
| `STRIPE_SECRET_KEY` / `STRIPE_WEBHOOK_SECRET` | `Stripe:SecretKey` / `Stripe:WebhookSecret` — the deposit. On staging both belong to a Stripe **sandbox**, so nobody is charged. **Paying for a booking fails without the key** outside Development. See "Stripe on staging" below. |
| `STAGING_HOSTNAME`, `BASIC_AUTH_USER`, `BASIC_AUTH_HASH` | consumed by the `caddy` service, not `web` |

`ASPNETCORE_ENVIRONMENT=Staging` is a hardcoded literal in the compose file itself, not pulled
from `.env`.

### Stripe on staging

Staging takes the deposit through Stripe in test mode since `PROJECT_STATE.md` Section 3cg; before
that a setting (`Stripe:AllowSandboxOutsideDevelopment`) made it pretend. That setting is gone from
the compose file and must never be given to the live site.

- **The key** is a restricted key made in a Stripe sandbox (it starts `rk_test_`), with one
  permission: Checkout Sessions, Write. The site opens, asks about and closes payment pages and
  asks Stripe for nothing else, so a leaked key can move no money.
- **The webhook** is made in the same sandbox: payload "Snapshot", the events
  `checkout.session.completed` and `checkout.session.expired`, pointed at
  `https://<staging host>/api/payment/webhook`. Its signing secret (`whsec_...`) is the second
  setting. Which version of Stripe's format the webhook is set to does not matter: the site reads
  the message's type and the page's id, and asks Stripe for the rest.
- **The Caddyfile lets that one path past Basic Auth**, because Stripe cannot answer a password
  prompt. The path is not open for that: the app refuses a call without Stripe's signature.
- **To pay on staging** use Stripe's test card, `4242 4242 4242 4242`, any date in the future
  and any three digits.
- **The live site** gets a live key and a webhook of its own, made outside any sandbox and pointed
  at the real domain. Same two settings, different values.

`CloudflareR2:*` (`R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY`, `R2_SERVICE_URL`, `R2_PUBLIC_URL`,
`R2_BUCKET_NAME`) is wired up as of 2026-08-05 — staging reuses the same bucket as local dev. See
`PROJECT_STATE.md` Section 3ae for how this was added and the compose-file sync issue it surfaced.

**A new setting has to reach the server before the build that needs it.** The Turnstile keys are
the case in point: a build with Turnstile fails its form pages when the keys are missing, and the
pipeline only replaces the image. So the order is: put the new lines into `.env` on the server,
copy the new `docker-compose.staging.yml` there, and only then push.

Three settings have defaults in code and need no line in `.env` unless they are to change:
`RateLimiting:FormPostsPerWindow` (10), `RateLimiting:FormWindowMinutes` (10) and
`RateLimiting:SlotLookupsPerMinute` (60). They are not mapped in the compose file; add
`RateLimiting__FormPostsPerWindow: "20"`-style lines there to override one.

---

## Why the app has to trust Caddy's forwarded headers

Caddy terminates real HTTPS from the browser, then proxies to the `web` container over **plain
HTTP** on the internal Docker network — there's no need for TLS on that hop, since it never leaves
the server. Caddy tells the app what the original connection actually was via the standard
`X-Forwarded-Proto`/`X-Forwarded-For`/`X-Forwarded-Host` headers, but nothing reads them unless the
app explicitly says to.

`Program.cs` configures and calls `UseForwardedHeaders()` as the **first** middleware in the
pipeline, before anything else — including exception handling and HSTS. Without it,
`Request.Scheme`/`Request.IsHttps` read `http` for every single request on staging, regardless of
what the browser used. That's not cosmetic — anything built on those two properties reads wrong:

- **Secure-flagged cookies** (including ASP.NET Core's own cookie-consent cookie) never actually
  get the `Secure` attribute, so browsers silently discard any cookie that also requires it (e.g.
  `SameSite=None` cookies) — this was a real, shipped bug: the cookie-consent banner reappeared on
  every page because "Accept" never actually persisted. Fixed by trusting the forwarded headers
  *and* by not using `SameSite=None` for a plain first-party cookie in the first place (`Lax`
  doesn't require `Secure` at all).
- **Absolute URLs built from `Request.Scheme`** — e.g. `PaymentController`'s Stripe Checkout
  success/cancel URLs — would be generated as `http://` instead of `https://`.

`ForwardedHeadersOptions.KnownNetworks`/`KnownProxies` are cleared rather than pinned to a fixed
address, because Docker Compose assigns Caddy's internal IP dynamically — there's nothing fixed to
allow-list. That's safe specifically because `web` publishes no ports of its own (see the compose
file above): Caddy is the only thing on the internal network that can reach it at all, so nothing
else is in a position to send a spoofed `X-Forwarded-*` header. This reasoning would need
revisiting if `web` ever gained a directly-reachable port.

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

**`AdminUserSeeder` makes the admin account at the one start that finds no administrator, from
`Admin:SeedEmail` and `Admin:SeedPassword`.** At that start, outside Development, it fails app
startup entirely if either is unset — it throws before `app.Run()`, naming the missing setting —
and only Development falls back to values written in the seeder, with a warning. This is the exact
CI/startup failure mode covered in the troubleshooting table below.

**At every later start it does nothing, and neither setting is read.** Changing
`ADMIN_SEED_PASSWORD` on a server whose database already has its admin changes nothing: the
password and the login email are changed in the admin panel, on the Account page. So for a new
database, the live site's above all, both are chosen **before** its first start.
`WORKFLOW_ADMIN_ACCOUNT.md` has the whole account.

---

## Quick troubleshooting

| Symptom | Cause |
| --- | --- |
| CI fails at the "Test" step | A real test regression — the deploy step never runs when this happens, by design. Fix the test before anything reaches staging. |
| Deploy step succeeds but the site doesn't change | Check the SSH script actually found `docker-compose.staging.yml` at `/opt/handyfix/deploy` on the server — a missing/misnamed file used to fail silently before `set -euo pipefail` was added. |
| You edited `docker-compose.staging.yml`/`Caddyfile`/env-var mappings in the repo, pushed, deploy shows green, but the behavior didn't change | **Not a pipeline failure** — see "What's actually automated, and what isn't" above. The SSH step never syncs these files from the repo; it restarted containers using the server's existing, unchanged copy. You have to update the server's copy yourself. |
| App container crashes on boot against a new, empty database | Almost always `Admin:SeedEmail` or `Admin:SeedPassword` (`ADMIN_SEED_EMAIL`, `ADMIN_SEED_PASSWORD` in `.env`) missing, or a password under 10 characters — the log names which. See the startup sequence above. A database that already has its admin needs neither. |
| `ADMIN_SEED_PASSWORD` was changed in `.env` and the old password still signs in | By design: it is read at the first start only. Change the password on the admin panel's Account page. |
| Caddy won't start / Basic Auth rejects a known-correct password | The hash in `BASIC_AUTH_HASH` doesn't match — regenerate with `docker run --rm caddy:2-alpine caddy hash-password --plaintext '<password>'` and update `.env`. |
| SSH deploy step fails with a key error | `STAGING_SSH_KEY` secret is missing, malformed, or the corresponding public key isn't authorized on the staging host. |
| Photo uploads fail on staging | `CloudflareR2:*` is configured as of 2026-08-05 (see above) — if this still happens, check the server's actual `.env` and `docker-compose.staging.yml` directly rather than assuming the repo version is what's running. |
| `/Contact`, `/JoinOurTeam` and `/Booking` fail, the rest of the site works | The Turnstile keys are missing from the container: `Turnstile is not configured for this environment` in the log. Check both lines are in `.env` **and** that the server's compose file maps them. |
| Every page `HomeController` serves fails | `Brevo:ApiKey` is missing. The enquiries service needs the email sender, and outside Development the sender refuses to exist without a key. |
| A booking is saved, then the customer gets an error page where Stripe should open | `Stripe is not configured for this environment` in the log: `STRIPE_SECRET_KEY` is missing from `.env`, or the server's compose file still has the old pretend-payment line in place of the two Stripe ones. |
| Stripe shows a webhook delivery as failed with `401` | The server's `Caddyfile` is the old one, without the path let past Basic Auth. Copy the repo's over it and restart Caddy. |
| Stripe shows a webhook delivery as failed with `400`, and the log says `A call to the Stripe webhook was refused` | `STRIPE_WEBHOOK_SECRET` is not the signing secret of the webhook that is calling. Each webhook has its own, and a sandbox's differs from the live one. Deposits are still confirmed when the customer comes back from Stripe, but not when they close the tab first. |
| A `Secure`-flagged cookie won't persist, or a generated absolute URL (e.g. Stripe redirect URLs) comes back `http://` instead of `https://` | `Request.Scheme`/`IsHttps` reading wrong behind Caddy — see "Why the app has to trust Caddy's forwarded headers" above. Confirm `UseForwardedHeaders()` is still the first middleware in `Program.cs`. |

For real hostnames/credentials and a step-by-step walkthrough of each failure above with actual
values, see `docs/private/STAGING_RUNBOOK.md`.

---

## Related

- Full architectural history and what's still open (production pipeline, DB host `sa` password
  rotation): `PROJECT_STATE.md` Section 3v–3w and Section 4 Tier 4. Stripe on staging:
  `PROJECT_STATE.md` Section 3cg.
- The forwarded-headers/cookie-consent bug, full root-cause writeup: `PROJECT_STATE.md`
  Section 3ac.
- Mobile-scroll fix, silent-booking-error fix, CloudflareR2 wiring, and the compose-file-drift bug
  this page now warns about: `PROJECT_STATE.md` Section 3ae.
- Real infrastructure values, SSH keys, and the SQL login's password:
  `docs/private/INFRASTRUCTURE.md`.
