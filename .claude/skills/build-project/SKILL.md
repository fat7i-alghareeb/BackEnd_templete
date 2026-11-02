---
name: build-project
description: Build a whole product from a description, feature by feature, after one approved spec.
argument-hint: "[what the product does, in a few sentences]"
disable-model-invocation: true
---

# Build the project: $ARGUMENTS

Goal: from a description to a working API with the least token use. One interview, one spec, one
approval, then build feature by feature. Be economical: read each file at most once.

1. **Setup check.** If `CLAUDE.md` still says "Not set up yet", run the `/first-run` steps first
   (or ask me to). Read `docs/PROJECT_MAP.md` once. If `docs/PROJECT_SPEC.md` already exists,
   **resume**: read it, find the first feature whose status is not `done`, and continue from step 4.
2. **Interview.** Ask in two or three batched sets (question tool; 2–4 options; your recommendation
   first). Propose, do not interrogate: derive the entity list from my description and ask me to
   correct it. Cover:
   - who uses it → roles (the template has `Manager` and `User`; more roles = more policies);
   - the entities and their fields (types: `string`, bilingual `text`, `int`, `decimal`, `bool`;
     anything else, such as dates or enums, is hand-written — say which);
   - relationships (by id), and rules that span entities ("one driver per car", "cannot delete while
     in use");
   - which lists need search, paging, filters or sorting; what may be cached;
   - images or files; things that must happen after something else (domain events, emails);
   - anything the template does not do (real-time push, payments, maps, search engines): say so now.
3. **Write `docs/PROJECT_SPEC.md`** from the template at the bottom of this skill, then **stop for my
   approval**. Do not write code before I approve. The status column is the resume point if the chat
   is cleared.
4. **Build in dependency order**, one feature at a time (a referenced entity first):
   1. `pwsh ./tool/catalog.ps1 <word>`; reuse what exists.
   2. `pwsh ./tool/generate-feature.ps1 <Name> -Fields "..."` (add `-HardDelete` if the spec says so).
   3. Translate every `TODO-AR:` value in `SharedResource.ar.json` (grep for `TODO-AR`).
   4. Add the spec's business rules to the entity (guards before assignment) and its domain tests;
      cross-entity rules go in the command handler (copy `AssignDriverCar`); extra endpoints and
      events by copying `Driver`.
   5. `dotnet ef migrations add Add<Plural> -p src/Taxi.Infrastructure -s src/Taxi.Api`, read it.
   6. Smallest checks only: `dotnet build Taxi_Server.slnx` (0 warnings), `dotnet test
      tests/Taxi.Domain.UnitTests`, `tests/Taxi.Application.UnitTests`. No full suite yet.
   7. Set the feature's status to `done` in `docs/PROJECT_SPEC.md` and add its row to
      `docs/PROJECT_MAP.md`. One line to me: what was built, what is next. Do not stop for approval
      between features unless a decision is mine.
5. **Between features** keep the context small: if the chat is long, say "state is in
   docs/PROJECT_SPEC.md; `/clear` and run `/build-project` to resume".
6. **At the end:** run the whole test suite once (Docker needed), `/finish` once for the docs, and
   report: features built, endpoints added (point at `requests/requests.http`), what is left for me
   (secrets, deployment — `/release`). Do not commit.

## Spec template (write it to `docs/PROJECT_SPEC.md`)

```markdown
# Project spec — <product name>

<two sentences: what it is and who uses it>

## Roles
| Role | Can |
|---|---|

## Features
| # | Entity | Fields | Rules | Endpoints | Cached | Status |
|---|---|---|---|---|---|---|
| 1 | Trip | Destination:text, Fare:decimal | fare >= 0; one active trip per driver | list, get, create, update, delete | list+get | todo |

## Relationships and cross-entity rules
- ...

## Out of scope / not supported by the template
- ...
```
