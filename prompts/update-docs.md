# Update the docs

Run this after any change that alters a convention, a folder, a signature, a registration or a
configuration key.

Doc drift is how this repo got into trouble once already: the blueprints described a different
template entirely: namespaces, folders and subsystems that did not exist. An agent following them
wrote code that would not compile. Docs are only useful while they are true.

## Copy this

```text
Resync the documentation with the code.

Read AGENTS.md, then the blueprint for each layer I changed.

What changed: <the code change you just made>

Update every doc that now describes something untrue. At minimum check:
  - AGENTS.md: hard rules, the §3 checklist, the §0 routing table (keep it a short digest)
  - docs/AGENT_REFERENCE.md: §4-§9 detail, §11 deliberate decisions, §12 known gaps
  - the blueprint for each affected layer
  - docs/ARCHITECTURE.md if a layer responsibility or the dependency graph moved
  - docs/RESTful_Naming_Constitution.md if a route, verb, status or header moved
  - README.md if setup, a configuration key, an endpoint or a port moved
  - docs/NAVIGATION.md if a document was added, moved or renamed

Rules:
  - Document only what EXISTS. Anything planned goes under
    "Future Extensions — NOT IMPLEMENTED".
  - Do not duplicate. If AGENTS.md owns a rule, the blueprint points at it.
  - Every file path, class name, number and section reference you write must be
    real: verify each one against the source, not from memory.
  - Keep section numbers stable. If you must renumber, grep the repository for
    the old reference ("§") and fix every link.

Then run `npx markdownlint-cli2` and confirm 0 issues.
```

## Decide before you send

| Input | Why it matters |
|---|---|
| What actually changed | The agent cannot diff intent. Name the change or it will re-audit everything and still miss things. |
| Convention or detail? | A changed convention means `AGENTS.md` plus every blueprint that echoes it. A local detail means one blueprint. |
| Did anything get deleted? | Deletions are the drift that hurts most: docs keep describing code that is gone. Say what you removed. |
| Aspirational or real? | If you are documenting something not built yet, it belongs under *Future Extensions*, never in the main body |
| Counts and numbers | Test counts, key counts, endpoint counts and port numbers rot fastest. Re-derive them by running the command, never by editing the old number |

## Where each kind of fact lives

| Fact | Owner |
|---|---|
| Rules, the feature checklist | `AGENTS.md` |
| Setup, ports, configuration keys, deployment | `README.md` |
| How a layer is built, and why | that layer's blueprint |
| Routes, verbs, status codes, headers | `docs/RESTful_Naming_Constitution.md` + Api blueprint §6 |
| The system-level picture | `docs/ARCHITECTURE.md` |

## Done means

- [ ] `npx markdownlint-cli2`: 0 issues
- [ ] Every relative link and every `§` reference resolves to a real file and heading
- [ ] No doc describes a file, class, or method that does not exist
- [ ] Nothing planned-but-unbuilt appears outside a *Future Extensions* section
- [ ] `docs/NAVIGATION.md` updated if a document was added, moved or renamed
- [ ] Numbers you touched (tests, keys, endpoints, ports) were re-derived from the code
