# AppTemplate.AspireTests

Tests of the whole local composition. `AppHostFixture` starts the real AppHost once per run (Postgres, Redis, Mailpit, the API and the Angular dev server, with throwaway containers), and two kinds of test share it:

- **The smoke test** (`AppHostStartup`): the API and the Angular app come up healthy.
- **Browser tests** (`EndToEnd/`) of critical user journeys, with [Playwright for .NET](https://playwright.dev/dotnet/), after the `EndToEndTests` in NimblePros/eShopOnWeb. They drive headless Chromium against the Angular app. Why browser tests cover journeys and nothing else: [ADR 006](../../../docs/content/architecture-decisions/adr-006-testing-strategy.md).

They share one AppHost because two, started in parallel by `dotnet test`, would both run `npm ci` in `frontend/`.

## Run them

From `backend/`, with Docker running (or Podman: `DOCKER_HOST`, `TESTCONTAINERS_RYUK_DISABLED=true` and `ASPIRE_CONTAINER_RUNTIME=podman`) and the development certificate trusted (`dotnet dev-certs https --trust`):

```bash
dotnet test tests/AppTemplate.AspireTests
```

The first browser test downloads Chromium into `~/.cache/ms-playwright`; starting the AppHost (`npm ci`, the Angular build) takes a minute or two. On a fresh Linux or WSL machine, Chromium may also need system libraries, which need root to install:

```bash
pwsh tests/AppTemplate.AspireTests/bin/Debug/net10.0/playwright.ps1 install-deps chromium
```

(Without PowerShell: `node tests/AppTemplate.AspireTests/bin/Debug/net10.0/.playwright/package/cli.js install-deps chromium`.) CI's Linux job installs both with `install --with-deps chromium`.

## When a test fails

It saves a screenshot and a Playwright trace to `bin/<Configuration>/net10.0/TestResults/playwright/` (CI uploads them as the `playwright-traces` artifact). Open the trace, which shows every action with the DOM, network and console at that moment:

```bash
pwsh tests/AppTemplate.AspireTests/bin/Debug/net10.0/playwright.ps1 show-trace <file>-trace.zip
```

or drop it on [trace.playwright.dev](https://trace.playwright.dev).

## Write a new browser test

- **Journeys only:** a sign-up through the emailed link, an admin-only page, a flow across several pages. Rules and their combinations belong in the unit and functional tests, which are faster and fail precisely.
- **Start from a recording** if it helps: run the AppHost, then `playwright.ps1 codegen <the angular URL>`, and clean the draft up.
- **Locate what the user sees:** `GetByRole`, `GetByLabel` and `GetByText`, not CSS classes or XPath. Assert with `Expect(...)`, which waits and retries, never with fixed delays.
- **One fresh context per test** (`app.NewBrowserContextAsync()`), so tests don't share cookies, and wrap the steps as `SignIn` does to save the trace on failure.
- **Use the seeded accounts** (`admin@example.com`, `user1@example.com`, ...; `SeedData`) or register a new one; the database lives only for the run.
