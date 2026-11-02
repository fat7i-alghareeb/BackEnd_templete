# Contributing

Thanks for improving the template. This page is the human workflow; the engineering rules are in
[AGENTS.md](AGENTS.md) and apply to people and AI agents alike.

## Set up

You need the .NET 10 SDK and Docker Desktop. Then:

```bash
git clone https://github.com/fat7i-alghareeb/BackEnd_templete.git
cd BackEnd_templete
docker compose up -d postgres
dotnet run --project src/Taxi.Api
```

The [README](README.md) has the full quick start, ports and seeded accounts.
[docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) covers the usual first-run problems.

## Before you start

- **Read [AGENTS.md](AGENTS.md).** It lists the hard rules, the 17-step feature checklist and the
  decisions that look like bugs but are not (§11) and the known gaps (§12).
- **Open an issue first** for anything that changes a convention, a public route or a dependency.
- **Keep one concern per pull request.** A feature, a fix or a docs change, not all three.

## Make the change

1. Branch from `main`.
2. Copy the matching `Car` or `Driver` file for each step of the checklist
   ([AGENTS.md §3](AGENTS.md)).
3. Add tests at the lowest layer that proves the behaviour (domain, handler, integration).
4. Add or update calls in [requests/requests.http](requests/requests.http) for any endpoint you
   touched.
5. Update the docs that your change makes untrue ([prompts/update-docs.md](prompts/update-docs.md)).

## Check it locally

```bash
dotnet build Taxi_Server.slnx -c Debug       # 0 warnings; CI builds with -warnaserror
dotnet test Taxi_Server.slnx                 # the integration suite needs Docker
dotnet list package --vulnerable --include-transitive
npx markdownlint-cli2                        # if you touched any .md
```

CI runs the same checks, plus a Docker image build, and fails on any vulnerable package.

## Commit messages

The history uses Conventional Commits with an optional scope and the pull request number:

```text
feat(drivers): second reference feature: child collection, relationship, domain event (#25)
fix(auth): persist RefreshToken.ExpiresOnUtc: token refresh never worked
docs: fix blockquote lint error in the Application blueprint
```

Types in use: `feat`, `fix`, `docs`, `test`, `chore`, `ci`, `build`. Say *why* in the body when it
is not obvious.

## Pull requests

The [pull request template](.github/pull_request_template.md) is the checklist. In short: green
build and tests, a named policy and `[ProducesResponseType]` on new endpoints, localization keys in
both languages, a cache eviction for every cached query you touch, a migration for every schema
change (read its SQL), no secrets, docs updated. CODEOWNERS requests review automatically.

## Working with an AI agent

Use the templates in [prompts/](prompts/README.md). For Claude Code the repository ships slash
commands and a feature generator: [docs/USING_CLAUDE.md](docs/USING_CLAUDE.md). Review agent
output like any other change: run the checks above yourself and read the diff; do not accept
"done" without build and test output.

## Reporting a security problem

Do not open a public issue. See [SECURITY.md](SECURITY.md).

## License

By contributing you agree that your contribution is released under the [MIT License](LICENSE).
