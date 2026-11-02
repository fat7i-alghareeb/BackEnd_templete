# Modify a feature

Add a field, change a rule, alter an endpoint on something that already works.

The risk here is not writing the code: it is everything downstream that quietly still assumes the
old shape: a migration, a cache tag, a queued domain event, an existing test, a client already
calling the endpoint.

## Copy this

```text
Change the <ENTITY> feature.

Read AGENTS.md first.

What changes:  <the field / rule / endpoint, and what it becomes>
Why:           <the business reason; it decides where the rule belongs>
Breaking:      <may existing API clients break? yes / no / don't care>
Existing data: <how should rows that already exist be treated?>

Update everything that follows from it: domain guards, validator, request
contract, DTO, mapper, EF configuration, migration, tests, requests.http.

If an existing test encodes the OLD rule, tell me before changing it; I need to
know whether the rule changed or the test was wrong.

If this conflicts with the existing pattern, stop and ask instead of inventing
a new one.

Show me `dotnet build` and `dotnet test` output.
```

## Decide before you send

| Input | Why it changes the code |
|---|---|
| Does the stored shape change? | Any new/renamed/retyped persisted field needs a migration. Review it: an owned JSONB type can generate a surprise `DropColumn`. A new required column needs a default or a backfill for existing rows. |
| Is the data cached? | Changing what a cached query returns without bumping or evicting its tag serves stale data until expiry (10 minutes). Check the query's `CacheKey` too. |
| Where does the rule belong? | A shape rule (length, required, format) goes in the validator. An always-true rule goes in the domain guard. Most rules want **both**: that overlap is deliberate. |
| Breaking the wire contract? | Renaming or removing a request/response property breaks existing clients. The API is versioned: say whether you want a new version (see `CarsController` v1 → v2) or an in-place change. |
| Does a domain event change? | Events are serialized into the `OutboxMessages` table and replayed later. Renaming the event type or a property can make queued, unprocessed messages fail to deserialize. Say whether the outbox may hold old messages. |
| New user-facing message? | Needs a `LocalizationKeys` constant plus entries in **both** `SharedResource` files |
| New field on a request that carries a secret? | The request must stay `ISensitiveRequest` |

## Done means

- [ ] `dotnet build`: 0 warnings
- [ ] `dotnet test`: green, and any test that encoded the old rule was flagged to you first
- [ ] Migration generated and reviewed, if the stored shape moved
- [ ] Cache tag evicted or query key changed, if cached data is affected
- [ ] New keys in **both** `SharedResource` files
- [ ] `requests/requests.http` reflects the new shape
- [ ] The blueprint or `README.md` table that described the old shape is updated
