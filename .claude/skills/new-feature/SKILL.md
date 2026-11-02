---
name: new-feature
description: Build one new feature from a short spec: ask, spec, approval, generate, finish by hand.
argument-hint: "[feature name — what it does]"
disable-model-invocation: true
---

# New feature: $ARGUMENTS

1. `pwsh ./tool/catalog.ps1 <word>` for the main noun. A match means: reuse it or extend it, and tell
   me before creating a second one.
2. Read `docs/FEATURE_SPEC_TEMPLATE.md` once. Ask me the open questions (options, recommended first),
   batched: fields and types, rules that must always hold, endpoints, who reads and who writes, what
   may be cached, soft or hard delete, relationships to other entities, retries (idempotent create),
   events.
3. Write the spec to `docs/features/<Plural>.md` and **stop for my approval**.
4. `pwsh ./tool/generate-feature.ps1 <Name> -Fields "..."` (`-HardDelete` if agreed). Field types the
   generator knows: `string`, `text` (bilingual), `int`, `decimal`, `bool`. A date, enum or other
   type is added by hand after generating — copy the nearest existing property across the layers.
5. Translate every `TODO-AR:` value in `src/Taxi.Api/Resources/SharedResource.ar.json`.
6. Add the spec's rules: guards in the entity (before assignment) with a test for each; rules that
   span entities in the command handler; extra endpoints, a child collection or an event by copying
   `Driver` (`AGENTS.md` §0 names the blueprint section).
7. `dotnet ef migrations add Add<Plural> -p src/Taxi.Infrastructure -s src/Taxi.Api`; read the file.
8. Integration test: copy `tests/Taxi.Api.IntegrationTests/Features/CarsTests.cs`, cut it to the CRUD
   round trip, one validation failure and the RBAC check.
9. `dotnet build Taxi_Server.slnx` → 0 warnings; run the new tests; then the whole suite **once**
   (this is the end of a feature, so it is allowed; Docker needed). Report briefly. Do not commit.
