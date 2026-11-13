---
title: "ADR 006: xUnit, NSubstitute, and functional tests against real Postgres"
weight: 60
---

# ADR 006: xUnit, NSubstitute, and functional tests against real Postgres

## Status
Accepted. Revised three times: functional tests originally ran on EF Core's InMemory provider and now run against a real Postgres (Testcontainers); architecture tests were added to enforce the layering mechanically; and the tests now follow [ardalis/CleanArchitecture](https://github.com/ardalis/CleanArchitecture/tree/main/tests)'s conventions, with persistence tests in their own integration test project.

## Context
The template needs a default testing setup that new projects inherit without a setup decision of their own, and that gives real confidence in the behavior a feature copies from the `User` reference slice.

The first version ran functional tests on EF Core's InMemory provider, so they needed no Docker and ran the same on Windows, Linux and macOS. That turned out to hide exactly the behavior that matters most:

- InMemory doesn't enforce unique indexes, so the email/external-id uniqueness was untested.
- It doesn't run database defaults (`CreatedAtUtc` uses `now()`), raw SQL (`ListUsersQueryService`'s `FromSqlRaw` had to be stubbed out), or transactions the way Postgres does.
- Replacing the app's `AddDbContext` meant re-registering it by hand, which silently dropped the domain-event `SaveChangesInterceptor`.

