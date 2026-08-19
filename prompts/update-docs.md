# Update the docs

Run this after any change that alters a convention, a folder, a signature, or a registration.

Doc drift is how this repo got into trouble once already: the blueprints described a different
template entirely — namespaces, folders and subsystems that did not exist. An agent following them
wrote code that would not compile. Docs are only useful while they are true.

## Copy this

```text
Resync the documentation with the code.

Read AGENTS.md, then the blueprint for each layer I changed.

What changed: <the code change you just made>

Update every doc that now describes something untrue. At minimum check:
  - AGENTS.md — hard rules, the §3 checklist, the §0 routing table
  - the blueprint for each affected layer
  - docs/ARCHITECTURE.md if a layer responsibility or the dependency graph moved

Rules:
  - Document only what EXISTS. Anything planned goes under
    "Future Extensions — NOT IMPLEMENTED".
  - Do not duplicate. If AGENTS.md owns a rule, the blueprint points at it.
  - Every file path, class name and section number you write must be real —
    verify each one against the source, not from memory.

Then run `npx markdownlint-cli2` and confirm 0 issues.
```

## Decide before you send

| Input | Why it matters |
|---|---|
| What actually changed | The agent cannot diff intent. Name the change or it will re-audit everything and still miss things. |
| Convention or detail? | A changed convention means `AGENTS.md` plus every blueprint that echoes it. A local detail means one blueprint. |
| Did anything get deleted? | Deletions are the drift that hurts most — docs keep describing code that is gone. Say what you removed. |
| Aspirational or real? | If you are documenting something not built yet, it belongs under *Future Extensions*, never in the main body |

## Done means

- [ ] `npx markdownlint-cli2` — 0 issues
- [ ] Every relative link resolves
- [ ] No doc describes a file, class, or method that does not exist
- [ ] Nothing planned-but-unbuilt appears outside a *Future Extensions* section
- [ ] `docs/NAVIGATION.md` updated if a document was added, moved or renamed
