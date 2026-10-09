# 🚀 Deployment — Staging and Production Pipelines

How a push to `dev` ends up running on the staging server, and how a merge into `main` ends up on
the live one. This doc is deliberately public-repo-safe — no real hostnames, IPs, or credential
values anywhere in it, matching the rest of this repo (see `PROJECT_STATE.md`'s Hosting section for
the same `<PLACEHOLDER>` convention). If you need real values, they live in
`docs/private/INFRASTRUCTURE.md` and `docs/private/STAGING_RUNBOOK.md` — both gitignored, neither
reproduced here.

The two pipelines are the same shape on purpose. Staging is described first and in full;
"Production" further down says only what differs.

---

## The pipeline, step by step

**`.github/workflows/deploy-dev.yml`** — triggers on every push to `dev`:

1. Checkout, `.NET 10.0.x` setup.
2. `dotnet restore` / `build -c Release` / **`dotnet test`** — a failing test blocks the deploy
   entirely, there's no `continue-on-error`.
3. Log in to `ghcr.io` using the automatic `secrets.GITHUB_TOKEN` (no separate PAT needed).
4. Build and push the Docker image, tagged both `latest` and `${{ github.sha }}`.
5. Copy `deploy/docker-compose.staging.yml` and `deploy/caddy/staging/Caddyfile` to
   `/opt/handyfix/deploy/` on the staging host (`secrets.STAGING_HOST` / `secrets.STAGING_SSH_KEY`).
6. SSH into the same host and run:
   ```bash
   set -euo pipefail
   cd /opt/handyfix/deploy
   docker compose -f docker-compose.staging.yml pull
   docker compose -f docker-compose.staging.yml up -d
   # then: caddy reload, tried up to five times (see below)
   docker image prune -f
   ```
   `set -euo pipefail` matters here: an earlier version of this script had no `set -e` and could
   silently do nothing for multiple runs when `docker compose` failed to find the compose file.

**GitHub Secrets this depends on** (names only): `STAGING_HOST`, `STAGING_SSH_KEY`, plus the
automatic `GITHUB_TOKEN`.

**The two SSH actions are pinned to a commit**, not to a tag such as `@v1`. They are somebody
else's code and they are handed the server's key; a tag can be moved to different code overnight,
a commit cannot. The comment beside each says which release that commit is.

---

## What's actually automated, and what isn't — read this before editing `deploy/`

- **The application image** — automated. Every push to `dev` rebuilds it from source and the SSH
  step always `pull`s the freshest one.
- **The compose file and the Caddyfile** — automated since `PROJECT_STATE.md` Section 3ci. Every
  deploy copies both from the repository to the server, then asks Caddy to re-read its file
  (`caddy reload`, which drops no connection). A change to either, pushed, is live by the end of
  that run.
- **`.env`** — by hand, always. It holds the secrets and exists on the server only. On the live
  server the same goes for the certificate in `certs/`.

**Why the copying was added.** Until Section 3ci the compose file and the Caddyfile were put on the
server once, by hand, and the pipeline only ever replaced the image. Editing either in the
repository and pushing did nothing to staging, while the deploy reported success: it had restarted
the containers, with the old file. That is what happened when `CloudflareR2:*` was first added to
the compose file (2026-08-05, `PROJECT_STATE.md` Section 3ae), and every change since had to be
remembered and copied over before the push that needed it. On a live site a forgotten copy is an
outage, so the pipeline does it now.

**Why Caddy is given a folder and not a file.** The compose files mount `./caddy/staging` (or
`./caddy/prod`) as `/etc/caddy`, not the one Caddyfile. Docker mounts a single file by what it is on
the disk at that moment; a deploy that replaces the file leaves a running Caddy looking at the old
one, and its reload re-reads the old one too. A folder is looked into afresh each time.

**A new `.env` line still has to reach the server before the build that needs it** — see the
Turnstile example under "Environment variables" below. On production the compose file enforces
this itself; on staging it is still a thing to remember.

---

## The image — `Dockerfile` (repo root)

