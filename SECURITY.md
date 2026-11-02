# Security Policy

## Reporting a vulnerability

**Do not open a public issue or pull request for a security problem.**

Use GitHub's private reporting: open the repository's **Security** tab and choose **Report a
vulnerability**. If that option is not available, contact the maintainer,
[Fathi Alghareeb](https://github.com/fat7i-alghareeb), through the contact details on their GitHub
profile and ask for a private channel before sharing any detail.

Please include:

- what is affected (endpoint, file or configuration key) and the version or commit;
- steps to reproduce, ideally a request from `requests/requests.http` or a failing test;
- the impact you expect (data exposure, privilege escalation, denial of service);
- whether you want to be credited.

You can expect an acknowledgement, then a fix or a reasoned decision. Please allow time to ship a
fix before you disclose publicly.

## Supported versions

This is a template, not a released product. Only the `main` branch is supported. A project created
from the template owns its own security from the day it is cloned: watch the Dependabot pull
requests and the vulnerable-package check in CI.

## What counts

In scope: the code, configuration and compose files in this repository that could expose users of
a deployment built from it, for example an authentication or authorization bypass, injection, a
secret written to logs, an unsafe file upload, or a header that leaks internals.

## Known and intentional (not vulnerabilities)

These are documented trade-offs of a template, listed in [AGENT_REFERENCE.md §11 and §12](docs/AGENT_REFERENCE.md):

- The development JWT secret and the seeded `Seed:Admin` / `Seed:User` passwords are public
  placeholders in `appsettings.Development.json`. Never deploy with them.
- `/metrics` is anonymous (a scrape target on the compose network); protect it at your proxy.
- The Identity password policy is deliberately relaxed (6 characters).
- ASP.NET Core Data Protection keys are not persisted or shared, so email-confirmation and
  password-reset tokens do not survive a restart or a second instance.
- `Email:Provider=Log` writes one-time links to the log; it is a development default.

A report that one of these is *worse than documented* is still welcome.

## Hardening already in the template

JWT validated at startup (secret of at least 32 characters), rotating single-live refresh tokens,
account lockout, uniform login failures and 202 responses that resist account enumeration, per-user
and per-IP rate limiting, security headers, request-size limits, uploads identified by magic bytes
with server-generated storage keys, secrets redacted from request logs, exception detail hidden
outside Development, and a CI gate that fails on vulnerable packages. The full list is in
[docs/ARCHITECTURE.md §12](docs/ARCHITECTURE.md).

## Before you deploy

Replace every development secret, set your own seed credentials, put a TLS reverse proxy in front
and configure `ForwardedHeaders`, restrict CORS to your front end, decide on the password policy,
and keep `/metrics`, Seq and Grafana off the public internet. The checklist is in the README's
[Deployment](README.md#-deployment) section.
