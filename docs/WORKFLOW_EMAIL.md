# 📧 Email — What the Site Sends, From Which Address, To Whom

One page for every email the site sends and every setting that decides where it goes. What each
email says, and why, stays with its feature: `WORKFLOW_BOOKINGS.md` for the four booking emails,
`WORKFLOW_ENQUIRIES.md` for the two enquiry emails.

**Code:** `src/Services/HandyFix.Services.Messaging/` (the senders),
`src/Services/HandyFix.Services.Data/Common/EmailSettings.cs` (the addresses and their settings),
`src/Services/HandyFix.Services.Data/Common/EmailSenderExtensions.cs` (`TrySendEmailAsync`),
`src/Services/HandyFix.Services.Data/Common/EmailLayout.cs` (how every email looks).

---

## The rule: role addresses only

**No person's own address goes into any setting, on any server.** Every address the site sends
from or to is a role address on the business's domain. Who reads a role address is one forwarding
rule in Cloudflare Email Routing, so when a person changes, one rule changes and no server, key or
document does.

| Address | Role |
| --- | --- |
| `bookings@` | The sender of every email the site sends. A customer's reply to it reaches the company inbox. |
| `info@` | The public address, on the site and in the footer. Every notice to the company goes here on the live site. |
| a developer address | Where staging sends its notices, so test enquiries stay out of the company inbox. It is a role address like the others and forwards to whoever tests staging. Its name is not written in this repository. |

The domain has no mailboxes of its own. Sending goes through Brevo's API; receiving is Cloudflare
Email Routing, which forwards each address to the company's one shared mailbox
(`PROJECT_STATE.md` Section 3bt).

---

## The seven emails

| Email | Sent when | To | A reply goes to | Subject |
| --- | --- | --- | --- | --- |
| Deposit paid | the payment succeeds | the customer | `bookings@` | Your Plumbing Handyman Surrey Booking is Confirmed! |
| Deposit paid, notice | the payment succeeds | the company | **the customer** | New Confirmed Booking - {name} |
| Deposit for a booking not held, notice | a deposit arrives for a booking that was dropped or cancelled, or was already paid | the company | **the customer** | Action needed: deposit paid for a booking that is not held - {name} |
| Technician picked | an admin picks a technician on a paid booking, or changes it to another | the customer | `bookings@` | Your technician for your Plumbing Handyman Surrey booking |
| Enquiry or application, notice | an enquiry or a job application is saved | the company | **the sender** | New enquiry - {name}, or New job application - {name} |
| Acknowledgement | an enquiry or a job application is saved | the sender | `bookings@` | We have received your enquiry, or application |
| Password reset link | "Forgot password?" is sent with the admin's login email | the admin's login email | `bookings@` | Reset your Plumbing Handyman Surrey admin password |

All seven are sent from `bookings@`. The three notices carry the customer's address as Reply-To,
so pressing Reply in the company inbox answers the customer and not the site.

**A customer who books on the website gets two emails and no more**: one when the deposit is
paid, one when a technician is picked. Nothing goes out before the deposit, because a booking
that is never paid is dropped fifteen minutes later. Nothing goes out when a job is cancelled or
moved, and nothing at all for a job the admin wrote in by hand, whatever email address is on it:
in each of those the admin is already speaking to the customer (`PROJECT_STATE.md` Section 3ce).

Every one but the reset link follows a save and goes through `TrySendEmailAsync`: a send that fails is logged
as `Email not sent: {which email}` and does not undo or hide what was saved. The technician email
is the one whose failure the admin is told about at once, in the line the booking's page answers
with. For the others nothing in the admin panel shows a failed send yet (`PROJECT_STATE.md`
Launch Sprint L4 item 15). The reset link goes
the same way and fails the same way; `WORKFLOW_ADMIN_ACCOUNT.md` has the rest of that flow. It is
the one email that goes to an address chosen in the admin panel, not in a setting, so **the admin's
login email has to be a mailbox that can be opened**.

---

## How an email looks, and where to change it

Every email has the same look, and it is built in one place: `EmailLayout`. The logo across the
top, a coloured line under it, a badge that says what the email is, the content, a footer.

| Wrapped by | Which emails | Line under the logo | Footer |
| --- | --- | --- | --- |
| `ForCustomer` | the two to a customer about a booking, and the acknowledgement | gold | the phone number, the email address, the areas served, and the company behind the business: its name, its number, where it is registered and its registered office |
| `ForCompany` | the three notices to the company | teal | one line |
| `ForAccountSecurity` | the password reset link | navy | one line |

An email's own words stay in the service that sends it. The service puts its content together from
`EmailLayout`'s pieces (`Badge`, `Heading`, `Lead`, `Details` and its rows, `Note`, `Advice`,
`Button` and a few more) and hands it to one of the three methods above. **To change how every
email looks, change `EmailLayout`. To change what one email says, change its service.**

- **Every piece takes HTML.** What a person typed goes through `EmailText.Encode` first, as it
  always did. `EmailLayout` encodes nothing.
