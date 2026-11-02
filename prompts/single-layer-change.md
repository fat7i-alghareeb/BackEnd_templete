# Change one layer only

Work that should stay inside a single project: a new EF configuration, one more pipeline
behaviour, an OpenAPI transformer, a controller attribute.

The failure mode is scope creep: the agent notices something adjacent, "helpfully" edits another
layer, and now a focused change is a cross-cutting one you have to review in full.

## Copy this

```text
Change only <PROJECT NAME>. Do not edit any other project.

Read AGENTS.md §2 (hard rules) and that project's blueprint; it sits in the
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
| Does it cross a boundary? | Adding a `DbSet` means both `IAppDbContext` (Application) and `AppDbContext` (Infrastructure). That is two layers: use [modify-feature.md](modify-feature.md) instead. |
| New abstraction? | Application declares the interface; Infrastructure implements it, **except** `IUser`, `ILanguageContext` and `ITextLocalizer`, which live in `Taxi.Api` because they need `HttpContext` or the API's resource files |
| Pipeline behaviour? | Registration order in `src/Taxi.Application/DependencyInjection.cs` *is* execution order, first = outermost. Changing it affects every request and is guarded by `PipelineRegistrationTests`. |
| Middleware? | Order in `UseCoreMiddlewares` is load-bearing: localization first, then the log context and security headers, the exception handler early, CORS before authentication, and the rate limiter **after** authentication (so the default policy can partition by user) and before authorization |
| Interceptor? | Registration order in `AddInfrastructure` is execution order: soft delete, then audit, then outbox |
| New configuration key? | Add it to `appsettings.json` (blank for secrets), validate it on start (`ValidateOnStart`), and document it in `README.md`'s configuration table |

## Watch for

Some things look like one layer and are not:

- A new entity field → Domain **and** Infrastructure (configuration + migration)
- A new endpoint → Api **and** Application, usually Contracts too
- A new localization key → Contracts **and** both `SharedResource` files in Api
- A new Application interface → Application **and** the project that implements it
- A new NuGet package → `Directory.Packages.props` **and** the `.csproj`
- A new policy → `Policies.cs` (Application) **and** `AddAppAuthorization` (Api)

If your change is on that list, you want a different template.

## Done means

- [ ] `dotnet build`: 0 warnings
- [ ] `dotnet test`: green
- [ ] `git status` shows files from **one** project only (plus its tests and docs), or the agent
      told you why not
