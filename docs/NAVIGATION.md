# Documentation Map

Every markdown file in the repository, and which one to open. Nothing else.

---

## Start here, by who you are

| I am… | Read, in order |
|---|---|
| **new to this repo** | [README](../README.md) → [ARCHITECTURE](ARCHITECTURE.md) |
| **an AI agent, any task** | [AGENTS.md](../AGENTS.md): its §0 table routes onward by itself |
| **using Claude Code efficiently** | [USING_CLAUDE](USING_CLAUDE.md): the commands, scripts and token habits |
| **starting a product from scratch** | [USING_CLAUDE §4](USING_CLAUDE.md): `/first-run`, then `/build-project` |
| **briefing an agent** | [prompts/](../prompts/README.md): fill-in-the-blank templates per task type |
| **adding a feature** | [AGENTS.md](../AGENTS.md) §3, the 17-step checklist |
| **working inside one layer** | that layer's blueprint, in the project folder; see the table below |
| **designing an endpoint** | [RESTful Naming Constitution](RESTful_Naming_Constitution.md), then [Api blueprint §6](../src/Taxi.Api/Api_Layer_Blueprint.md) |
| **running or deploying it** | [README](../README.md): Quick start, Configuration, Deployment |
| **hitting an error on first run** | [TROUBLESHOOTING](TROUBLESHOOTING.md) |
| **contributing** | [CONTRIBUTING](../CONTRIBUTING.md) |
| **reporting a vulnerability** | [SECURITY](../SECURITY.md) |
| **seeing behaviour I can't explain** | [AGENT_REFERENCE.md](AGENT_REFERENCE.md) §11 and §12, then that layer's blueprint ("Deliberate decisions") |
| **changing a document** | [prompts/update-docs.md](../prompts/update-docs.md) |

---

## Every file, what it is, when it changes

| File | What it is | Changes when |
|---|---|---|
| [README.md](../README.md) | Front door: what it is, quick start, configuration, API surface, deployment, CI | Setup steps, configuration keys, endpoints or the stack change |
| [AGENTS.md](../AGENTS.md) | **The pattern.** Hard rules, the add-a-feature checklist, validation tiers, errors, caching, pipeline, verification, deliberate decisions, known gaps, and a routing table to everything below | A convention changes |
| [docs/ARCHITECTURE.md](ARCHITECTURE.md) | System overview: layers, dependency graph, `Result<T>`, pipeline, persistence, API surface, security, topology, CI | The architecture changes |
| [docs/RESTful_Naming_Constitution.md](RESTful_Naming_Constitution.md) | Routes, verbs, status codes, headers, required action attributes, error shapes | An API convention changes |
| [docs/NAVIGATION.md](NAVIGATION.md) | This page | A document is added, moved or renamed |
| [CLAUDE.md](../CLAUDE.md) | What Claude Code reads every chat: token habits, how to work with the owner; imports `AGENTS.md` | A habit changes (stays under 90 lines) |
| [docs/AGENT_REFERENCE.md](AGENT_REFERENCE.md) | Long-form detail behind `AGENTS.md`: validation tiers, errors, caching, bilingual data, templates, pipeline order, deliberate decisions, known gaps | A convention's detail changes |
| [docs/USING_CLAUDE.md](USING_CLAUDE.md) | How to use every Claude Code piece in this repo: skills, scripts, rules, settings, habits | A skill, script or setting changes |
| [docs/PROJECT_MAP.md](PROJECT_MAP.md) | Where things are; the features table | A feature or folder is added |
| [docs/COMMANDS.md](COMMANDS.md) | Every command, script and the manual smoke test | A command or script changes |
| [docs/DECISIONS.md](DECISIONS.md) | Durable decisions and why | A decision is made |
| [docs/CODE_STYLE.md](CODE_STYLE.md) | How code is written here; owner corrections | The owner corrects a habit |
| [docs/FEATURE_SPEC_TEMPLATE.md](FEATURE_SPEC_TEMPLATE.md) | Template for a feature spec (`docs/features/*.md`) | The spec shape changes |
| [docs/TROUBLESHOOTING.md](TROUBLESHOOTING.md) | Symptom, cause, fix: startup, Docker, auth, requests, email, tests, renaming | A new failure mode is found |
| [CONTRIBUTING.md](../CONTRIBUTING.md) | Human workflow: set up, branch, check, commit style, pull request | The workflow or checks change |
| [SECURITY.md](../SECURITY.md) | How to report a vulnerability; what is intentional; pre-deploy list | The security posture or contact route changes |
| [docs/SHOWCASE.md](SHOWCASE.md) | Visual tour: diagrams of every major feature; images in `docs/assets/` | A feature shown in it changes |
| [prompts/](../prompts/README.md) | Templates for briefing an agent: what to say, what to decide first, what "done" means | A new kind of task recurs |
| [.github/pull_request_template.md](../.github/pull_request_template.md) | The checklist every pull request starts with | A review rule changes |

