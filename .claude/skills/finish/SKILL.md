---
name: finish
description: End-of-feature routine — update docs, style and memory for what changed.
disable-model-invocation: true
---

# Finish a feature

Run this when a feature, endpoint group or shared component is done (skip it for a bug fix or a
one-line change). Be economical: look only at what changed (`git diff --stat`, `git status`) and open
only the docs a row below sends you to.

1. **What changed?** `git status --short` and `git diff --stat`. Name: new feature / new shared
   helper / new convention / new decision / new command or setting.
2. **Leftovers.** `grep -rn "TODO-AR" src/` must find nothing. New keys exist in **both** `SharedResource`
   files. Every new `DbSet` is in `IAppDbContext`, `AppDbContext` and `TestAppDbContext`.
3. **Duplicates.** For each new helper or DTO: `pwsh ./tool/catalog.ps1 <word>`. If it overlaps an
   existing one, do **not** merge on your own — one question to me (merge, add a parameter, or keep
   both and why).
4. **Update only what applies** (one line or one row each; do not rewrite pages):

   | If you added… | Update |
   | --- | --- |
   | a feature | its row in `docs/PROJECT_MAP.md`; its spec in `docs/features/` kept true |
   | an endpoint | calls in `requests/requests.http`; the endpoint table in `README.md` |
   | a command, script or setting | `docs/COMMANDS.md`; the configuration table in `README.md` |
   | a durable decision (and why) | `docs/DECISIONS.md` |
   | a convention | `AGENTS.md` and, if detailed, `docs/AGENT_REFERENCE.md` + the layer blueprint |
   | a document | `docs/NAVIGATION.md` |

5. **Style.** If the feature introduced a convention worth repeating, or I corrected you during it,
   add a line to `docs/CODE_STYLE.md` (marked `corrected` with my words and the reason).
6. **Memory.** Save only what the repo cannot tell the next chat: a decision and its reason, a
   preference of mine, a verified external fact with its URL. One fact per file.
7. **Verify.** `dotnet build Taxi_Server.slnx` → 0 warnings; the new and changed tests, then the whole
   suite **once** (end of a feature; Docker needed); `npx markdownlint-cli2` if any `.md` changed.
8. **Report in five lines or fewer:** what you updated, what you skipped and why, the duplicate
   question if any, check results. Do not commit.
