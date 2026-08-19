# Add a feature

A new entity with its own endpoints — the full vertical slice, contract to controller.

## Copy this

```text
Add a <ENTITY> feature.

Read AGENTS.md first — it routes you to any blueprint you need.

Fields:      <NAME: type, bilingual? required? min/max>
Rules:       <invariants that must ALWAYS hold, whoever calls>
Endpoints:   <list / by-id / create / update / delete>
Auth:        <role, or anonymous>
Cache lists: <yes / no>
Delete:      <hard delete, or a rule that blocks it>

Follow the §3 checklist in order. Car is the reference — copy its shape.
If anything I asked for conflicts with the existing pattern, stop and tell me
instead of inventing a new one.

Show me `dotnet build` and `dotnet test` output when you are done.
```

## Decide before you send

| Input | Why it changes the code |
|---|---|
| Which fields are bilingual | `LocalizedText` + `OwnsOne(...).ToJson()` (one JSONB column) versus a plain `string`. Getting this wrong means a migration to undo it. |
| Invariants | Become guards inside `Create`/`Update` **and** entries in `<Entity>Errors` **and** keys in both `SharedResource` files |
| Cached? | Forces a `CacheTags` constant *and* a `RemoveByTagAsync` call in every command that mutates the data. A cached query with no invalidator is a bug. |
| Auth | `[Authorize]` on the controller class, or `[AllowAnonymous]` per action |
| Delete semantics | `Car` has no `Delete()` — removal is `context.Cars.Remove(car)`. Add a domain method only if deletion carries a rule. |
| Child collections | A localized **child collection** hits an EF limit: `OwnsMany` + `ToJson` is unsupported. Say so up front. |

## Done means

- [ ] `dotnet build Taxi_Server.slnx -c Debug` — 0 warnings (StyleCop runs here)
- [ ] `dotnet test Taxi_Server.slnx` — green, including new guard tests
- [ ] Every new key present in **both** `SharedResource.en.json` and `SharedResource.ar.json`
- [ ] Migration generated and reviewed — check for an unintended `DropColumn`
- [ ] `requests/requests.http` has calls for the new endpoints
- [ ] Cached query, if any, has a matching `RemoveByTagAsync` in each mutating command
