# CLAUDE.md

## Read `AGENTS.md` first, and follow it

[`AGENTS.md`](AGENTS.md) in the repository root is the single source of truth for how to work in
this project — context initialization, the public-repo secret rules, plan-before-code, commit
format, StyleCop, package security, testing layers, and documentation conventions.

**Everything in it applies to Claude Code.** Read it at the start of every session, together with
`PROJECT_STATE.md`, `git log -n 5`, and `docs/private/VISION_AND_CONTEXT.md` if present.

Rules are deliberately **not** duplicated here. This project previously had two agent rule files
that disagreed — one asked for prose commit bodies while the other forbade them — and each agent
correctly followed its own, producing inconsistent history. One real file, two signposts, nothing
to drift.

If anything below ever contradicts `AGENTS.md`, `AGENTS.md` wins and the contradiction is a bug in
this file.

---

## Claude Code specifics

Harness details, not project rules — these have no equivalent for other agents.

- **Never add a `Co-Authored-By: Claude <noreply@anthropic.com>` trailer** to any commit here. The
  default Claude Code template suggests one; this repository does not use it.
- Prefer the dedicated file tools (Read, Edit, Write, Glob, Grep) over shell equivalents (`cat`,
  `sed`, `find`, `grep`) — they integrate with the permission UI and produce clickable file links.
- Reference code as clickable markdown links, e.g.
  `[Program.cs:135](src/Web/HandyFix.Web/Program.cs#L135)`.
- Windows environment. The PowerShell tool takes PowerShell syntax; the Bash tool takes POSIX
  syntax. `&&` and `||` are not available in PowerShell 5.1 — use `;` and `if ($?) { ... }`.
