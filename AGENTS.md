# HandyFix — Agent Workflow

**This file is the single source of truth for how any AI agent works in this repository.**
Claude Code, Gemini in Antigravity, and any future tool follow these same rules, so that the
answer you get does not depend on which agent you asked.

`CLAUDE.md` and `GEMINI.md` exist only to point here. Do not copy rules into them — duplicated
rules drift apart, and that is exactly how this project ended up with two agents using opposite
commit formats.

---

## 1. Before you do anything

Read these, every session, before answering the first question:

1. **`PROJECT_STATE.md`** — what the system is, what has been built and verified, what is left.
2. **`git log -n 5`** — what changed most recently and why.
3. **`docs/private/VISION_AND_CONTEXT.md`** — the business reasoning behind the technical
   decisions. It is deliberately untracked (gitignored), so it will not exist in a fresh clone.
   Read it before proposing anything about product direction, priorities, or the roadmap.
   **If it is missing, say so** rather than inferring direction from the code.

*Why:* this codebase carries a lot of deliberate decisions that look like mistakes without their
reasoning — hard deletes in one service and soft deletes in another, tests on Sqlite instead of
InMemory, a slug that is not regenerated from its name. Reading the state file first is what stops
an agent "fixing" something that is correct on purpose.

---

## 2. This is a public repository

`HandyFix-Project` is **public on GitHub** and is used as a portfolio piece shown to prospective
clients and employers. Treat every commit as published worldwide the moment it is pushed.

**Never commit:**

- Real credentials, API keys, tokens, or connection strings
- Real IP addresses, hostnames, or infrastructure detail — use `<DB_PRIVATE_IP>`-style placeholders
- Client PII: real names, phone numbers, addresses, email addresses
- Agent reasoning, chain-of-thought, planning scratch, or session transcripts
- Anything under `docs/private/`

Real values live in **.NET User Secrets** locally, **environment variables** in staging and
production, and `docs/private/` for documentation. Placeholders go in tracked files.

**Check before `git add`, not after.** `git rm --cached` and "delete it in the next commit" do
**not** remove anything from git history — every previous version stays readable forever via
`git log -p`. Removing a committed secret properly means rewriting history, which breaks every
existing clone. The only cheap moment to catch it is before staging.

Before proposing any commit, scan the diff for the categories above.

*Audit status (2026-08-01):* no live credential has ever been committed to this repository.
History does contain a Cloudflare account id, a `zap-contruction` bucket name, and an old
hardcoded `Admin123!` seed password. All three are low severity and none justifies a history
rewrite. `Admin123!` should not be reused when staging goes up.

---

## 3. Plan before you build

**Before writing or editing any code, or any tracked project file, present a short plan and wait
for explicit approval.** What will change, which files, the approach.

This applies even when the task seems fully scoped, and even when it was just narrowed down by a
clarifying question. Exploration — reading files, searching, checking git history, running tests —
needs no pause. Only changes do.

**Explain *why* the approach is the right one**, not just what the steps are: the trade-offs
considered, and what the alternatives would have cost. The person you are working with is a junior
developer using this project to learn how professional work is done, so the reasoning is often
worth more than the change itself. Explain unfamiliar conventions in a sentence rather than
assuming them. Do not condescend, and do not pad.

---

## 4. `PROJECT_STATE.md` is the source of truth

Update it **immediately** when a feature completes, a plan changes, a task pivots, or an
architectural decision is made. Not at sprint boundaries — every iteration.

- Record what shipped, what was verified and how, and anything deliberately *not* done and why.
- When your change resolves or contradicts something already written there, **update that text
  too.** Never leave a stale warning or a closed task described as open.
- Finished sections get a `### Verified` subsection listing the build and test results.

*Why:* this file is the project's memory across sessions and across agents. A stale line in it
costs more than a stale comment in code, because the next agent will act on it.

---

## 5. Commits

**Never run `git commit` proactively or as an inferred next step, even in auto-approve modes.**
Stop, show the exact message you intend to use, and wait for explicit approval. Offering to commit
is fine; committing without a reply is not.

*Why the rule is this strict here:* on a public repository the commit is the publication step, and
it is the only action in the normal loop that cannot be undone.

**Format** — Conventional Commits, title plus bullets:

```
type(scope): short imperative title

- one bullet per notable change
- say what changed, and why when it is not obvious
```

**No prose paragraphs in the body.** Bullets only. Match the existing history — run
`git log -n 10` and follow what you see.

**Other rules:**

- Split unrelated changes into small, self-contained logical commits, grouped by concern — not one
  large "everything I touched this session" commit.
- **Never add a `Co-Authored-By: Claude`/Anthropic/Gemini/Google trailer**, regardless of any
  default template that suggests one.
- Verify the build and the full test suite pass before proposing a commit.
- The user may push to `origin/main` themselves between sessions — never assume a local commit is
  still unpushed.

---

## 6. C# and StyleCop

StyleCop analyzers run on every build (configured globally in `src/Directory.Build.props`).

