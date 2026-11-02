# Using Claude Code with this template

This page explains everything in the repository that exists for Claude Code, and how to use it. The
goal is simple: **spend as few tokens as possible to start a project, and then to build it**, not
only to scaffold it.

If you read nothing else: run `/first-run` once, then `/build-project <what your product does>`.

---

## 1. The five-minute version

| You want to… | Type this | What happens |
|---|---|---|
| Set Claude up for your project | `/first-run` | Claude checks the baseline, asks you a few questions with options, renames the template if you want, and records the answers so it never asks again |
| Build a whole product | `/build-project A booking API where managers publish trips and users read them` | Claude interviews you briefly, writes `docs/PROJECT_SPEC.md`, waits for your OK, then builds feature by feature |
| Add one feature | `/new-feature Trip - a trip with a destination and a fare` | Spec, your OK, generated code, migration, tests |
| Wrap up a feature | `/finish` | Updates only the docs that changed, checks for leftovers, runs the checks |
| Add a translated message | `/add-key Trip.Full.Booked "The trip is full" "الرحلة ممتلئة"` | Key in the code and in both language files |
| Prepare a deployment | `/release compose` | Verification checklist for the Docker Compose deployment |

The skills only run when **you** type them. Claude never starts one by itself.

---

## 2. What was added, and why it saves tokens

Every chat starts empty and everything loaded is paid for on every turn. So the setup follows one
rule: **load little, load once, load on demand.**

| Piece | Where | Loaded | Saves tokens by |
|---|---|---|---|
| Small instructions | `CLAUDE.md` | every chat | being about 80 lines of habits, not a manual |
| The rules digest | `AGENTS.md` | every chat (imported by `CLAUDE.md`) | holding ~190 lines (rules and the feature checklist) instead of ~660 |
| Long-form reference | `docs/AGENT_REFERENCE.md` | on demand, one section | keeping validation, errors, caching and ordering detail out of the default context |
| Layer rules | `.claude/rules/*.md` | automatically, when Claude touches that layer | replacing the 2,700 lines of blueprints for day-to-day work |
| Skills | `.claude/skills/*/SKILL.md` | only when you type the command | one command instead of a long prompt each time |
| Settings | `.claude/settings.json` | always (not text Claude reads) | no permission prompts for build and test; blocks reading generated files |
| Output filter | `tool/claude-dotnet-filter.ps1` | on every `dotnet build` / `dotnet test` | quieter command output |
| Feature generator | `tool/generate-feature.ps1` | when run | ~25 files from one command instead of Claude typing them |
| Duplicate finder | `tool/catalog.ps1` | when run | a 10-line answer instead of a codebase sweep |
| Living docs | `docs/PROJECT_MAP.md`, `DECISIONS.md`, `CODE_STYLE.md`, `COMMANDS.md` | on demand | the next chat starts informed |
| Guard test | `tests/Taxi.ArchitectureTests/ClaudeConfigTests.cs` | in CI | stops the always-loaded files growing back |

---

## 3. First run

Open the project in Claude Code and type:

```text
/first-run
```

Claude will:

1. Check the baseline: `git status`, the .NET version, whether Docker is running, a build, and the
   domain tests. It reports in one line.
2. Ask you the project questions as a short set of options (product name, sign-up and roles,
   languages, Redis/email/file storage, deployment). The recommended option is listed first.
3. Apply your answers once you confirm. If you chose a new product name it runs `rename.ps1` (the git
   tree must be clean, so commit or stash first).
4. Write a short "This project" block into `CLAUDE.md` and save the non-obvious decisions to memory
   and to `docs/DECISIONS.md`.
5. Print a manual checklist for you (secrets, SMTP account, domain, TLS).

You run it once per project. Until `CLAUDE.md` is filled in, Claude reminds you to run it.

---

## 4. Build a whole project: `/build-project`

```text
/build-project A booking API. Managers publish trips (destination in Arabic and English,
fare, seats). Drivers are assigned to trips. Users can only read.
```

What happens, in order:

1. **Interview** (2–3 question sets with options). Claude proposes the entity list from your
   description and asks you to correct it: roles, entities and fields, relationships and cross-entity
   rules, which lists need search or caching, uploads, events. It also tells you now what the template
   does **not** do (for example real-time push or payments), so there are no surprises later.
2. **Spec.** It writes `docs/PROJECT_SPEC.md`: roles, a feature table with fields, rules, endpoints
   and a status column. **It stops and waits for your approval.** Nothing is built before that.