### Layer blueprints: deep reference, one per project

Each lives beside the code it describes, so it is impossible to miss when you open the project.
All five share one shape: At a glance · Purpose · Dependency rules · Directory structure · the
layer's patterns · Belongs / does not belong · Naming · Talking to other layers · Enforced by
tests · Deliberate decisions · Common mistakes · Future Extensions (**not implemented**).

| Blueprint | Covers |
|---|---|
| [Taxi.Domain](../src/Taxi.Domain/Domain_Layer_Blueprint.md) | Entities and factories, `Driver` (child collection), `<Entity>Errors`, `Result<T>`, `LocalizedText`, domain events |
| [Taxi.Contracts](../src/Taxi.Contracts/Contracts_Layer_Blueprint.md) | Request DTOs, `LocalizationKeys`, `Languages`, paging / filtering / sorting |
| [Taxi.Application](../src/Taxi.Application/Application_Layer_Blueprint.md) | Commands, queries, handlers, validators, MediatR behaviours, caching, interfaces, the feature catalogue |
| [Taxi.Infrastructure](../src/Taxi.Infrastructure/Infrastructure_Layer_Blueprint.md) | `AppDbContext`, EF configuration, migrations, interceptors, outbox, seeding, Identity, JWT, Redis, files, email, health checks |
| [Taxi.Api](../src/Taxi.Api/Api_Layer_Blueprint.md) | Controllers, endpoint catalogue, middleware order, error translation, OpenAPI and versioning, uploads, idempotency, rate limiting, configuration, observability |

---

## Which file answers which question

