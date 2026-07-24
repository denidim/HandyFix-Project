# HandyFix — Workflow Rules

## 1. Context Initialization (Startup Rule)

Before answering any query or starting any new work, proactively read `PROJECT_STATE.md` and check `git log -n 5`. This is required to understand the architectural history, current sprint status, and exact project state.

## 2. State Management (Update Rule)

`PROJECT_STATE.md` is the single source of truth. Whenever a plan changes, a task pivots, an architectural shift happens, or a feature is completed, update `PROJECT_STATE.md` immediately to reflect reality. Do not wait to be reminded.

## 3. Sprint Transitions

When a sprint is completed, ask for the goals of the next sprint, update `PROJECT_STATE.md` to archive the finished tasks and add the new goals, and commit the changes before continuing.
