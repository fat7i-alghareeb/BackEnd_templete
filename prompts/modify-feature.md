# Modify a feature

Add a field, change a rule, alter an endpoint on something that already works.

The risk here is not writing the code — it is everything downstream that quietly still assumes the
old shape: a migration, a cache tag, an existing test, a client already calling the endpoint.

## Copy this

```text
Change the <ENTITY> feature.

Read AGENTS.md first.

What changes:  <the field / rule / endpoint, and what it becomes>
Why:           <the business reason — it decides where the rule belongs>
Breaking:      <may existing API clients break? yes / no / don't care>

Update everything that follows from it: domain guards, validator, DTO, mapper,
EF configuration, migration, tests, and requests.http.

If an existing test encodes the OLD rule, tell me before changing it — I need to
know whether the rule changed or the test was wrong.

If this conflicts with the existing pattern, stop and ask instead of inventing
a new one.

Show me `dotnet build` and `dotnet test` output.
```

## Decide before you send

| Input | Why it changes the code |
|---|---|
| Does the stored shape change? | Any new/renamed/retyped persisted field needs a migration. Review it — an owned JSONB type can generate a surprise `DropColumn`. |
| Is the data cached? | Changing what a cached query returns without bumping or evicting its tag serves stale data until expiry |
| Where does the rule belong? | A shape rule (length, required, format) goes in the validator. An always-true rule goes in the domain guard. Most rules want **both** — that overlap is deliberate. |
| Breaking the wire contract? | Renaming or removing a request/response property breaks existing clients. The API is versioned — say whether you want a new version or an in-place change. |
| New user-facing message? | Needs a `LocalizationKeys` constant plus entries in **both** `SharedResource` files |

## Done means

- [ ] `dotnet build` — 0 warnings
- [ ] `dotnet test` — green, and any test that encoded the old rule was flagged to you first
- [ ] Migration generated and reviewed, if the stored shape moved
- [ ] Cache tag evicted or query key changed, if cached data is affected
- [ ] New keys in **both** `SharedResource` files
- [ ] `requests/requests.http` reflects the new shape
