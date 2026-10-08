# 🛡️ Public Forms — What a Submission Goes Through

The site has three forms anyone can send: **Contact** (`/Contact`), **Join Our Team**
(`/JoinOurTeam`) and **Booking** (`/Booking`). This is everything that stands between a submission
and the database, in the order it happens, and what the visitor sees at each step.

Full history and reasoning: `PROJECT_STATE.md` Section 3cb.

**A fourth form carries the same guard: "Forgot password?"** on the login page. It is not a
customer's form, but anyone can open it and each send is an email to the owner. Checks 1 to 5 below
apply to it as written, with two differences: its rate limit is the account allowance it shares
with signing in, not the forms' one, and a submission the guard drops gets the page's usual
"an email may be on its way" answer. `WORKFLOW_ADMIN_ACCOUNT.md` has the rest.

---

## The order of checks

| # | Check | Where | If it fails, the visitor sees |
| --- | --- | --- | --- |
| 1 | **Rate limit** | `RateLimits`, `[EnableRateLimiting]` on the action | the site's "too many attempts" page (429) |
| 2 | **Antiforgery token** | ASP.NET Core, on every POST | the site's "that didn't go through" page (400) |
| 3 | **The form's own rules** | attributes on the input model | the form again, with a message on each field |
| 4 | **Hidden box and time** | `FormGuard` | *nothing different*: the usual thank-you, and nothing is saved |
| 5 | **Person check (Turnstile)** | `FormGuard` → `TurnstileVerifier` | the form again: "we could not confirm that you are a person" |
| 6 | Contact and Join only: **same enquiry again?** | `InquiriesService.IsRecentDuplicateAsync` | the usual thank-you; saved once |
| 6 | Booking only: **postcode in our area?** | `IServiceAreasService.IsPostcodeServedAsync` | the form again, with an enquiry link and the phone number |
| 7 | **Photos** | `ImageService` | a photo we do not take: the form again with the reason. Storage down: carries on without photos |
| 8 | Save, then email | the services | a failed email is logged and never shown as a failed submission |

Steps 3, 6 and 7 are described where they belong: `WORKFLOW_BOOKINGS.md` Step 2 and
`WORKFLOW_ENQUIRIES.md`. This document covers 1, 2, 4 and 5, and the error pages.

**Why the cheap checks come first.** A submission caught by the hidden box costs nothing; one that
reaches Turnstile costs a call to Cloudflare. So Turnstile is asked last, and only about
submissions the free checks let through.

---

## 4. The hidden box and the time check — `FormGuard`

Each public form renders the `_FormGuard` partial just above its button. It adds two things:

- **A text box no person sees.** It is moved off the page by `forms.css` (`.form-extra`) and taken
  out of the Tab order. A person never fills it in; a program filling in every field does.
- **A stamp of when the form was shown**, signed with the site's data-protection keys so it cannot
  be made up.

A submission is taken for a program's when the box has something in it, when the stamp is missing
or not one of ours, or when it comes back **in under three seconds**.

**What happens then is deliberately nothing visible.** Contact and Join Our Team answer with their
usual thank-you and save nothing, so a program learns nothing from the reply. A booking cannot be
"thanked", because its success is the payment page: it comes back with *"We could not take this
booking online. Please call us or message us on WhatsApp"*, which gives nothing away and still tells
a person how to book.

Two details that protect real people:

- **A form shown again keeps its first stamp.** When a form comes back with an error and the visitor
  corrects it and sends again two seconds later, the time is counted from when they first saw the
  form, not from the redisplay. Without this a quick correction would be dropped in silence.
- **Every drop is logged** as `Submission of the {form} form dropped as automated: {reason}`, with
  nothing of what was typed. If a real person is ever caught, this is where it shows.

There is no upper limit on the time: a form left open overnight still sends.

---

## 5. The person check — Cloudflare Turnstile

