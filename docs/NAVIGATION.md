# Documentation Map

Twelve markdown files. This page says which one to open. Nothing else.

---

## Start here, by who you are

| I am… | Read, in order |
|---|---|
| **new to this repo** | [README](../README.md) → [ARCHITECTURE](ARCHITECTURE.md) |
| **an AI agent, any task** | [AGENTS.md](../AGENTS.md) — it routes onward by itself |
| **briefing an agent** | [prompts/](../prompts/README.md) — fill-in-the-blank templates per task type |
| **adding a feature** | [AGENTS.md](../AGENTS.md) §3, the 17-step checklist |
| **working inside one layer** | that layer's blueprint, in the project folder — see the table below |
| **designing an endpoint** | [RESTful Naming Constitution](RESTful_Naming_Constitution.md) |
| **seeing behaviour I can't explain** | that layer's blueprint — design decisions are recorded there |

---

## Every file, what it is, when it changes

| File | What it is | Changes when |
|---|---|---|
| [README.md](../README.md) | Setup, run, secrets, project list | Setup steps or the stack change |
| [AGENTS.md](../AGENTS.md) | **The pattern.** Hard rules, the add-a-feature checklist, validation tiers, caching rule, and a routing table to everything below | A convention changes |
| [docs/ARCHITECTURE.md](ARCHITECTURE.md) | System overview for humans — layers, dependency graph, `Result<T>`, pipeline, persistence, API surface | The architecture changes |
| [docs/RESTful_Naming_Constitution.md](RESTful_Naming_Constitution.md) | Routes, verbs, status codes, required action attributes | An API convention changes |
| [docs/NAVIGATION.md](NAVIGATION.md) | This page | A document is added or moved |
| [prompts/](../prompts/README.md) | Templates for briefing an agent — what to say, what to decide first, what "done" means | A new kind of task recurs |

### Layer blueprints — deep reference, one per project

Each lives beside the code it describes, so it is impossible to miss when you open the project.

| Blueprint | Covers |
|---|---|
| [Taxi.Domain](../src/Taxi.Domain/Domain_Layer_Blueprint.md) | Entities, factories, `<Entity>Errors`, `Result<T>`, `LocalizedText` |
| [Taxi.Contracts](../src/Taxi.Contracts/Contracts_Layer_Blueprint.md) | Request DTOs, `LocalizationKeys`, `Languages` |
| [Taxi.Application](../src/Taxi.Application/Application_Layer_Blueprint.md) | Commands, queries, handlers, validators, MediatR behaviours, caching |
| [Taxi.Infrastructure](../src/Taxi.Infrastructure/Infrastructure_Layer_Blueprint.md) | `AppDbContext`, EF configuration, migrations, seeding, Identity, JWT |
| [Taxi.Api](../src/Taxi.Api/Api_Layer_Blueprint.md) | Controllers, middleware order, error translation, OpenAPI |
| [Taxi.Client](../src/Taxi.Client/Client_Layer_Blueprint.md) | Blazor WASM shell — currently a scaffold, not a working app |

---

## Which file answers which question

| Question | File |
|---|---|
| How do I run this? | [README](../README.md) |
| What files do I create for a new feature? | [AGENTS.md](../AGENTS.md) §3 |
| Where does validation go — contract, validator, or entity? | [AGENTS.md](../AGENTS.md) §4 |
| Should this query be cached? | [AGENTS.md](../AGENTS.md) §6 |
| How does a bilingual field work end to end? | [AGENTS.md](../AGENTS.md) §7 |
| Which layer may reference which? | [ARCHITECTURE](ARCHITECTURE.md) §2 |
| In what order do the MediatR behaviours run? | [Application blueprint](../src/Taxi.Application/Application_Layer_Blueprint.md) |
| In what order does the middleware run? | [Api blueprint](../src/Taxi.Api/Api_Layer_Blueprint.md) |
| How does an `Error` become an HTTP status? | [Api blueprint](../src/Taxi.Api/Api_Layer_Blueprint.md) |
| How do I map a `LocalizedText` column? | [Infrastructure blueprint](../src/Taxi.Infrastructure/Infrastructure_Layer_Blueprint.md) |
| What should this route be called? | [RESTful Naming Constitution](RESTful_Naming_Constitution.md) |
| Where do traces, metrics and logs go? | [Api blueprint](../src/Taxi.Api/Api_Layer_Blueprint.md) §10 |
| When are domain events published? | [Infrastructure blueprint](../src/Taxi.Infrastructure/Infrastructure_Layer_Blueprint.md) §4 |
| How do I add SignalR / background jobs / pagination correctly? | the relevant blueprint's **Future Extensions** section |

---

## Two conventions worth knowing

**`README.md` and `AGENTS.md` stay at the repository root on purpose.** GitHub renders the root
README as the landing page, and Claude Code, Codex and Cursor auto-discover `AGENTS.md` **only** at
the root. Moving either into `docs/` would break how they are found.

**Blueprints stay next to their project.** `src/Taxi.Domain/Domain_Layer_Blueprint.md` is visible
the moment you open the Domain folder; in a shared `docs/` folder it would not be.
