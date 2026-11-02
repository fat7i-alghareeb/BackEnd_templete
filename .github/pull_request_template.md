<!-- markdownlint-disable-file MD041 -- GitHub shows this under the PR title. -->
## Summary

<!-- What changes, and why. Link the issue if there is one. -->

## Type of change

- [ ] Feature
- [ ] Bug fix
- [ ] Refactor / cleanup
- [ ] Docs only
- [ ] Build / CI / dependencies

## How it was tested

<!-- Tests added or changed; manual steps (e.g. calls from requests/requests.http). -->

## Checklist

- [ ] `dotnet build Taxi_Server.slnx` is clean (CI builds with `-warnaserror`)
- [ ] `dotnet test Taxi_Server.slnx` passes (the integration suite needs Docker)
- [ ] Follows [AGENTS.md](../AGENTS.md): the `Car` / `Driver` shape, `Result<T>` instead of
      exceptions, no logic in controllers
- [ ] New endpoints have a named policy (`Policies.*`), `[ProducesResponseType]` for each status,
      and a call in `requests/requests.http`
- [ ] New localization keys exist in **both** `SharedResource.en.json` and `SharedResource.ar.json`
- [ ] Cached queries have a matching `RemoveByTagAsync` in every command that changes the data
- [ ] Requests carrying passwords, tokens or streams implement `ISensitiveRequest`
- [ ] Schema changes include a migration
- [ ] Docs updated (layer blueprint, `README.md`), and `npx markdownlint-cli2` passes