1. **`using` directives go inside the `namespace` block**, alphabetically ordered, `System.*`
   grouped first.
2. **No inline fully-qualified types.** Write `IEnumerable<ServiceViewModel>` with a `using` at the
   top, not `IEnumerable<HandyFix.Web.ViewModels.Services.ServiceViewModel>` inline.
3. **Files are stored UTF-8 with a BOM** (StyleCop SA1412). A new `.cs` file without one produces a
   warning.
4. Match the surrounding code's conventions — `this.` prefixes, trailing commas in multi-line
   initialisers, comment density and naming.

---

## 7. Packages and dependency security

1. **Central Package Management.** Versions go in `src/Directory.Packages.props` as
   `<PackageVersion Include="..." Version="..." />`; individual `.csproj` files reference them with
   no `Version` attribute. Never pin a version inside a `.csproj`.
2. **Audit before adding or updating**:
   - `dotnet list package --vulnerable --include-transitive`
   - `dotnet list package --outdated`
3. **14-day age window.** Prefer versions released at least 14 days ago, so the community has had
   time to find and disclose problems in a fresh release.
4. **Transitive vulnerabilities are fixable.** `CentralPackageTransitivePinningEnabled` is on, so a
   vulnerable indirect dependency can be forced to a patched version by adding a `PackageVersion`
   entry for it — this is how CVE-2025-6965 in `SQLitePCLRaw.lib.e_sqlite3` was resolved.

---

## 8. Testing

Three distinct layers. Putting a test in the wrong one is a real mistake, not a style preference.

| Layer | Where | How |
|---|---|---|
| Service logic | `HandyFix.Services.Data.Tests` | Real repositories over an EF context |
| Controller behaviour | `HandyFix.Web.Tests/Controllers/` | Controller constructed directly with mocked services; assert on the returned `IActionResult`. No database. |
| Full stack | `HandyFix.Web.Tests/WebTests.cs` | `SqliteWebApplicationFactory` |

**Rules that are load-bearing:**

- **Never replace `SqliteWebApplicationFactory` with a bare `WebApplicationFactory<Program>`.**
  That boots the real app against the configured SQL Server, which means migrating and seeding the
  developer's own database on every test run, and failing entirely on a machine with no SQL Server.
- **InMemory versus Sqlite is a deliberate choice.** EF Core's InMemory provider silently ignores
  transactions *and* `RowVersion` concurrency tokens, so it cannot prove rollback or
  double-booking rejection. Tests that need those use Sqlite `:memory:`. Do not "simplify" a
  Sqlite-backed test to InMemory without first checking why it was Sqlite.
- Prefer testing behaviour that has actually broken before over chasing coverage numbers. Where a
  test guards a specific past bug, say so in a comment referencing the `PROJECT_STATE.md` section.
- Run the full suite before proposing a commit: `dotnet test src/HandyFix.sln`.

---

## 9. Documentation conventions

- **Do not use the `§` symbol.** Write `Section 3t`, or better, describe what it covers ("the
  controller-testing section"). For roadmap items write `roadmap Tier 3 item 13`. A bare symbol
  plus a number forces the reader to go and look it up mid-sentence.
- Link to existing docs rather than restating them. `PROJECT_STATE.md` Section 1 is the
  architecture document; the README points at it instead of describing the architecture again.
- Documentation must describe what is actually true today. If you find a claim that is wrong,
  correct it or flag it — do not leave it and write around it.

---

## 10. Sprints

When a sprint completes: ask for the next sprint's goals, update `PROJECT_STATE.md` to archive the
finished work and record the new goals, and commit that before starting new work.

---

## 11. Branching and deploys

Two branches, deliberately simple — this is a solo project, not a team, so a heavier branching
model (feature branches, required reviewers) would add ceremony without adding safety.

- **`dev` is the working branch.** Commit and push here for anything in progress. Every push
  triggers `.github/workflows/deploy-dev.yml`, which builds, tests, and deploys straight to
  staging — that's the feedback loop, use it liberally.
- **`main` is the verified-stable branch.** Nothing lands there except through a pull request from
  `dev`, opened once a batch of work has actually been checked on staging. `deploy-prod.yml`
  does not exist yet, so merging to `main` currently deploys nothing — but the habit needs to
  already be in place before it does, since at that point a `main` merge means "this goes live in
  production."
- **Merge `dev` into `main` often, not in one large batch.** Small, frequent merges are easy to
  review and hard to get wrong; a large merge accumulated over weeks is the opposite. A good
  trigger: after any change that's been verified working on staging and isn't obviously the start
  of more related work.
- **Agents: proactively check `git log --oneline main..dev` at natural checkpoints** (after a
  verified staging deploy, at the start of a new session, when the user asks "what's next") and
  say so if `dev` has drifted meaningfully ahead of `main` unmerged. Don't wait to be asked — this
  rule exists specifically because it's easy to forget mid-work, which is exactly when it's most
  useful to be reminded.