Microsoft also [recommends against](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy#in-memory-as-a-database-fake) InMemory as a database fake.

## Decision
Five test projects, following [ardalis/CleanArchitecture](https://github.com/ardalis/CleanArchitecture/tree/main/tests):

- **Conventions:**
  - xUnit v3 (pinned to 3.2.x with `xunit.runner.visualstudio` 3.1.x: 4.x requires Microsoft.Testing.Platform on the .NET 10 SDK), [Shouldly](https://docs.shouldly.org/) assertions and [NSubstitute](https://nsubstitute.github.io/) fakes.
  - One class per member or behavior, named `<Type><Member>` (`UserCreate`, `LoginHandlerHandle`, `AuthRefreshTokenPost`), with methods named for the behavior.
  - `TestContext.Current.CancellationToken`, and a `GlobalUsings.cs` per project.
- **`AppTemplate.UnitTests`** - Core (aggregates, Vogen value objects, domain event handlers) and UseCases (command and query handlers) in isolation. Handlers are tested against `IRepository<T>` substituted with NSubstitute, not a real database.
- **`AppTemplate.IntegrationTests`** - Infrastructure against a real Postgres. `PostgresTestDatabase` (`tests/Shared`, compiled into both Docker projects) starts one Postgres container per test run with [Testcontainers](https://dotnet.testcontainers.org/) (same image major version as the AppHost). `BaseEfRepoTestFixture` gives each class its own database, built by the real migrations and wired with the real `AddInfrastructureServices`.
  - **Covered:** repositories (`EfRepositoryAdd`, `EfRepositoryUpdate`, `EfRepositoryDelete`, including cascades and a concurrent delete), ASP.NET Core Identity through `IdentityService` (`Identity/`: account creation, lockout, confirmation and reset codes, and `AccountCodeProtection`, which checks that codes survive a new service provider because the Data Protection keys are in Postgres; ADR 013), the raw-SQL `ListUsersQueryService`, and `MigrationDrift`.
  - The fixture registers what Web would provide for Identity: a recording email sender, the account link settings and the authentication services `SignInManager` needs.
- **`AppTemplate.FunctionalTests`** - the real HTTP pipeline (FastEndpoints -> Mediator -> EF Core -> Postgres) through `CustomWebApplicationFactory<Program>`, with a fresh database per factory in the same container. The app's DbContext registration is used as is.
  - **Endpoints:** tests live in `ApiEndpoints/`, one class per route and verb (`UserGetById`, `AuthLoginPost`), with `Ardalis.HttpClientTestExtensions` for status code assertions.
  - **Real sign-in:** there is no test authentication handler. `TestSession` signs in through the real endpoints and is a cookie jar on `https://localhost`: it keeps the session cookie, fetches an antiforgery token after signing in, and sends it as `X-XSRF-TOKEN` on unsafe requests, as the SPA does. `factory.CreateUserAsync(role)` creates a confirmed account and its profile through `IdentityService` (a `Role`, `Member` by default), `factory.CreateSignedInSessionAsync(role)` also signs them in, and `factory.EmailSender.LastLinkQueryTo(email)` follows an emailed link. `factory.Time` (a `FakeTimeProvider`, which the cookie and Identity's stamp validator use) moves the clock past the stamp check, and `factory.EmailSender` records emails.
  - FastEndpoints keeps static state per process, so the project runs its test classes one at a time (`xunit.runner.json`).
- **`AppTemplate.ArchitectureTests`** - enforces the rules of [ADR 001]({{< relref "adr-001-clean-architecture-layering" >}}) so they can't erode:
  - `ProjectReferenceTests` reads every `src/*.csproj` and allows only the references each layer may have. It catches a forbidden `ProjectReference` even before any code uses it, which the compiled assembly can't show. A new project fails until it is added to the allow-list.
  - `LayerDependencyTests` ([NetArchTest](https://github.com/BenMorris/NetArchTest)) inspects the compiled code. Core uses no outer layer, EF Core or ASP.NET Core. UseCases uses no Infrastructure, Web or infrastructure SDK. No layer uses MediatR.
  - `ConventionTests`: endpoints live in a feature namespace under `AppTemplate.Web` (`AppTemplate.Web.Endpoints.Users`, from `Web/Endpoints/Users/`), there are no MVC controllers, command and query handlers live only in UseCases, and commands and queries follow the `<UseCase>Command`/`<UseCase>Query` naming.
  - It needs no Docker, so it runs on every CI operating system.
- **`AppTemplate.AspireTests`** - one smoke test of the whole local composition. It starts the real AppHost with [Aspire.Hosting.Testing](https://learn.microsoft.com/en-us/dotnet/aspire/testing/write-your-first-test) and waits for the API to be healthy. The other projects test the API (`WebApplicationFactory`) and the infrastructure (Testcontainers) on their own; only this one notices a broken resource reference, a setting the AppHost no longer passes, or a health check that never turns healthy. It overrides the AppHost's persistent containers and data volumes with throwaway ones, so it never touches a developer's data. The smoke test itself is deliberately a single startup check.

One rule needs the running app, so it lives in the functional tests: `EndpointAuthorization` pins the exact set of anonymous routes ([ADR 011]({{< relref "adr-011-role-based-permissions" >}}), [ADR 012]({{< relref "adr-012-jwt-authentication-fastendpoints" >}})).

For the integration and functional tests, Testcontainers was chosen over `Aspire.Hosting.Testing`, which would start the whole AppHost (Mailpit, the frontend) for every run - far more than they need. `Aspire.Hosting.Testing` is used only where starting the whole AppHost is the point: the smoke test and the browser tests, both in `AppTemplate.AspireTests`.

- **Browser tests** (`AppTemplate.AspireTests/EndToEnd`) of critical journeys, after the pattern in NimblePros/eShopOnWeb's `EndToEndTests`. They share the smoke test's AppHost: an assembly fixture (`AppHostFixture`) starts it once per run, with the throwaway containers, and waits for the API and the Angular app to be healthy. A separate project would start a second AppHost in parallel, and both would run `npm ci` in `frontend/` at once. The tests drive headless Chromium with [Playwright for .NET](https://playwright.dev/dotnet/). It starts with one test, `SignIn.AdminSignsInAndSeesTheUsers`: the seeded admin signs in through the Angular form and opens the users page. That covers what no backend test can: the dev server's proxy, the session cookie and antiforgery token in a real browser, and the route guards. A failing test saves a Playwright trace and a screenshot.

Browser tests are slow and the most fragile layer, so they cover **journeys, not rules**: a sign-in, a sign-up through the emailed link, a page that only an admin reaches. Validation messages, permission combinations, error mappings and the like stay in the unit and functional tests, where they run in milliseconds and fail precisely:

| Scenario | Example here | Layer |
|---|---|---|
| A domain rule | `UserName` rejects an empty name | Unit |
| A use case's decisions | an update by someone without `users:write` is `Forbidden` | Unit |
| Persistence and Identity | a taken email, lockout, a migration that drifts | Integration |
| An endpoint's contract | status codes, problem details, which routes are anonymous | Functional |
| The composition | the AppHost starts and the API is healthy | Aspire smoke test |
| A critical user journey | the admin signs in and sees the users | End-to-end |

## Consequences
- Integration and functional tests need Docker (or a Docker-compatible engine such as Podman, via `DOCKER_HOST`). Without it they fail fast with a message saying so.
- Every test class that needs Postgres is marked `[Trait(TestCategories.Name, TestCategories.RequiresDocker)]`. Tests that don't (`ResultExtensionsToProblem`, `OptionsValidationAtStartup`) are not, so `dotnet test --filter "Category!=RequiresDocker"` still runs them anywhere.
- The end-to-end tests need Chromium and its system libraries: CI's Linux job installs them (`playwright.ps1 install --with-deps chromium`) and uploads the traces of failed tests; locally, the fixture downloads Chromium on its first run.
- CI runs the full suite only on Linux; hosted Windows and macOS runners can't run Linux containers, so they use that filter (see `backend-build.yml`).
- Tests now catch Postgres-only behavior: unique violations, database defaults, owned types, raw SQL and concurrency (`AppTemplate.IntegrationTests`).
- Migrations can't drift from the model (`IntegrationTests/Data/MigrationDrift`). A model change without a migration fails `HasPendingModelChanges()`. The schema the migrations build is also compared, column by column (including types, lengths and defaults), constraint by constraint and index by index, with the schema `EnsureCreated` builds straight from the model. That catches what the model snapshot can't see: a hand-edited migration, or a default EF added to fill existing rows and never dropped. The first run found exactly that on `users.external_id`.
- Breaking a layering rule fails the build's tests, not just a review. Change a rule only together with the ADR that justifies it.
- NSubstitute was chosen over Moq for its simpler syntax and permissive license; either works fine here since `IRepository<T>` and friends are plain interfaces.
