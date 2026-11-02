# Add a feature

A new entity with its own endpoints: the full vertical slice, contract to controller.

## Copy this

```text
Add a <ENTITY> feature.

Read AGENTS.md first; it routes you to any blueprint you need.

Fields:        <NAME: type, bilingual? required? min/max>
Rules:         <invariants that must ALWAYS hold, whoever calls>
Relationships: <none, or: references <OTHER> by id; rule that spans both>
Endpoints:     <list (paged? filter? sort?) / by-id / create / update / delete>
Auth:          <who reads, who writes: Manager / User / anonymous>
Cache reads:   <yes / no>
Delete:        <soft delete, hard delete, or a rule that blocks it>
Retries:       <is a repeated create harmful? then make it idempotent>
Events:        <none, or: raise <Entity><Verb>DomainEvent when ...>

Follow the §3 checklist in order. Car is the reference (Driver for a child collection,
a relationship or an event); copy its shape.
If anything I asked for conflicts with the existing pattern, stop and tell me
instead of inventing a new one.

Show me `dotnet build` and `dotnet test` output when you are done.
```

A filled-in example, to show the level of detail that works:

```text
Add a Trip feature.
Read AGENTS.md first; it routes you to any blueprint you need.

Fields:        PickupAddress (bilingual, required, max 200), Fare (decimal, > 0), Status (enum)
Rules:         a completed trip can never change; fare is set once
Relationships: references Driver by id; a driver can have one active trip
Endpoints:     list (paged, filter by status, newest first), by-id, create, complete
Auth:          Manager writes, User reads
Cache reads:   yes
Delete:        no delete endpoint
Retries:       create must be idempotent
Events:        raise TripCompletedDomainEvent when a trip completes
...
```

## Decide before you send

| Input | Why it changes the code |
|---|---|
| Which fields are bilingual | `LocalizedText` + `OwnsOne(...).ToJson()` (one JSONB column) versus a plain `string`. Getting this wrong means a migration to undo it. |
| Invariants | Become guards inside `Create`/`Update` **and** entries in `<Entity>Errors` **and** keys in both `SharedResource` files |
| Rules that span entities | The entity cannot query. They go in the handler, backed by a database constraint where a race is possible (see `AssignDriverCarCommandHandler`) |
| Cached reads? | Forces a `CacheTags` constant *and* a `RemoveByTagAsync` call in every command that mutates the data. A cached query with no invalidator is a bug. |
| Auth | Which **policy** reads and which writes: add constants to `Policies` and map them to roles in `AddAppAuthorization`; `[AllowAnonymous]` only when justified |
| Delete semantics | `Car` has no `Delete()`: removal is `context.Cars.Remove(car)`, soft-deleted by `SoftDeleteInterceptor` because `Car` implements `ISoftDeletable`. `Driver` is hard-deleted. Soft or hard for the new entity? Add a domain method only if deletion carries a rule. |
| Child collections | A localized **child collection** hits an EF limit: `OwnsMany` + `ToJson` is unsupported (flat `...En` / `...Ar` columns instead). Say so up front. |
| Paged list | Copy `GetCarsRequest` / `GetCarsQuery`: a clamped page size, a sort whitelist, `PaginatedList<T>` |
| Retried creates | `[Idempotent]` on the `POST` (clients opt in with an `Idempotency-Key` header) |
| Events | Raised inside the entity after the guards pass; handled after commit, in the background, at least once, so the handler must be idempotent |
| API version | New features start at v1. Say so if the entity must exist in two versions |

## Done means

- [ ] `dotnet build Taxi_Server.slnx -c Debug`: 0 warnings (StyleCop runs here)
- [ ] `dotnet test Taxi_Server.slnx`: green, including new domain, handler and integration tests
- [ ] Every new key present in **both** `SharedResource.en.json` and `SharedResource.ar.json`
- [ ] Migration generated and reviewed: check for an unintended `DropColumn`
- [ ] `DbSet` added to **both** `IAppDbContext` and `AppDbContext`
- [ ] Every action has `[EndpointName]`, `[ProducesResponseType]`, `[MapToApiVersion]` and a named policy
- [ ] `requests/requests.http` has calls for the new endpoints
- [ ] Cached query, if any, has a matching `RemoveByTagAsync` in each mutating command
- [ ] A creating `POST` is `[Idempotent]`; a secret-carrying request is `ISensitiveRequest`
- [ ] The affected blueprint's feature list (and `README.md`'s API table) mentions the new endpoints
