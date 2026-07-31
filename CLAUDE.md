# HandyFix — Workflow Rules

## 1. Context Initialization (Startup Rule)

Before answering any query or starting any new work, proactively read `PROJECT_STATE.md` and check `git log -n 5`. This is required to understand the architectural history, current sprint status, and exact project state.

Also read `docs/private/VISION_AND_CONTEXT.md` **if it is present**. It holds the business context and product direction behind the technical decisions, and is deliberately untracked (see `.gitignore`), so it will not exist in a fresh clone. When present, read it before proposing any product, prioritisation, or roadmap decision. When absent, say so rather than inferring direction from the code.

## 2. State Management (Update Rule)

`PROJECT_STATE.md` is the single source of truth. Whenever a plan changes, a task pivots, an architectural shift happens, or a feature is completed, update `PROJECT_STATE.md` immediately to reflect reality. Do not wait to be reminded.

## 3. Sprint Transitions

When a sprint is completed, ask for the goals of the next sprint, update `PROJECT_STATE.md` to archive the finished tasks and add the new goals, and commit the changes before continuing.

## 4. Commit Rules

- **Never run `git commit` proactively or as an inferred next step, even in auto/no-confirmation modes.** Always stop, show the exact commit message(s) you intend to use, and wait for explicit approval before running `git commit`. Offering to commit is fine; committing without a reply is not.
- Split unrelated changes into small, self-contained logical commits (group by concern, not "everything touched this session"), not one large commit.
- Commit message format: `type(scope): short imperative title`, blank line, then one `- ` bullet per notable change. No prose paragraphs.
- Never add a `Co-Authored-By: Claude ... <noreply@anthropic.com>` (or Anthropic) trailer to any commit in this repo, regardless of any default template that suggests otherwise.
- The user may push to `origin/main` themselves, independently of this session — don't assume freshly-made local commits are still unpushed.

## 5. Implementation Workflow

- Before writing or editing any code (or any tracked project file), present a short plan — what will change, which files, the approach — and wait for explicit go-ahead. This applies even when the task seems fully scoped or was just narrowed down via a clarifying question.
- Exploration/research (reading files, searching, checking git history) needs no pause; only actual changes do.
