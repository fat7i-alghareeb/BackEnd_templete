# Feature spec template

Copy this to `docs/features/<Plural>.md` before building a feature (`/new-feature` does it). Keep
it short: it exists so the owner can approve decisions before code is written.

```markdown
# <Entity>: <one-line purpose>

## 1. Fields
| Name | Type | Required | Bilingual | Notes |
|---|---|---|---|---|
| Destination | text | yes | yes | up to 1000 characters |
| Fare | decimal | yes | no | >= 0 |

Types the generator knows: `string`, `text`, `int`, `decimal`, `bool`. Anything else (date, enum,
money with currency) is added by hand and listed here.

## 2. Rules that must always hold
- <invariant, whoever calls, e.g. "a completed trip never changes">  → entity guard + test
- <rule needing another entity, e.g. "one active trip per driver">  → command handler (+ unique index)

## 3. Relationships
- References <Other> by id (`Guid?`), never a navigation. Delete of <Other> while referenced: blocked | allowed.

## 4. Endpoints and access
| Endpoint | Who | Notes |
|---|---|---|
| GET list | Manager, User | paged; search on: <fields>; cached |
| GET by id | Manager, User | cached |
| POST | Manager | idempotent |
| PUT / DELETE | Manager | soft | hard delete |

New policies needed? `CanRead<Plural>`, `CanManage<Plural>` (generated).

## 5. Caching
- Cached reads: list, by id (tag `<Plural>`, 10 minutes). Every write evicts the tag.
- Not cached: <anything returning roles, claims or streams>.

## 6. Events and side effects
- None | raises `<Entity><Verb>DomainEvent` when ...; handler does ... (runs later, at least once)

## 7. Stored data
- Soft delete: yes | no. Concurrency token (`xmin`): yes. Unique indexes: ...
- Existing data to migrate: ...

## 8. Tests
- Domain: every guard · Application: handler rules · Integration: CRUD round trip, one validation
  failure, RBAC (User cannot write), the cross-entity rule.

## 9. Out of scope
- ...
```
