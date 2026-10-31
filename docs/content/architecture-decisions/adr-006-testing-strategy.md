---
title: "ADR 006: xUnit, NSubstitute, and functional tests against real Postgres"
weight: 60
---

# ADR 006: xUnit, NSubstitute, and functional tests against real Postgres

## Status
Accepted. Revised twice: functional tests originally ran on EF Core's InMemory provider and now run against a real Postgres (Testcontainers); architecture tests were added to enforce the layering mechanically.

## Context
The template needs a default testing setup that new projects inherit without a setup decision of their own, and that gives real confidence in the behavior a feature copies from the `User` reference slice.

The first version ran functional tests on EF Core's InMemory provider, so they needed no Docker and ran the same on Windows, Linux and macOS. That turned out to hide exactly the behavior that matters most:

- InMemory doesn't enforce unique indexes, so the email/external-id uniqueness was untested.
- It doesn't run database defaults (`CreatedAtUtc` uses `now()`), raw SQL (`ListUsersQueryService`'s `FromSqlRaw` had to be stubbed out), or transactions the way Postgres does.
- Replacing the app's `AddDbContext` meant re-registering it by hand, which silently dropped the domain-event `SaveChangesInterceptor`.

Microsoft also [recommends against](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy#in-memory-as-a-database-fake) InMemory as a database fake.

## Decision
Three test projects, all xUnit:

- **`AppTemplate.UnitTests`** - tests Core (aggregates, Vogen value objects) and UseCases (command/query handlers) in isolation. Handlers are tested against `IRepository<T>` substituted with [NSubstitute](https://nsubstitute.github.io/), not a real database.
- **`AppTemplate.FunctionalTests`** - drives the real HTTP pipeline (FastEndpoints -> Mediator -> EF Core -> Postgres) through `WebApplicationFactory<Program>`. `PostgresTestDatabase` starts one Postgres container per test run with [Testcontainers](https://dotnet.testcontainers.org/) (same image major version as the AppHost), and each `AppTemplateWebApplicationFactory` gets its own database in it, created by running the real migrations. The app's DbContext registration is used as is.
- **`AppTemplate.ArchitectureTests`** - enforces the rules of [ADR 001]({{< relref "adr-001-clean-architecture-layering" >}}) so they can't erode:
  - `ProjectReferenceTests` reads every `src/*.csproj` and allows only the references each layer may have. It catches a forbidden `ProjectReference` even before any code uses it, which the compiled assembly can't show. A new project fails until it is added to the allow-list.
  - `LayerDependencyTests` ([NetArchTest](https://github.com/BenMorris/NetArchTest)) inspects the compiled code. Core uses no outer layer, EF Core or ASP.NET Core. UseCases uses no Infrastructure, Web or infrastructure SDK. No layer uses MediatR.
  - `ConventionTests`: endpoints live under `Web.Features`, there are no MVC controllers, command and query handlers live only in UseCases, and commands and queries follow the `<UseCase>Command`/`<UseCase>Query` naming.
  - It needs no Docker, so it runs on every CI operating system.

One rule needs the running app, so it lives in the functional tests: `EndpointAuthorizationTests` pins the exact set of anonymous routes and checks the authorization fallback policy ([ADR 011]({{< relref "adr-011-role-based-permissions" >}})). Sign-in, refresh-token rotation and the CSRF checks run through the real JWT handler (`AuthEndpointsTests`, `BrowserSession`); everything else authenticates with `TestAuthHandler`.

For functional tests, Testcontainers was chosen over `Aspire.Hosting.Testing`, which would start the whole AppHost (Mailpit, the frontend) for every run - far more than they need.

There are no end-to-end browser tests in this template. The full template ([dotnet-angular-aspire-template](https://github.com/andregabrielmelo/dotnet-angular-aspire-template)) has an `AppTemplate.EndToEndTests` project that starts the AppHost with `Aspire.Hosting.Testing` and drives Chromium with Playwright through sign-in, profile editing and sign-out; copy and adapt it when the sign-in round trip through the Angular app needs a test of its own.

## Consequences
- Functional tests need Docker (or a Docker-compatible engine such as Podman, via `DOCKER_HOST`). Without it they fail fast with a message saying so.
- Every test class that uses the factory is marked `[Trait(TestCategories.Name, TestCategories.RequiresDocker)]`. Tests that don't need the database (e.g. the startup configuration tests) are not, so `dotnet test --filter "Category!=RequiresDocker"` still runs them anywhere.
- CI runs the full suite only on Linux; hosted Windows and macOS runners can't run Linux containers, so they use that filter (see `backend-build.yml`).
- Tests now catch Postgres-only behavior: unique violations, database defaults, owned types, raw SQL and concurrency (`Data/PostgresPersistenceTests`).
- Migrations can't drift from the model (`Data/MigrationDriftTests`). A model change without a migration fails `HasPendingModelChanges()`. The schema the migrations build is also compared, column by column (including types, lengths and defaults), constraint by constraint and index by index, with the schema `EnsureCreated` builds straight from the model. That catches what the model snapshot can't see: a hand-edited migration, or a default EF added to fill existing rows and never dropped. The first run found exactly that on `users.external_id`.
- Breaking a layering rule fails the build's tests, not just a review. Change a rule only together with the ADR that justifies it.
- NSubstitute was chosen over Moq for its simpler syntax and permissive license; either works fine here since `IRepository<T>` and friends are plain interfaces.