Multi-stage build. The build stage copies just the `.sln`, `Directory.Build.props`,
`Directory.Packages.props`, `Rules.ruleset`, `stylecop.json`, and every `.csproj` **before** copying
the rest of the source tree — this makes `dotnet restore` its own cached Docker layer, so a
source-only change doesn't re-download the whole dependency graph on every build. Runtime stage is
`mcr.microsoft.com/dotnet/aspnet:10.0`, listens on port `8080` (`ASPNETCORE_HTTP_PORTS=8080`).

`.dockerignore` excludes `bin/`/`obj/`/`.git/`/docs/`*.md` (README kept) from the build context.

---

## The stack — `deploy/docker-compose.staging.yml` + `deploy/caddy/staging/Caddyfile`

Two services on a private `internal` Docker network:

- **`web`** — the app container. **No ports exposed to the host** — only reachable through `caddy`.
- **`caddy`** — `caddy:2-alpine`, the only service with `80`/`443` published. Terminates TLS
  (via the Hetzner reverse-DNS hostname), enforces HTTP Basic Auth, and sets
  `X-Robots-Tag: noindex, nofollow` as a second line of defense against a search engine indexing
  staging: a copy of the real site under another address — belt and braces alongside the Basic
  Auth itself.

Two lines in the compose file are not secrets and are the same on staging and on the live site:

