# Fix a bug

Something behaves wrongly. Before anything else: **it may not be a bug.**

Several behaviours in this codebase are deliberate and documented in the layer blueprints — the
Domain referencing Contracts, domain events publishing only after the commit, exception detail
being suppressed outside Development, the committed dev JWT placeholder. An agent that "fixes" one
of those undoes a decision made on purpose.

## Copy this

```text
Fix a bug.

Read AGENTS.md, then the blueprint for the layer you suspect. If the behaviour
is described there as a design decision, say so and stop — do not change it.

What happens:  <observed behaviour>
What I expect: <expected behaviour>
Repro:         <exact steps, request, or command>
Error text:    <paste it verbatim, including the stack trace if you have one>

Write a failing test that reproduces this BEFORE fixing it. Show me the test
failing, then the fix, then the test passing.

If the root cause is somewhere I did not point you, say so — do not silently
patch the symptom.
```

## Decide before you send

| Input | Why it matters |
|---|---|
| Exact error text | A paraphrase sends the agent hunting for the wrong string |
| Repro steps | Without them the agent cannot write the failing test, so it will guess at the cause |
| Expected behaviour | "It's broken" is not a spec. State what correct looks like. |
| Which layer you suspect | Offer it as a hint, not a fact, or the agent stops looking anywhere else |
| Environment | Development vs anything else. Several behaviours differ deliberately: exception detail is Development-only, migrations auto-apply outside Production. |

## Done means

- [ ] A test that failed before the fix and passes after — this is the deliverable, not the fix
- [ ] `dotnet build` — 0 warnings
- [ ] `dotnet test` — green
- [ ] Root cause named, not just the symptom patched
- [ ] If it turned out to be a documented design decision, the blueprint explains why and no code
      changed
