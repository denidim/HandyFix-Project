# 🔑 The Admin Account — Signing In, Changing It, Getting Back In

The site has one account: the administrator's. This is where it comes from, what protects its
login, and what the owner can do to it without a developer.

Full history and reasoning: `PROJECT_STATE.md` Section 3cc.

> The admin area is routed as `Administration`, not `Admin`. `/Admin/...` will 404.

**Code:** `src/Data/HandyFix.Data/Seeding/AdminUserSeeder.cs`,
`src/Data/HandyFix.Data/IdentityOptionsProvider.cs`,
`src/Web/HandyFix.Web/Services/Accounts/`, `Areas/Identity/Pages/Account/`,
`Areas/Administration/Controllers/AccountController.cs`.

---

## Where the account comes from: two settings, read once

`AdminUserSeeder` runs at every start. **If the database already holds an administrator it does
nothing.** If it holds none, it makes one from two settings:

| Setting | In `.env` | What it is |
| --- | --- | --- |
| `Admin:SeedEmail` | `ADMIN_SEED_EMAIL` | the email the admin signs in with |
| `Admin:SeedPassword` | `ADMIN_SEED_PASSWORD` | the password, at least 10 characters |

**Both count at one start only: the first start of a database.** Changing either afterwards
changes nothing, because by then an administrator exists. So the real password and the real login
email are chosen **before** a new database starts for the first time. After that they are changed
on the Account page, by the admin.

Outside Development a first start with either setting missing stops the application and names the
missing one. In Development both fall back to fixed values written in the seeder, with a warning in
the log.

**Why the seeder looks for "an administrator" and not for one address.** It used to look for a
fixed address written in the code. The login email can now be changed in the admin panel, and the
first start after such a change would have made a second administrator, with the seed password. It
also keeps the real login out of a public repository.

---

## Signing in — `/Identity/Account/Login`

- **Five wrong passwords in a row pause the login for five minutes**, for the right password too
  (`IdentityOptionsProvider`). A sign-in that works sets the count back to nought.
- **Ten sign-in or reset attempts in ten minutes per address**, then the site's "too many attempts"
  page (`RateLimits.AccountPolicy`). It is an allowance of its own, apart from the customer forms'.
  Opening the login page does not count; sending it does.
- **One message for every failure**: *"That email and password did not match. After 5 wrong tries
  in a row the login pauses for 5 minutes."* It is the same for a wrong password, for an address
  that is not a login, and for a paused login, so the page tells a stranger nothing about which
  addresses are logins, and tells the owner the rule he may have run into.

The pause is per account and the allowance is per address, and each covers what the other cannot:
the allowance stops one computer guessing fast, the pause stops many computers guessing slowly.

---

## The Account page — `/Administration/Account`

In the admin panel's sidebar. Three things:

- **Change password**: the current one, then the new one twice. The admin stays signed in on this
  browser; any other browser signed in to the account is signed out within half an hour, which is
  how often Identity checks a sign-in against the account.
- **Change login email**: the new address and the current password. The password is asked for
  again so that a panel left open on a shared computer is not enough to move the login, and with
  it the password reset, to somebody else's mailbox. No confirmation email is sent; the password
  is the proof.
- **Sign out.**

The only password rule is length: at least 10 characters. A few words in a row pass; a short
password with a capital, a digit and a symbol does not.

---

## Forgot password — `/Identity/Account/ForgotPassword`

1. The admin types the login email. The form carries the same guard as the customer forms
   (`WORKFLOW_FORMS.md`): the hidden box, the time check, the person check.
2. **The answer is always the same**: *"If that address is the admin login, an email with a link is
   on its way."* Nothing is sent for any other address, and the page does not say which it was.
3. The email (`WORKFLOW_EMAIL.md`) holds a link to `/Identity/Account/ResetPassword`. **It works
   for two hours and once.**
4. The admin chooses a new password and lands on the login page, told it has been changed.
   **A reset also ends a pause**: forgetting the password is the usual reason for five wrong tries.

A link that is too old, already used or cut short in a mail program gets *"This link no longer
works"* and a button to ask for a new one.

**Until the data-protection keys are kept between deploys, a deploy also ends every link sent
before it** (`PROJECT_STATE.md` Launch Sprint L4 item 5). Ask for a new one.

---

## The other account pages are closed

The Identity UI package brings about thirty pages of its own under `/Identity`, in its stock look:
"delete my account", two-step sign-in setup, an email change that waits for a confirmation nobody
is sent. A signed-in admin could open all of them. **Only the six the site uses have a route**:
Login, Logout, AccessDenied, ForgotPassword, ResetPassword, and Register (which answers 404 by
itself while sign-up is closed). Everything else is the site's own "not found" page.

`IdentityPages` holds the list. It lists what is **open**, so a page a later version of the package
adds starts closed, and `AdminAccountWebTests` asks the running site for its `/Identity` routes and
fails if there is one more.

---

## Troubleshooting

| Symptom | Cause |
| --- | --- |
| The right password is refused | Five wrong tries came first. Wait five minutes, or use "Forgot password?", which also ends the pause. |
| The reset email does not arrive | The address typed is not the login email; or the send failed, which is `Email not sent: password reset link, to the admin` in the log; or the login email is not a mailbox anyone can open. |
| "This link no longer works" on a fresh link | A deploy happened between the email and the click (see above), or the link was opened once already. |
| The app stops at its first start naming `Admin:SeedEmail` or `Admin:SeedPassword` | A new database and the setting is missing from `.env`, or from the compose file on the server. |
| `ADMIN_SEED_PASSWORD` was changed and the old password still works | Expected: it is read at the first start only. Change the password on the Account page. |
| Nobody can sign in and the login email is a dead mailbox | A developer's job, in the database: on the administrator's row in `AspNetUsers` set `Email` and `UserName` to a mailbox that works, and `NormalizedEmail` and `NormalizedUserName` to the same in capitals. Then "Forgot password?" reaches it. |
