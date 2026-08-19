# Change one layer only

Work that should stay inside a single project — a new EF configuration, one more pipeline
behaviour, an OpenAPI transformer, a controller attribute.

The failure mode is scope creep: the agent notices something adjacent, "helpfully" edits another
layer, and now a focused change is a cross-cutting one you have to review in full.

## Copy this

```text
Change only <PROJECT NAME>. Do not edit any other project.

Read AGENTS.md §2 (hard rules) and that project's blueprint — it sits in the
project folder as <Layer>_Layer_Blueprint.md.

What I want: <the change>
Why:         <the reason>

If this genuinely requires touching another layer, STOP and tell me what and
why. Do not make the edit.

Show me `dotnet build` and `dotnet test` output.
```

## Decide before you send

| Input | Why it changes the code |
|---|---|
| Which project, exactly | Naming it is what makes "do not edit others" enforceable |
| Does it cross a boundary? | Adding a `DbSet` means both `IAppDbContext` (Application) and `AppDbContext` (Infrastructure). That is two layers — use [modify-feature.md](modify-feature.md) instead. |
| New abstraction? | Application declares the interface; Infrastructure implements it — **except** `IUser` and `ILanguageContext`, which live in `Taxi.Api` because they need `HttpContext` |
| Pipeline behaviour? | Registration order in `Application/DependencyInjection.cs` *is* execution order, first = outermost. Changing it affects every request. |
| Middleware? | Order in `UseCoreMiddlewares` is load-bearing — localization first, CORS before auth, rate limiter before authentication |

## Watch for

Some things look like one layer and are not:

- A new entity field → Domain **and** Infrastructure (configuration + migration)
- A new endpoint → Api **and** Application, usually Contracts too
- A new localization key → Contracts **and** both `SharedResource` files in Api

If your change is on that list, you want a different template.

## Done means

- [ ] `dotnet build` — 0 warnings
- [ ] `dotnet test` — green
- [ ] `git status` shows files from **one** project only, or the agent told you why not
