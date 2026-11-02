---
paths:
  - "src/Taxi.Application/**"
  - "tests/Taxi.Application.UnitTests/**"
---

# Application rules (loaded when you touch `src/Taxi.Application/`)

Copy `Features/Cars/` (minimal), `Features/Drivers/` (cross-aggregate rule, event),
`Features/Account/` (anonymous flow + email).

- One folder per use case: `Features/<Plural>/Commands|Queries/<UseCase>/`, one type per file:
  `<UseCase>Command|Query`, `...Handler`, `...Validator` (sealed). A validator is optional when a route
  constraint guarantees the input.
- Every handler returns `Result<T>`. Nothing to return: `Result<Updated>`, `Result<Deleted>`,
  `Result<Success>`. Anything that writes is a `Command`, even a sign-in that rotates a token.
- Mutating handler order: load or construct → `if (r.IsError) return r.Errors;` → mutate →
  `SaveChangesAsync` → `RemoveByTagAsync(CacheTags.X, ct)` → map to a DTO.
- Queries: `.AsNoTracking()`, map in memory with `ToDto(languageContext.Language)`, lists through
  `PaginatedList<T>.CreateAsync(...).Map(...)`. Never return an entity.
- A cached query implements `ICachedQuery<Result<T>>` (`CacheKey`, `Tags => [CacheTags.X]`,
  `Expiration`). Every command that mutates that data evicts the same tag; cache keys are
  language-suffixed unless `IsCultureAware => false`. Never cache a user's roles or claims.
- Validators carry the localization key: `.WithMessage(LocalizationKeys.Validation.X)`.
- A request carrying a password, token or stream implements `ISensitiveRequest`.
- Rules that span aggregates (uniqueness, "car exists", "cannot delete while assigned") live in the
  handler. Use `IAppDbContext`, `HybridCache`, `IUser`, `ILanguageContext` — never `HttpContext`,
  Infrastructure or Api types. Never use a `*Request` DTO here.
- Use-case failures that no entity owns go in `Common/Errors/ApplicationErrors.cs`.
- `this.` on members; primary-constructor parameters copied to `private readonly` fields.
- Event handlers are `INotificationHandler<T>` in `Features/<Plural>/EventHandlers/`; they run later,
  at least once, with no `HttpContext` — keep them idempotent.
- Adding a `DbSet` means `IAppDbContext` **and** `tests/.../TestDoubles/TestAppDbContext.cs`.

Details only if needed: `src/Taxi.Application/Application_Layer_Blueprint.md` §6–§9, §12.