| Question | File |
|---|---|
| How do I run this? | [README](../README.md) Quick start |
| Which configuration key does X? | [README](../README.md) Configuration |
| What files do I create for a new feature? | [AGENTS.md](../AGENTS.md) §3 |
| Where does validation go: contract, validator, or entity? | [AGENT_REFERENCE.md](AGENT_REFERENCE.md) §4 |
| Should this query be cached? | [AGENT_REFERENCE.md](AGENT_REFERENCE.md) §6 |
| How does a bilingual field work end to end? | [AGENT_REFERENCE.md](AGENT_REFERENCE.md) §7 |
| Which layer may reference which? | [ARCHITECTURE](ARCHITECTURE.md) §2 |
| What is the full endpoint list? | [Api blueprint §6](../src/Taxi.Api/Api_Layer_Blueprint.md) |
| In what order do the MediatR behaviours run? | [Application blueprint §8](../src/Taxi.Application/Application_Layer_Blueprint.md) |
| In what order does the middleware run? | [Api blueprint §9](../src/Taxi.Api/Api_Layer_Blueprint.md) |
| How does an `Error` become an HTTP status? | [Api blueprint §8](../src/Taxi.Api/Api_Layer_Blueprint.md) |
| How do I map a `LocalizedText` column? | [Infrastructure blueprint §6](../src/Taxi.Infrastructure/Infrastructure_Layer_Blueprint.md) |
| What should this route be called? | [RESTful Naming Constitution](RESTful_Naming_Constitution.md) |
| Where do traces, metrics and logs go? | [Api blueprint §16](../src/Taxi.Api/Api_Layer_Blueprint.md) |
| When are domain events published? | [Infrastructure blueprint §8](../src/Taxi.Infrastructure/Infrastructure_Layer_Blueprint.md) |
| How do accounts, tokens and refresh work? | [Infrastructure blueprint §10](../src/Taxi.Infrastructure/Infrastructure_Layer_Blueprint.md) and [Application blueprint §13](../src/Taxi.Application/Application_Layer_Blueprint.md) |
| How does versioning (v1 → v2) work? | [Api blueprint §11](../src/Taxi.Api/Api_Layer_Blueprint.md) |
| How do I add pagination, a file upload, an idempotent POST? | [Contracts §7](../src/Taxi.Contracts/Contracts_Layer_Blueprint.md), [Api §12](../src/Taxi.Api/Api_Layer_Blueprint.md), [Api §13](../src/Taxi.Api/Api_Layer_Blueprint.md) |
| Is this odd behaviour a bug? | [AGENT_REFERENCE.md](AGENT_REFERENCE.md) §11 / §12 |
| How do I add SignalR, background jobs, Dapper, a value object? | the relevant blueprint's **Future Extensions** section |
| How do I use the Claude commands and scripts? | [USING_CLAUDE](USING_CLAUDE.md) |
| Which command runs X? | [COMMANDS](COMMANDS.md) |
| Where is X? | [PROJECT_MAP](PROJECT_MAP.md) |
| Why was X decided? | [DECISIONS](DECISIONS.md) |
| Something failed on first run: what now? | [TROUBLESHOOTING](TROUBLESHOOTING.md) |
| How do I contribute, and how are commits named? | [CONTRIBUTING](../CONTRIBUTING.md) |
| How do I report a vulnerability? | [SECURITY](../SECURITY.md) |
| How do I rename the template? | [README](../README.md) "Make it yours" |

---

## Documentation that is not markdown

| File | What it is |
|---|---|
| `requests/requests.http` | **The** API samples: every flow, success and failure paths. Add a call for every new endpoint |
| `.env.example` | Variables the production compose file reads |
| `docker-compose.yml`, `docker-compose.override.yml`, `docker-compose.prod.yml` | Base, development and production topologies |
| `src/Taxi.Api/appsettings*.json` | Every configuration key with its default |
| `.markdownlint-cli2.jsonc` | Markdown lint rules (prose wraps at 100 columns; tables, code and headings exempt) |
| `docs/assets/*.svg` | Images used by the README and SHOWCASE |

`src/Taxi.Api/Taxi.Api.http` and `Identity.http` are older, partly stale samples; use
`requests/requests.http`.

---

## Conventions worth knowing

**`README.md` and `AGENTS.md` stay at the repository root on purpose.** GitHub renders the root
README as the landing page, and Claude Code, Codex and Cursor auto-discover `AGENTS.md` **only** at
the root. Moving either into `docs/` would break how they are found.

**Blueprints stay next to their project.** `src/Taxi.Domain/Domain_Layer_Blueprint.md` is visible
the moment you open the Domain folder; in a shared `docs/` folder it would not be.

**Section numbers are cross-references.** Documents point at each other as "Infrastructure §8" or
"AGENT_REFERENCE.md §6". If you renumber a section, search the repository for the old reference
(`grep -rn "§"`) and fix every one.

**One owner per fact.** State a fact in one place and link to it from the rest:

| Fact | Owner |
|---|---|
| Rules and the feature checklist | AGENTS.md |
| Setup, configuration keys, deployment | README.md |
| How a layer is built, and why | that layer's blueprint |
| Routes, verbs, status codes, headers | RESTful Naming Constitution + Api blueprint §6 |
| System-level picture | ARCHITECTURE.md |

**Planned-but-unbuilt ideas** appear only under a blueprint's "Future Extensions — NOT IMPLEMENTED"
heading, never in a main section.
