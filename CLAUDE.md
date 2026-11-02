# Taxi_Server — instructions for Claude Code

@AGENTS.md

The file above is the project digest: rules, the feature checklist and a "what to read for which
task" table. It is already in your context — do not open it again.

## Token discipline (every chat)

Every chat starts empty and everything loaded is paid for on every turn, so: load little, load once,
load on demand.

- **Never re-read what you know.** A file you already read in this chat is not read again unless
  `git status` / `git diff` shows it changed. A fact in this file, `AGENTS.md` or memory is not
  looked up again. Read before you act, once.
- **Read narrowly.** `grep` or a line range first; open a whole file only to edit it. Never read
  generated files (`Migrations/*.Designer.cs`, `AppDbContextModelSnapshot.cs`, `bin/`, `obj/`);
  settings block it.
- **Details load on demand.** Layer rules in `.claude/rules/` load themselves when you touch that
  layer. The blueprints (`src/*/…_Layer_Blueprint.md`) and `docs/AGENT_REFERENCE.md` are read only
  for the one section the `AGENTS.md` §0 table names, never swept.
- **Delegate noise.** Wide exploration, long logs and long test output go to a subagent; only its
  summary comes back.
- **Pick the model once, at the start.** Default: Sonnet, medium effort. Opus / high effort only for
  architecture or a hard bug. Do not switch mid-task (a switch rebuilds the cache).
- **One task per chat.** When a task is done and the next is unrelated, say so and suggest
  `/clear`. `/compact` only at a natural break.
- **Do not trade correctness for tokens.** Run the smallest check that proves the change: build,
  then the changed layer's test project. **The whole suite is not the default** — only when
  `/finish` ends a feature, before a release, after a many-file refactor, or when I ask. Use plan
  mode for changes that touch several files; skip it for a one-line fix.

## Building features (every time)

- **Generate, then edit.** For a new entity run `pwsh ./tool/catalog.ps1 <word>` (does it exist
  already?), then `pwsh ./tool/generate-feature.ps1 <Name> -Fields "..."`; do not type the ~25 files
  by hand. Then: translate the `TODO-AR` values, `dotnet ef migrations add`, add the real business
  rules and tests. `/new-feature` and `/build-project` run this whole routine.
- **Closest finished feature is the template.** `Car` (minimal) and `Driver` (child collection,
  relationship, event). `docs/CODE_STYLE.md` records how I write code; follow it and add a line
  when I correct you.
- **Duplicates are my decision.** Two near-duplicates, or about to write a second copy → ask me
  (merge / add a parameter / keep both) with options; never silently duplicate or refactor shared code.
- **When a feature is done** (not for a bug fix) run `/finish` yourself: it updates only the docs
  that changed, `CODE_STYLE.md` and memory, then reports in five lines.

## Memory

- Save to auto memory only what the repo cannot tell you: a decision and its reason, a preference, an
  external fact. One fact per file. Never code structure, paths or anything in git history.
- When you verify something on the web (a package, an API), save the answer as a `reference` memory
  with the source URL so it is never searched twice. Check memory before searching.

## How to work with me

- **Ask, with options.** When a decision is mine (design, naming, scope, a package, a trade-off),
  ask with the question tool: 2–4 options, your recommendation first, batched in one set. Do not
  guess a decision that is mine; do not ask what `AGENTS.md` already answers.
- **Verify on the web before implementing anything about a package, platform or cloud service.**
  Official docs first; cite the source. Never rely on memory for versions or APIs.
- **Do not commit** unless I ask. Never push.
- **Reports are short:** files changed, commands run with pass/fail, what needs my decision. No recap
  of the work, no restating the task.
- Reply in the language I write in.

## First time in a new project

This project came from a template, so Claude has not been set up for it yet. Run **`/first-run`**
once: it checks the baseline, asks the project questions (with options), records the answers in the
section below and in memory, and gives me a manual checklist. Until that section is filled in, remind
me to run it. To build a whole product from a description, run **`/build-project`**.

## This project (filled in by `/first-run`)

_Not set up yet — run `/first-run`._

## Compact instructions

When compacting, keep: the files changed and why, the exact build/test commands and their last
result, decisions made and their reasons, the current step of `docs/PROJECT_SPEC.md` if a build is
running, and anything I still have to decide. Drop file contents, command output and exploration.