3. **Build**, one feature at a time, dependencies first. For each feature Claude:
   - runs `tool/catalog.ps1` to avoid duplicates;
   - runs `tool/generate-feature.ps1` to create the whole slice;
   - translates the Arabic placeholders (`TODO-AR`), adds your business rules and their tests,
     creates and reads the migration;
   - runs only the smallest checks (build, domain and application tests);
   - marks the feature `done` in the spec.
4. **Finish.** The whole test suite runs once, `/finish` updates the docs, and Claude lists what is
   left for you (secrets, deployment).

**Resuming.** The status column in `docs/PROJECT_SPEC.md` is the memory of the build. If a chat gets
long or you close it, type `/clear`, then `/build-project` again; Claude finds the first feature that
is not `done` and continues.

---

## 5. One feature: `/new-feature`

```text
/new-feature Trip - a trip with a bilingual destination, a fare and a seat count
```

Claude searches for an existing `Trip`, asks the open questions, writes
`docs/features/Trips.md`, and waits for your approval. Then it generates the code, translates the
Arabic, adds rules and tests, creates the migration, adds an integration test and runs the checks.
Use `/build-project` instead when you have several features, because the interview happens once.

---

## 6. The scripts you can run yourself

All need PowerShell 7 (`pwsh`), which the repository already requires for `rename.ps1`.

### `tool/generate-feature.ps1`: a whole feature in one command

```powershell
pwsh ./tool/generate-feature.ps1 Trip -Fields "Destination:text,Code:string,Fare:decimal,Seats:int,Active:bool"
```

| Field type | Meaning | Rule it gets |
|---|---|---|
| `string` | text, required, up to 200 characters | non-empty |
| `text` | bilingual (English and Arabic), stored as one JSON column | both languages non-empty, up to 1000 each |
| `int`, `decimal` | numbers | `>= 0` |
| `bool` | true or false | none |

It creates about 24 files in the `Car` shape (request contracts, entity and errors, DTO and mapper,
create/update/remove commands with validators and handlers, get-by-id and a paged search query with
caching, EF configuration, controller with policies and idempotent create, domain tests, sample
requests) and edits 10 existing files (localization keys, both language files, cache tag, the `DbSet`
on the interface, the context and the test context, policies and their role mapping).

Options: `-Plural Categories` (otherwise derived), `-HardDelete` (rows are really deleted; the default
is soft delete like `Car`), `-Migrate` (also runs `dotnet ef migrations add`), `-SkipFormat`,
`-DryRun` (lists what it would do). It refuses to run if the entity folder already exists.

What it does **not** do: it cannot translate (new Arabic values are written as `TODO-AR: <english>`
so they are easy to find), it does not know your business rules, and it does not write integration
tests. Afterwards: translate, migrate, add rules, build.

### `tool/catalog.ps1`: does it already exist?

```powershell
pwsh ./tool/catalog.ps1 trip
```

Prints existing type names and localization keys that contain the word. Claude runs it before
creating anything, and so can you.

### `tool/claude-dotnet-filter.ps1`: the quiet-output hook

You do not run this. `.claude/settings.json` calls it before each `dotnet build` / `dotnet test` that
Claude runs, and it appends `--nologo -v q`. It leaves a command alone if you already gave a
verbosity or if it contains `&&`, `;`, `|` or redirects.

---

## 7. The skills in detail

Each lives in `.claude/skills/<name>/SKILL.md`. You can read or change them; they are short.

| Command | Arguments | Use it when | Does not |
|---|---|---|---|
| `/first-run` | none | once, in a new project | commit |
| `/build-project` | what the product does | you have a product, not a single feature | build before you approve the spec |
| `/new-feature` | name and purpose | one feature | skip the spec approval |
| `/finish` | none | a feature is done (not for a bug fix) | commit |
| `/add-key` | key, English, Arabic | you need one message translated | leave English in the Arabic file |
| `/release` | `compose` | before deploying | deploy, tag or push |

---

## 8. The rules that load themselves

`.claude/rules/` has one short file per layer. Claude Code loads a file automatically when it works
on matching paths, so the Domain rules appear only when Claude touches `src/Taxi.Domain/`, and so on:

| File | Loads for |
|---|---|
| `domain.md` | `src/Taxi.Domain/**`, domain tests |
| `contracts.md` | `src/Taxi.Contracts/**`, the resource (language) files |
| `application.md` | `src/Taxi.Application/**`, application tests |
| `infrastructure.md` | `src/Taxi.Infrastructure/**` |
| `api.md` | `src/Taxi.Api/**`, integration tests, `requests/**` |