Turnstile is Cloudflare's replacement for a CAPTCHA. A small widget in the form checks the
visitor's browser, usually without asking them anything, and puts a token into the form. **The
token is worth nothing until the server has asked Cloudflare about it**, which is what
`TurnstileVerifier` does.

A token passes when Cloudflare says all of:

- `success` is true,
- it was earned on **this form** (the widget's `action`, one of `FormNames`), so a token from the
  Contact form cannot be spent on a booking,
- it was earned on **this site** (the `hostname` is the one the request came to).

### Keys

| Setting | In `.env` on the server | What it is |
| --- | --- | --- |
| `Turnstile:SiteKey` | `TURNSTILE_SITE_KEY` | public; it is in every form's HTML |
| `Turnstile:SecretKey` | `TURNSTILE_SECRET_KEY` | secret; never in a tracked file |

Both come from the Cloudflare dashboard: Turnstile → the widget → Settings. **The widget must list
the hostname the site is served on**, or it shows an error instead of the check.

- **No keys, in Development:** the check is off. No widget, no script from Cloudflare.
- **No keys anywhere else:** the form pages fail, on purpose. A deployed site must not run its
  forms unprotected without anyone noticing; this is the same rule as the email and payment keys.
- **One key without the other:** fails everywhere. It is always a mistake.

Cloudflare publishes test keys that work on any hostname. With a test *secret* only `success` is
read, because its answers carry a made-up hostname and action:

| | Site key | Secret key |
| --- | --- | --- |
| Always passes | `1x00000000000000000000AA` | `1x0000000000000000000000000000000AA` |
| Always fails | `2x00000000000000000000AB` | `2x0000000000000000000000000000000AA` |

A real secret refuses the token a test site key makes, so the two keys are always changed together.

### When Cloudflare cannot be asked

If the call fails or takes more than five seconds, **the submission is let through** and
`Turnstile could not be reached` is logged as an error. That is a choice: an outage at Cloudflare
is not the visitor's doing, and refusing every enquiry and booking until it is back would cost more
than what the hidden box, the time check and the rate limit let through meanwhile.

A wrong secret key is different: Cloudflare answers, and says no. Every submission is then refused
with the "could not confirm" message, and the log says `Turnstile does not accept this site's secret
key`.

### In the page

- The widget takes its full shape up to 400px wide. Where the inside of a form is narrower than
  the 300px that shape needs (the Contact and Join forms on a phone), the partial asks for the
  compact one before Cloudflare's script draws it.
- `site.js` holds a form that is sent before the token is there and shows the hint under the
  widget, so the visitor is not sent round a failed submission for being quick.
- A token lasts five minutes and can be used once. The widget renews it by itself, and a form that
  comes back from the server gets a new widget and a new token.

---

## 1. The rate limit — `RateLimits`

ASP.NET Core's own rate limiter, with two policies:

| Policy | On | Default | Setting |
| --- | --- | --- | --- |
| `forms` | the three form POSTs, **one allowance between them** | 10 in 10 minutes | `RateLimiting:FormPostsPerWindow`, `RateLimiting:FormWindowMinutes` |
| `slot-lookups` | `/Booking/GetSlots` | 60 a minute | `RateLimiting:SlotLookupsPerMinute` |

Ten, not fewer, because a submission the form's own rules refuse counts too, and a mobile network
can put many customers behind one address.

**Who counts as one visitor** is the address the request came from (`RateLimits.ClientKey`). An
IPv6 address counts by its first half, the part a provider gives to one customer; the second half
is the customer's to change at will.

Past the limit the answer is `429` with a `Retry-After` header. A browser sending a form is shown
the site's own page for it; the booking page's script, which asked for slots, says *"please wait a
minute, then pick the day again"*.

> **Behind Cloudflare the address must be the visitor's, not Cloudflare's.** The app takes the
> address from `X-Forwarded-For` as Caddy sets it. On staging Caddy faces the internet, so that is
> the visitor. Production sits behind Cloudflare's proxy, and unless Caddy is told to trust
> Cloudflare's forwarded address, every visitor will look like one of a handful of Cloudflare
> servers and share one allowance. This belongs to the production Caddyfile, roadmap L4 item 5.

---

## The site's own error pages

A response that ends in an error status with nothing in it is shown as the site's own page for
that status, by `ErrorsController.StatusPage` and `Views/Shared/StatusPage.cshtml`. The status code
is kept.

| Status | When | Says |
| --- | --- | --- |
| 404 | a page, service or area that is not there | "We can't find that page." |
| 429 | the rate limit | "That's a lot of tries in a short while." |
| 400 | a form whose antiforgery token expired | "That didn't go through." |
| 403 | no access | "This page isn't open to you." |
| 500+ | an unhandled exception | "Something went wrong on our side." |

Two things worth knowing:

- **Only a request that asked for a page gets one** (its `Accept` header names `text/html`). A
  script fetching data, a crawler probing for files and the payment provider calling back get the
  bare status as before, and cost no page render.
- **`ErrorsController` takes no services, on purpose.** It has to be able to show itself when
  something the rest of the site needs is what broke. A request reaches it as it was first sent, so
  the action accepts any verb and skips the antiforgery check: a form whose token had expired
  arrives as a POST with that same bad token.

---

## Adding another public form

1. Give it a name in `FormNames` (letters, digits, hyphens, underscores).
2. In its view, put `<partial name="_FormGuard" model="@FormNames.YourForm" />` just above the button.
3. On its POST action, add `[EnableRateLimiting(RateLimits.FormsPolicy)]`.
4. In the action, after `ModelState.IsValid`, call `formGuard.CheckAsync(HttpContext, FormNames.YourForm)`
   and handle both results: `Automated` (answer as if it worked, save nothing) and
   `ChallengeFailed` (show the form again with `FormNames.ChallengeFailedMessage`).
5. Use the shared validation attributes for names, email and phone (`WORKFLOW_BOOKINGS.md` Step 2).

The double-send lock in `site.js` covers every POST form without being asked.

---

## Tests

| What | Where |
| --- | --- |
| The hidden box, the time, the kept stamp | `Services/FormGuardTests.cs` |
| Turnstile, with Cloudflare played by a fake handler | `Services/TurnstileVerifierTests.cs` |
| Who counts as one visitor; the status page's words | `Services/RateLimitsTests.cs` |
| All of it through the whole stack | `FormsWebTests.cs`, `StatusPagesWebTests.cs` |

The shared test host (`SqliteWebApplicationFactory`) sets the minimum time to nothing and all but
lifts the rate limit, because a test sends a form the instant it has fetched it. Tests of the guard
and of the limit put the real values back for themselves.

---

## Quick troubleshooting

| Symptom | Cause |
| --- | --- |
| The three form pages fail on a deployed site | No Turnstile keys. Set `TURNSTILE_SITE_KEY` and `TURNSTILE_SECRET_KEY` in the server's `.env`, and make sure the compose file on the server maps them. |
| The widget shows an error instead of the check | The widget in Cloudflare does not list this hostname, or the site key is not this widget's. |
| Every submission comes back with "could not confirm that you are a person" | The secret key is wrong, or belongs to a different widget than the site key. The log says which. |
| A customer says they sent an enquiry and it never arrived | Look in the log for `dropped as automated` at that time. |
| A customer hits "too many attempts" | Ten forms in ten minutes from one address. It clears by itself; they can call meanwhile. If it happens to many people at once in production, see the Cloudflare note above. |
| A form shows "that didn't go through" after a deploy | The page was open across the deploy. Until the data-protection keys are kept between deploys (roadmap L4 item 5), every deploy invalidates open forms and signs the admin out. |

---

## Related

- The booking form's rules, postcode check and emails: `docs/WORKFLOW_BOOKINGS.md`.
- Enquiries, their emails and the duplicate check: `docs/WORKFLOW_ENQUIRIES.md`.
- Where the settings live on a server: `docs/WORKFLOW_DEPLOYMENT.md`.
