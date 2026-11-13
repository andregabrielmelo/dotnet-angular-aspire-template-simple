# Changelog

All notable changes to **this template** are documented here (not changes to projects started *from* it). Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added

- Bearer tokens for non-browser clients: `POST /auth/token`, accepted next to the session cookie (ADR 018), after NimblePros/eShopOnWeb's token service.
- `ILoggerAdapter<T>` and `LoggerAdapter<T>`: every layer logs through the adapter, and Core and UseCases no longer reference `Microsoft.Extensions.Logging`.
- Signed-in requests tag their trace with `user.id` and their logs with `UserId` (`UserContextEnrichmentMiddleware`).
- `scripts/BuildTestFormat.cs`: runs every CI check locally, with a pass/fail summary.
- A Playwright browser test on the shared AppHost (`AppTemplate.AspireTests/EndToEnd`).
- A dev container (`.devcontainer/`).
- Docs: a patterns page and an FAQ.
- Derived from [dotnet-angular-aspire-template](https://github.com/andregabrielmelo/dotnet-angular-aspire-template) at commit 84209d9, keeping the features most projects use. The removed features (background jobs, outbox and inbox, auditing, caching, file storage, ETags, API versioning, third-party sign-in, the custom password reset endpoint, business metrics and the end-to-end tests) remain in that repository.

[Unreleased]: https://github.com/andregabrielmelo/dotnet-angular-aspire-template-simple/commits/main