- **`TZ: Europe/London`.** A container's own clock is UTC, an hour behind the UK from late March
  to late October. Wherever the code asks what time or what day it is (which slots have passed,
  "Today's Jobs", the calendar's default day), it now gets a UK clock's answer, as it always did on
  a developer's machine. Without it a slot stayed on offer for an hour after it had started.
- **`DataProtection__KeysPath: /keys`**, with the volume `dataprotection_keys` mounted there. These
  are the keys that sign the login cookie, the antiforgery tokens, the time stamp on each public
  form and the password reset links. Kept inside the container, every deploy made new ones: the
  admin was signed out, a form open at that moment came back "that didn't go through", and a reset
  link already sent stopped working. The keys are stored as they are, not encrypted, and the
  framework says so in the log at start (`No XML encryptor configured`); the volume is on the same
  server as the `.env` that holds every other secret, so that is accepted.

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
the case in point: a build with Turnstile fails its form pages when the keys are missing. So the
order is: put the new lines into `.env` on the server, and only then push. The compose file that
maps them travels with the push.

Three settings have defaults in code and need no line in `.env` unless they are to change:
`RateLimiting:FormPostsPerWindow` (10), `RateLimiting:FormWindowMinutes` (10) and
`RateLimiting:SlotLookupsPerMinute` (60). They are not mapped in the compose file; add
`RateLimiting__FormPostsPerWindow: "20"`-style lines there to override one.

---

## Production — `deploy-prod.yml`, `docker-compose.prod.yml`, `caddy/prod/Caddyfile`

The same pipeline, pointed at the live server. What differs, and why:

| | Staging | Production |
| --- | --- | --- |
| Runs on | every push to `dev` | every push to `main`, which only a merged pull request from `dev` makes |
| Image tag | `latest` | `prod`; the commit-id tag is pushed too, for going back a version |
| Jobs | one | two: build and test, then deploy. Only the second is given the server's key |
| Secrets | `STAGING_HOST`, `STAGING_SSH_KEY`, on the repository | `PROD_HOST`, `PROD_SSH_KEY`, on a GitHub **environment** named `production` that only `main` may use |
| `ASPNETCORE_ENVIRONMENT` | `Staging` | `Production` |
| Slots | 14 days made at each start | none made: an admin generates them (`WORKFLOW_BOOKINGS.md`) |
| In front of Caddy | nothing | Cloudflare's proxy |
| Certificate | Let's Encrypt, got by Caddy itself | Cloudflare's origin certificate, two files in `certs/` on the server |
| Ports published | 80 and 443 | 443 only |
| Password prompt, `noindex` header | both | neither |
| A setting missing from `.env` | the page that needs it fails | the deploy stops and says which one |
| Email settings | its own notice address and a `[STAGING]` mark | none: the code's own (`bookings@` sends, `info@` receives, no mark) |
| Photo bucket | shared with development | its own |

**Why the secrets sit on an environment.** A repository secret can be read by a workflow on any
branch, so a workflow file changed on `dev` could use the live server's key. An environment's
secrets are handed only to a job that names the environment, and the environment is set to allow
the `main` branch alone.

**Why the image is built again and not carried over from staging.** `main` and `dev` hold the same
files after a merge, and the tests run again on them, so the two images are made from the same
source. Carrying staging's image over would be one build fewer, at the cost of finding which commit
on `dev` a merge commit on `main` came from. Not worth the moving parts here.

### Cloudflare in front, and what that changes in the Caddyfile

- **The certificate.** Caddy's usual free certificate needs the certificate authority to reach the
  server directly, and Cloudflare stands in between. So the live Caddyfile names two files,
  `/certs/origin.pem` and `/certs/origin.key`: Cloudflare's own origin certificate, made in its
  dashboard (SSL/TLS, Origin Server) and valid for years. Cloudflare's SSL mode is **Full
  (strict)**: it speaks HTTPS to the server and checks that certificate. Only Cloudflare trusts it,
  so the proxy has to stay switched on for the site's records; with it off a browser is shown a
  certificate warning.
- **The visitor's address.** Every request now arrives from one of Cloudflare's servers, with the
  visitor's address in the `CF-Connecting-IP` header. The Caddyfile lists Cloudflare's published
  address ranges as `trusted_proxies` and names that header in `client_ip_headers`, then hands the
  app one address in `X-Forwarded-For`: the visitor's. Left alone, Caddy would add Cloudflare's
  address behind the visitor's, the app reads the last one, and every visitor would share the form
  limit of a handful of Cloudflare servers. A request that reaches the server without going through
  Cloudflare is taken to come from the address it came from, whatever its headers claim. The list
  of ranges is Cloudflare's own (`https://www.cloudflare.com/ips/`); it changes rarely, and the date
  it was read is in the file.
- **`www`** answers with a permanent redirect to the name without it.
- **A request that arrives during a deploy waits.** `lb_try_duration 30s` makes Caddy keep trying
  the app's container for up to 30 seconds when it cannot be reached, which is what happens for a
  few seconds while a deploy replaces it. Only a request that could not be handed over at all is
  tried again, so a form is never sent twice.

How the Caddyfile was checked before it went near a server: run on a developer's machine with a
test certificate and a program behind it that answers with the request it was sent. A request
claiming to be someone else reached the app as the address it really came from; with that machine
listed as a trusted proxy, the same request reached the app as the address in `CF-Connecting-IP`
and nothing else; `www` redirected; a request sent while the app's container was stopped was
answered `200` ten seconds later, once it was back, and `502` at once with the wait taken out.

### What has to be on the live server before its first deploy

The workflow copies two files and restarts two containers. Everything else is put there once, by
hand, and the first deploy fails without it:

1. `/opt/handyfix/deploy/.env`, every line of `deploy/.env.prod.example` filled in.
2. `/opt/handyfix/deploy/certs/origin.pem` and `origin.key`, the Cloudflare origin certificate.
3. The public half of a deploy key of the live server's own in `root`'s `authorized_keys`, and its
   private half in the `production` environment as `PROD_SSH_KEY`, beside `PROD_HOST`.
4. A database login for the live site, with rights in `handyfix_prod` and nowhere else.
5. In the accounts the settings come from: the live Stripe key and webhook, a Brevo key, the real
   domain on the Turnstile widget's hostname list, and the photo bucket.

The first start of an empty database also makes the admin account, from `ADMIN_SEED_EMAIL` and
`ADMIN_SEED_PASSWORD`; see "Startup sequence" below.

### Going back a version

Every image the live site has run is still in the registry under its commit id. On the server:

```bash
cd /opt/handyfix/deploy
IMAGE_TAG=<commit id> docker compose -f docker-compose.prod.yml up -d
```

The next deploy puts the site back on `prod`. This goes back the code only: a database migration
that the newer version ran is not undone, so check what the versions in between changed in the
database before relying on it.

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
| You added a setting to a compose file, pushed, the deploy is green, and the app still does not have it | The compose file reached the server (it is copied at every deploy), so look at `.env` there: the line the mapping reads is missing or empty. `.env` is the one file the pipeline never touches. |
| The live deploy stops at `required variable ... is missing a value` | A line the live compose file insists on is not in the server's `.env`. Nothing was restarted; the site is still on the version before. Add the line and run the workflow again. |
| The deploy fails at the `caddy reload` step after five tries | The Caddyfile that was just copied over does not read: the step's output has Caddy's own message with the line. Caddy is still running on the one before. Fix the file and push. |
| App container crashes on boot against a new, empty database | Almost always `Admin:SeedEmail` or `Admin:SeedPassword` (`ADMIN_SEED_EMAIL`, `ADMIN_SEED_PASSWORD` in `.env`) missing, or a password under 10 characters — the log names which. See the startup sequence above. A database that already has its admin needs neither. |
| `ADMIN_SEED_PASSWORD` was changed in `.env` and the old password still signs in | By design: it is read at the first start only. Change the password on the admin panel's Account page. |
| Caddy won't start / Basic Auth rejects a known-correct password | The hash in `BASIC_AUTH_HASH` doesn't match — regenerate with `docker run --rm caddy:2-alpine caddy hash-password --plaintext '<password>'` and update `.env`. |
| SSH deploy step fails with a key error | `STAGING_SSH_KEY` secret is missing, malformed, or the corresponding public key isn't authorized on the staging host. |
| Photo uploads fail on staging | `CloudflareR2:*` is configured as of 2026-08-05 (see above) — if this still happens, check the five `R2_` lines in the server's `.env`. |
| `/Contact`, `/JoinOurTeam` and `/Booking` fail, the rest of the site works | The Turnstile keys are missing from the container: `Turnstile is not configured for this environment` in the log. Check both lines are in the server's `.env`. |
| Every page `HomeController` serves fails | `Brevo:ApiKey` is missing. The enquiries service needs the email sender, and outside Development the sender refuses to exist without a key. |
| A booking is saved, then the customer gets an error page where Stripe should open | `Stripe is not configured for this environment` in the log: `STRIPE_SECRET_KEY` is missing from the server's `.env`. |
| Stripe shows a webhook delivery to staging as failed with `401` | Staging's Caddyfile lets that one path past the password prompt; a `401` means the Caddy that answered was not running that file. Run the deploy again and read its `caddy reload` step. |
| The admin is signed out after every deploy, or a form open during one comes back "that didn't go through" | The keys are not being kept: check the compose file still maps `DataProtection__KeysPath` and mounts the `dataprotection_keys` volume at that path. |
| A slot that started within the last hour can still be booked in summer | The container's clock is UTC again: check `TZ: Europe/London` is in the compose file (`docker compose exec web date` shows the clock the app sees). |
| Stripe shows a webhook delivery as failed with `400`, and the log says `A call to the Stripe webhook was refused` | `STRIPE_WEBHOOK_SECRET` is not the signing secret of the webhook that is calling. Each webhook has its own, and a sandbox's differs from the live one. Deposits are still confirmed when the customer comes back from Stripe, but not when they close the tab first. |
| A `Secure`-flagged cookie won't persist, or a generated absolute URL (e.g. Stripe redirect URLs) comes back `http://` instead of `https://` | `Request.Scheme`/`IsHttps` reading wrong behind Caddy — see "Why the app has to trust Caddy's forwarded headers" above. Confirm `UseForwardedHeaders()` is still the first middleware in `Program.cs`. |

For real hostnames/credentials and a step-by-step walkthrough of each failure above with actual
values, see `docs/private/STAGING_RUNBOOK.md`.

---

## Related

- Full architectural history: `PROJECT_STATE.md` Section 3v–3w and Section 4 Tier 4. Stripe on
  staging: `PROJECT_STATE.md` Section 3cg. The production pipeline, the copying of the compose file
  and Caddyfile, the keys folder and the UK clock: `PROJECT_STATE.md` Section 3ci.
- The forwarded-headers/cookie-consent bug, full root-cause writeup: `PROJECT_STATE.md`
  Section 3ac.
- Mobile-scroll fix, silent-booking-error fix, CloudflareR2 wiring, and the compose-file-drift bug
  this page now warns about: `PROJECT_STATE.md` Section 3ae.
- Real infrastructure values, SSH keys, and the SQL login's password:
  `docs/private/INFRASTRUCTURE.md`.