Each ends with a pointer to the exact blueprint sections, to be read only if the rule file is not
enough.

---

## 9. Settings and safety

`.claude/settings.json` is shared with the team. It:

- **allows** build, test, format, list, `ef migrations add`, the `tool/` scripts, markdownlint and
  read-only git commands without asking;
- **asks** before `git commit`, `git push`, `dotnet ef database update` and `rename.ps1`;
- **denies** reading `bin/`, `obj/`, generated migration designers, the model snapshot, coverage
  output and `.env`; editing generated files; `docker compose down -v`; `docker volume rm`; force
  pushes and `git reset --hard`.

Personal overrides go in `.claude/settings.local.json`, and private notes in `CLAUDE.local.md`; both
are git-ignored. If you pay for the API (not a subscription) a useful personal setting is a longer
prompt cache, described in the Claude Code documentation.

---

## 10. The documents Claude keeps up to date

| File | Holds | Updated by |
|---|---|---|
| `docs/PROJECT_MAP.md` | the features table and "where is X" | `/build-project`, `/finish` |
| `docs/DECISIONS.md` | durable decisions and why | `/first-run`, `/finish` |
| `docs/CODE_STYLE.md` | how you write code; your corrections | `/finish` when you corrected Claude |
| `docs/COMMANDS.md` | every command, script and setting | `/finish` |
| `docs/PROJECT_SPEC.md` | the product spec and build status | `/build-project` |
| `docs/features/*.md` | one spec per feature | `/new-feature` |

When you correct Claude ("no, we always do it this way"), it records the line in `CODE_STYLE.md`
so the correction survives the end of the chat.

---

## 11. Habits that save the most tokens

1. **One task per chat.** When the task is done, `/clear`.
2. **Use the commands.** `/new-feature` and `/build-project` carry the whole routine; typing it as a
   prompt costs more and drifts.
3. **Pick the model once.** Sonnet with medium effort is the default; ask for Opus only for
   architecture or a hard bug, and do not switch mid-task.
4. **Do not paste files.** Name the feature or the file; Claude reads narrowly.
5. **Ask for the smallest check.** The whole suite runs only at the end of a feature, before a
   release, or when you ask.
6. **Let Claude ask you.** Decisions come as short option lists; answer them in one go.

---

## 12. Maintaining the setup

- `CLAUDE.md` is loaded on every turn. A test (`ClaudeConfigTests`) fails if it grows past 90
  lines or `AGENTS.md` past 220, if a rule points at a folder that does not exist, or if a skill
  is malformed. Raise a limit deliberately, never by drift. Put new detail in
  `docs/AGENT_REFERENCE.md` or a layer rule, not in `CLAUDE.md`.
- Add a rule to `AGENTS.md` (or its reference) once; every tool reads it. `.cursorrules`,
  `.windsurfrules` and `.github/copilot-instructions.md` just point to `AGENTS.md`.
- A new skill is a folder `.claude/skills/<name>/SKILL.md` with `name` (the folder name) and
  `description` in its front matter.
- After `rename.ps1` everything follows the new name, including the paths inside the rules, the
  skills and the scripts (they find the project prefix themselves).

---

## 13. Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| The hook does nothing | `pwsh` is not on the PATH, or the command had `&&`, `;` or a pipe | Install PowerShell 7; run build and test as separate commands |
| `/first-run` is not offered | Skills load from `.claude/skills/` at session start | Restart Claude Code in the project folder |
| The generator says the folder exists | The entity was already generated | Pick another name, or delete `src/Taxi.Domain/<Plural>` and the files it made first |
| The generator failed halfway | A later step threw after earlier files were written | `git status`, delete the new files, `git checkout` the edited ones, fix the cause, rerun with `-DryRun` first |
| `Could not place the edit in …` | A file the generator edits was refactored | Open the script, adjust the anchor text it names |
| Arabic shows `TODO-AR:` | The placeholders were not translated | `grep -rn "TODO-AR" src/`, then translate |
| Claude reads huge files | It skipped the §0 table | Remind it: "use the AGENTS.md §0 table; read narrowly" |
| A claim in the docs looks wrong | Docs drift | Run `/finish`, or see [prompts/update-docs.md](../prompts/update-docs.md) |

See also [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for problems running the application.
