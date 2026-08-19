# Prompts

Fill-in-the-blank templates for briefing an AI agent on this codebase.

[AGENTS.md](../AGENTS.md) teaches the agent *how this repo works*. These templates cover the other
half: **what only you know** — the business rules, whether a field is bilingual, whether a list is
cached, which role guards an endpoint. Front-load those and the agent stops guessing.

| Template | Use when |
|---|---|
| [add-feature.md](add-feature.md) | New entity, full vertical slice from contract to controller |
| [modify-feature.md](modify-feature.md) | Add a field or change a rule on something that already exists |
| [single-layer-change.md](single-layer-change.md) | Work confined to one project |
| [fix-bug.md](fix-bug.md) | Something is broken |
| [update-docs.md](update-docs.md) | Resync the docs after a code change |

## Four rules for every prompt

1. **Name `AGENTS.md`.** One sentence — "Read AGENTS.md first." Its §0 routing table sends the
   agent to the right blueprint on its own. Listing blueprints yourself just goes stale.
2. **Give business rules, not file paths.** Say "a car's year cannot precede 1886", not "edit
   `Car.cs` line 40". The paths are derivable from the pattern; the rule is not.
3. **Demand a stop, not a guess.** End with *"if this conflicts with the existing pattern, stop and
   tell me instead of inventing a new one."* Most bad output comes from an agent resolving your
   ambiguity silently.
4. **Ask for proof.** `dotnet build` and `dotnet test` output, not "done". A claim is not a result.

## When to skip these

A typo, a renamed variable, a one-line tweak. Ceremony costs more than the change. Templates are
for work that touches more than one file or more than one layer.
