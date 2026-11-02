---
name: first-run
description: Set Claude up for a new project made from this template. Run once.
disable-model-invocation: true
---

# First run for this project

Do this once, in order. Be economical: read each file at most once.

1. **Check the baseline.** `git status --short` (clean?), `dotnet --version` (10.x), `docker info`
   (is Docker running? the integration tests and `docker compose` need it). Then
   `dotnet build Taxi_Server.slnx` and `dotnet test tests/Taxi.Domain.UnitTests`. Report the baseline
   in one line. If the project is still named `Taxi`, say so.
2. **Read once** (skim, do not dump): `docs/PROJECT_MAP.md` and `docs/DECISIONS.md`. Everything else
   loads on demand.
3. **Ask me the project questions** with the question tool (2–4 options each, your recommendation
   first, batched in one set):
   - **Product name** (PascalCase) — runs `rename.ps1`; or keep `Taxi` for now.
   - **Sign-up and roles** — seeded admin only, or open registration with email confirmation; keep the
     two roles `Manager` / `User` or add one (a new role means new policies).
   - **Languages** — English and Arabic only (the template's design), or more (this is a large change:
     `LocalizedText` stores exactly two languages; say so).
   - **Infrastructure** — Redis now or later; email provider now (SMTP) or later (log only); file
     storage stays local disk or later.
   - **Deployment** — Docker Compose on a server, or undecided.
4. **Apply** the answers only when I confirm. For a new name: the git tree must be clean, so ask
   me to commit or stash first; then `pwsh ./rename.ps1 -Name <Name> -WhatIf` (show the summary),
   then the real run, then `dotnet build` and the smallest test project. Config changes go in
   `appsettings*.json` (never a secret). List what needs me: secrets (`dotnet user-secrets`),
   SMTP account, domain, TLS, `ClientApp` front-end URLs, CORS origins.
5. **Record.** Replace the line under "This project" in `CLAUDE.md` with a short block (≤ 12 lines):
   name, sign-up mode, roles, languages, infrastructure choices, what is still pending. Save the
   non-obvious decisions as auto-memory (one fact per file, with the reason) and in `docs/DECISIONS.md`.
6. **Manual checklist for me** (print it, short): `/memory` shows memory on; `/context` shows
   `CLAUDE.md` and the rules loaded; `.claude/settings.local.json` for personal settings and
   `CLAUDE.local.md` for private notes (both git-ignored).
7. Finish with: what you changed, what is pending, and the first question for my next task (usually
   "run `/build-project`?"). Do not commit.
