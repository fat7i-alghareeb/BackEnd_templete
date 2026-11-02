# Code style

How this codebase is written, so new code looks like old code. When in doubt, copy the closest
finished feature (`Car`, `Driver`). A line marked **corrected** records a correction from the owner:
follow it over the general rule.

## Structure

- One folder per use case; one type per file; `<UseCase>Command|Query`, `...Handler`, `...Validator`.
- Commands, queries and DTOs are positional `record`s; entities, handlers, validators and controllers
  are `sealed` where possible (handlers are not required to be).
- File-scoped namespaces. Application handlers put `using` after the namespace; validators, requests
  and controllers put them first. Match the neighbouring file, then keep the order alphabetical
  (StyleCop SA1210 fails the build otherwise).
- No AutoMapper: mappers are static extension classes with `ToDto`.

## Members

- Prefix instance members with `this.`; copy each primary-constructor parameter to a `private
  readonly` field with the same name (`private readonly IAppDbContext context = context;`).
- Entities: private constructors, `private set`, `Create` / `Update` returning `Result<T>`, guards
  before assignment, normalisation (`Trim`) inside the entity.
- Errors are `public static readonly Error` fields in `<Entity>Errors`; a message with an argument is
  a method.
- Controllers: three statements per action; `result.Match(this.Ok, this.Problem)`.

## Naming

- Plural folders and routes (`Cars`, `/cars`), singular types (`Car`). Kebab-case multi-word routes.
- Localization keys: constant `MakeRequired`, value `"Car.Make.Required"`; request-shape messages in
  `Validation` and prefixed with the entity for a new feature (`Validation.TripFare.Invalid`).
- Tests: `Method_Outcome_WhenCondition` (`Create_Fails_WhenIdIsEmpty`), xUnit `Assert`, no mocking
  library in domain tests; handler tests use the EF InMemory `TestAppDbContext` and NSubstitute.
- UTC timestamps end in `Utc`.

## Comments

- XML doc comments only where they explain *why* (a trap, a constraint, a decision). No comment that
  restates the code.
- English fallback text in `Error.Description`; real user text lives in the resource files.

## Never

- `throw` for a business rule; `NameEn` / `NameAr` columns on an entity; a literal `"en"` / `"ar"`;
  a literal cache tag or policy name; `[Authorize(Roles = ...)]`; an entity returned from a handler.

## Corrections

None yet. `/finish` adds a line here, marked **corrected**, whenever the owner corrects Claude,
with the owner's words and the reason.