- **Tables, with the styles written on each cell, on purpose.** A mail program understands far less
  than a browser: Outlook ignores a stylesheet, margins and most of what a `div` can do. The one
  stylesheet, in the head, is for phones, where each labelled line stacks under its label.
- **The logo is a file on the live site**, `wwwroot/images/email/logo-header.png`, and every email
  points at it there (`EmailLayout.LogoUrl`), whichever copy of the site sent it. Gmail does not
  show a picture packed inside an email, and staging sits behind a password. Until launch the live
  Caddyfile leaves `/images/email/*` outside its password for the same reason. **Do not rename or
  move the file**: every email already sent points at that address.
- **A button in a notice opens the admin panel of the site that sent it**: the job's own page for
  a deposit, the Enquiries list for an enquiry. The address comes from `Site:PublicUrl`
  (`EmailSettings.SiteUrl`). Staging's compose file sets it from `STAGING_HOSTNAME`; with
  nothing set it is the live site's address.
- **A job application is acknowledged without the "Is your request urgent?" box.** That box is
  about leaks and emergency repairs.

---

## Settings, per environment

| Setting | In `.env` | Local | Staging | Live site |
| --- | --- | --- | --- | --- |
| `Brevo:ApiKey` | `BREVO_API_KEY` | unset: nothing is sent | its own key | its own key |
| `Email:BookingsFromAddress`, `Email:SystemFromAddress` | `EMAIL_BOOKINGS_FROM_ADDRESS`, `EMAIL_SYSTEM_FROM_ADDRESS` | unset | empty | empty |
| `Email:SubjectPrefix` | `EMAIL_SUBJECT_PREFIX` | unset | `[STAGING]` | empty |
| `Admin:NotificationEmail` | `ADMIN_NOTIFICATION_EMAIL` | unset | the developer address | empty |
| `Site:PublicUrl` | none: staging's compose file builds it from `STAGING_HOSTNAME` | unset | staging's own address | unset |

Empty or unset means the default in `EmailSettings`: `bookings@` as the sender, `info@` for
notices, no mark on the subject, and the live site's address behind a notice's button.

- **Staging sends from the real address on purpose.** Brevo refuses a sender it has not verified,
  and the domain's signing records only prove themselves on a real send. Staging finds a fault in
  either before the live site depends on them.
- **Staging marks its subjects** because of that: sent from the same address, a test email would
  otherwise read exactly like a real one. `SubjectPrefixEmailSender` wraps the Brevo sender when
  the setting holds anything.
- **One Brevo key per environment**, both from the company's Brevo account. Either can be deleted
  in Brevo without touching the other. Brevo shows a key once, when it is made: it goes straight
  into the server's `.env` and nowhere else, and a lost key is replaced with a new one.
- **A missing key outside Development stops the site**, not only its emails: every page
  `HomeController` serves fails until `Brevo:ApiKey` is set (`WORKFLOW_ENQUIRIES.md`).

---

## What the domain needs in DNS

All three are public records and can be read with `nslookup -type=TXT`:

- **SPF**: one `TXT` record on the domain naming Cloudflare's mail servers and Brevo together. A
  domain may have only one SPF record, so a new sending service is added to that line.
- **DKIM**: Brevo's two selectors, `brevo1._domainkey` and `brevo2._domainkey`.
- **DMARC**: `_dmarc`, at `p=none`, which reports and refuses nothing. Do not tighten it until a
  reply sent from the company mailbox as a domain address has been checked to pass, or those
  replies are what gets refused.

---

## Checking it after a change

1. Send one enquiry through the Contact form on staging, by hand: a browser driven by a program
   cannot pass the real person check (`WORKFLOW_FORMS.md`).
2. The application log has no `Email not sent` line for it.
3. Two emails arrive, each with `[STAGING]` in the subject: the notice at the developer address,
   the acknowledgement at the address typed into the form.
4. Open one in the mail program's "show original" view: SPF, DKIM and DMARC each say `pass`, and
   DKIM names the business's domain.
5. After a change to how the emails look: the logo shows at the top of both, on a phone as well,
   and the button under the notice opens staging's own admin panel.

---

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| `Email not sent` in the log, with `Brevo email send failed (401)` | The key in `.env` is wrong or was deleted in Brevo. |
| `Email not sent`, with a 400 naming the sender | The sender address is not on a domain Brevo has verified for this account. |
| The log is clean and nothing arrives at a domain address | The forwarding rule for that address in Cloudflare Email Routing is missing or switched off. |
| A real email carries `[STAGING]`, or a test one does not | `EMAIL_SUBJECT_PREFIX` is set on the wrong server. The line is in that server's `.env`, which is written by hand (`WORKFLOW_DEPLOYMENT.md`). |
| An email shows the business's name in words where the logo should be | The mail program could not fetch the logo from the live site. Open `/images/email/logo-header.png` on the real domain in a private window: it has to show without asking for a password. Before launch, the live Caddyfile's `BEFORE LAUNCH ONLY` block must still leave `/images/email/*` outside the password. Some mail programs also hide every picture until the reader allows them. |
| The button in a notice sent from staging opens the live site | `Site__PublicUrl` is missing from staging's compose file, or `STAGING_HOSTNAME` is empty in its `.env`. |
