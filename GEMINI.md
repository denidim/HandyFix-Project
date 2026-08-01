# GEMINI.md

## Read `AGENTS.md` first, and follow it

[`AGENTS.md`](AGENTS.md) in the repository root is the single source of truth for how to work in
this project — context initialization, the public-repo secret rules, plan-before-code, commit
format, StyleCop, package security, testing layers, and documentation conventions.

**Everything in it applies here.** Read it at the start of every session, together with
`PROJECT_STATE.md`, `git log -n 5`, and `docs/private/VISION_AND_CONTEXT.md` if present.

---

## Why this file is only a pointer

In Antigravity, a project-root `GEMINI.md` **overrides** `AGENTS.md` where the two disagree. This
file therefore deliberately contains no rules of its own: if it did, it would silently outrank the
shared workflow and the two agents on this project would drift apart again.

That is not hypothetical. This repository previously had `CLAUDE.md` and an untracked
`.agents/AGENTS.md` giving opposite instructions — one required prose commit bodies, the other
forbade them. Each agent followed its own file correctly, and the git history ended up
inconsistent. The fix was one real rules file plus signposts.

**Do not add project rules here.** Put them in `AGENTS.md`, where every agent reads them. If
something in this file ever contradicts `AGENTS.md`, `AGENTS.md` wins and this file needs fixing.

---

## Note on global rules

Antigravity and the Gemini CLI both read `~/.gemini/GEMINI.md` for global, machine-wide rules.
Anything written there applies to **every** project on this machine and can override this
repository's workflow without being visible in the repo. Keep HandyFix-specific rules in
`AGENTS.md`, not in the global file.
