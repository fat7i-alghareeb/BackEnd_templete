# Decisions

Durable decisions and why they were made. Check here before changing something established. Add a
row when a decision is made that the code alone would not explain; `/finish` does this.

For things that look like bugs but are deliberate, see [AGENT_REFERENCE §11](AGENT_REFERENCE.md);
for known gaps, §12.

| Decision | Why | Where |
|---|---|---|
| `Taxi.Domain` references `Taxi.Contracts` | Error codes are `LocalizationKeys`; moving them would ship business rules to clients, duplicating them drifts | Domain blueprint §2 |
| Failures are `Result<T>` values, not exceptions | Business failures are expected; one place turns them into HTTP | ARCHITECTURE §4 |
| No repositories or Unit of Work | `DbSet` is the repository, `SaveChangesAsync` the unit of work | ARCHITECTURE §9 |
| Vertical slices per use case | A change touches one folder; copyable shape | Application blueprint §4 |
| Bilingual data is `LocalizedText` in one JSONB column | Two languages are a modelling concern, not a column pair; flat columns only inside owned collections (EF limit) | Infrastructure blueprint §6 |
| Domain events go through a transactional outbox | Events survive crashes and never fail the request; handlers run later, at least once | Infrastructure blueprint §8 |
| `xmin` optimistic concurrency on `Car` and `Driver` | Detects lost updates without an extra column | Infrastructure blueprint §7 |
| `Car` is soft-deleted, `Driver` is not | `Car` shows the pattern; add `ISoftDeletable` where history matters | Infrastructure blueprint §7 |
| HybridCache with optional Redis L2 | One cache API for one or many instances; no backplane, so ≤ 30 s staleness across nodes | Application blueprint §9 |
| One live refresh token per user, rotated on use | Simple revocation; sign-in elsewhere replaces it | Infrastructure blueprint §10 |
| Account endpoints answer 202 and login answers one 401 | No account enumeration | Api blueprint §6 |
| Cars v1 is deprecated with a `Sunset` header, v2 exists | Worked example of versioning and retirement | Api blueprint §11 |
| `[Idempotent]` creates with an `Idempotency-Key` header | Retried POSTs must not create twice | Api blueprint §13 |
| Authorization by named policy, never by role | Granting a capability to a role is a one-line change | Api blueprint §7 |
| Mapping to DTOs in memory, manual extension methods | No mapper library; no SQL-side JSONB projection yet | Application blueprint §10 |
| Password policy is relaxed (6 characters) | Template default; tighten for production | Infrastructure blueprint §10 |
| `this.` on instance members is a convention only | The StyleCop rule (SA1101) is off; review enforces it | AGENTS.md §2 |
| `AGENTS.md` is a short digest; detail in `docs/AGENT_REFERENCE.md` | `AGENTS.md` is loaded every chat; the rest is read on demand | USING_CLAUDE §2 |
| Layer rules load by path (`.claude/rules/`) | Day-to-day work needs 30 lines, not a 600-line blueprint | USING_CLAUDE §8 |
| Features are generated, then edited (`tool/generate-feature.ps1`) | Typing ~25 near-identical files costs more than reviewing them | USING_CLAUDE §6 |
| `dotnet build` / `test` output is quietened by a hook | Fewer tokens per command | USING_CLAUDE §9 |
| Whole-suite test runs only at the end of a feature | Smallest check first keeps feedback fast and cheap | CLAUDE.md |
| Claude never commits or pushes unless asked | The owner decides what enters history | CLAUDE.md |
