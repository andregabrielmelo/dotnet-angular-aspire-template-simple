# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

The agent-neutral rules (layer boundaries, endpoint and use case conventions, testing, the done-means checklist) live in [`AGENTS.md`](AGENTS.md), with path-specific rules in `.github/instructions/`. Follow them; this file adds commands and a detailed architecture tour, and must not contradict them.

## What this is

A GitHub **template repository** for starting new full-stack projects: a Clean Architecture .NET backend, an Angular frontend, and .NET Aspire orchestrating both locally. It is not a sample app - the `User` feature is a deliberate end-to-end reference slice (Core → UseCases → Infrastructure → Web) to copy the shape of when adding a real feature, then delete once no longer needed as a reference.

It is the **simple** variant of [dotnet-angular-aspire-template](https://github.com/andregabrielmelo/dotnet-angular-aspire-template): it keeps what most projects use (Keycloak sign-in through a backend for frontend, permissions, email, structured logging, health checks, rate limiting, tests) and leaves out background jobs, the transactional outbox, auditing, caching, file storage, ETags and API versioning. When a task needs one of those, copy it from the full template, with its tests and ADR.

Every project, namespace, folder, the Postgres database name, and the Keycloak realm/client names are currently named `AppTemplate` / `apptemplate`. When someone actually uses this template for a new project, they run `scripts/rename-template.sh YourProjectName` (a plain find-and-replace, not a `dotnet new` template engine) as their first step - keep that in mind if a task looks like it wants a "real" project name; the placeholder is intentional.

Step-by-step guides live in `docs/content/guides/`: `project-map.md` (layout, a request's path, vocabulary), `adding-a-feature.md` (a complete, tested `Project` example through every layer, including the frontend and tests) and `cookbook.md` (permissions, public endpoints, email, domain events, settings, external services, schema changes). Follow them when building something.

Full documentation (architecture, design decisions, ADRs) lives in `docs/` (a Hugo site) and is authoritative for *why* things are built this way - read it before making architectural changes: `docs/content/design-decisions.md` and `docs/content/architecture-decisions/adr-*.md`. The template builds on ardalis/CleanArchitecture and jasontaylordev/CleanArchitecture; when the ADRs don't settle a structural question, `docs/content/further-reading.md` lists the sources to check.

## Commands

All backend commands run from `backend/`; all frontend commands from `frontend/`.

### Backend (.NET 10)

```bash
dotnet run --project src/AppTemplate.AppHost      # runs Postgres + API + frontend together via Aspire
dotnet build AppTemplate.slnx
dotnet test AppTemplate.slnx                      # functional tests need Docker (or Podman)
dotnet test tests/AppTemplate.UnitTests/AppTemplate.UnitTests.csproj --filter "FullyQualifiedName~UserTests"  # single class/test
dotnet csharpier check .                          # format check (CI-enforced); `format .` to fix
```

New EF Core migration:

```bash
dotnet ef migrations add YourMigrationName \
  --project src/AppTemplate.Infrastructure \
  --startup-project src/AppTemplate.Web \
  -o Data/Migrations
```

`Data/MigrationDriftTests` fails when the model has changes no migration captures, or when the migrated schema differs from the model (column types, lengths, defaults, constraints, indexes).

Migrations apply automatically on startup only in the `Development` environment (`DatabaseConfigurations.StartDatabase`, called from `Program.cs`); elsewhere they're a no-op unless `Database:ApplyMigrationsOnStartup` is set. Seed data (`SeedData`) is only ever added in Development.

There is no pre-commit hook: run `dotnet csharpier format .` before committing (CI runs `dotnet csharpier check .`). Format again after generating code, for example EF migrations.

The API contract is committed: after changing an endpoint, request or response, run `scripts/export-openapi.sh` (from the repo root; writes `backend/openapi/v1.json` without needing any services) and then `npm run api:generate` in `frontend/` (writes `src/app/core/api/api-types.ts`). CI fails if either file is stale.

### Frontend (Angular)

```bash
npm ci
npm start                                          # ng serve - reached through the backend for frontend, not directly
npm run build
npm run csp:check                                  # fails if the built index.html breaks the CSP (inline scripts/handlers)
npm run test                                       # vitest, runs once (not watch mode)
npm run lint                                       # eslint (@angular-eslint), CI-enforced
npm run format:check                               # prettier check (CI-enforced); npm run format to fix
npm run api:generate                               # regenerate API types from backend/openapi/v1.json
```

### Docs site (Hugo, in `docs/`)

Not run day-to-day, but useful when editing docs: requires the `hugo-book` theme, which CI clones fresh each run rather than vendoring it. To build locally: `git clone --depth 1 https://github.com/alex-shpak/hugo-book themes/hugo-book` inside `docs/`, then `hugo --minify`. Don't commit `docs/public/`, `docs/themes/`, or `docs/resources/` (gitignored at the repo root).

## Architecture

### Backend: Clean Architecture, five projects under `backend/src/` (plus AppHost, ServiceDefaults and BackendForFrontend)

Dependencies point inward; nothing below depends on something above it in this list:

- **`AppTemplate.SharedKernel`** - base types shared by every layer: `EntityBase`/`EntityBase<TId>`, domain event interfaces + dispatch, and the `[PersonalData]`/`[SecretData]` data classifications the logging pipeline redacts. Kept in-repo (not a separate NuGet package) since this template is meant to be self-contained.
- **`AppTemplate.Core`** - the domain model: aggregates (`Aggregates/UserAggregate/`), value objects, specifications, domain events (`UserProvisioned`), and the ports Infrastructure implements against external resources (`Core/Interfaces`, such as `IEmailSender`). Near-zero external dependencies (`Ardalis.Specification`, `Ardalis.Result`, `Vogen`).
- **`AppTemplate.UseCases`** - CQRS commands/queries dispatched via `Mediator`, with the `LoggingBehavior` pipeline behavior in `UseCases/Behaviors/` (martinothamar/Mediator, source-generated - **not** MediatR). Depends on Core only; data access goes through `IRepository<T>` (`SharedKernel/IRepository.cs`, an `Ardalis.Specification` repository) and query-service interfaces defined here, implemented in Infrastructure.
- **`AppTemplate.Infrastructure`** - EF Core + Npgsql (`Data/ApplicationDatabaseContext.cs`), email (MailKit), repository/query-service implementations. Anything talking to the outside world lives here.
- **`AppTemplate.Web`** - the ASP.NET Core entry point and composition root (`Program.cs`, `Configurations/`). One [FastEndpoints](https://fast-endpoints.com/) class per endpoint under `Features/<Feature>Features/`, following the REPR pattern (Request-Endpoint-Response), with FluentValidation validators colocated in the same file as their endpoint. Endpoints map `Ardalis.Result` only through `Extensions/ResultExtensions.cs` (`ToOkResult` and siblings), and every error, including validation, 401/403, unmatched routes and exceptions, is an RFC 9457 problem details response with a `traceId` (ADR 009, `Configurations/ProblemDetailsConfigurations.cs`).

**`AppTemplate.BackendForFrontend`** sits outside the layers: it references only ServiceDefaults and is the browser's single origin. It runs the OpenID Connect flow against Keycloak, keeps the session in an HTTP-only `__Host-apptemplate` cookie, and proxies `/api/**` to Web with YARP, stripping `/api` and adding the user's access token as `Authorization: Bearer` (Duende.AccessTokenManagement refreshes it). Its session endpoints are `/backend-for-frontend/{login,register,user,logout}`. `/api/**` and `/backend-for-frontend/user` require the header `X-CSRF: 1`.

### Authentication

Keycloak is the OpenID Connect provider and owns credentials, registration, password reset and logout pages. The realm is `AppHost/Realms/apptemplate-realm.json`, imported on first start. Web accepts only Keycloak JWTs with audience `apptemplate-api` (`Configurations/AuthenticationConfigurations.cs`, `MapInboundClaims = false`, so read `sub`/`email`/`name`). FastEndpoints endpoints are authenticated unless marked `AllowAnonymous()`. Endpoints are unversioned: the API serves `/users`, which the browser calls as `/api/users` through the backend for frontend. Domain `User` rows are provisioned just in time by `GET /users/me`, linked through `User.ExternalId` (= `sub`), and raise the `UserProvisioned` domain event. Functional tests authenticate with `TestAuthHandler` via the `X-Test-User` header (`factory.CreateAuthenticatedClient(sub)`). Keycloak's admin console is on `http://localhost:8080`; the admin password is the `keycloak-password` parameter in the Aspire dashboard. See ADR 007.

Password reset is Keycloak's own: the realm sets `resetPasswordAllowed`, so its sign-in page has a "Forgot password?" link, and Keycloak sends the email through its `smtpServer` setting. In development that is the Mailpit container; read the emails in Mailpit's web UI.

Authorization (ADR 008): the API's permissions are Keycloak client roles on `apptemplate-api` (`users:read`, `users:write`, `users:delete`), defined in `UseCases/Authorization/Permission.cs` and bundled into realm roles such as `admin`. `Web/Authorization/KeycloakPermissionsClaimsTransformation` maps `resource_access.apptemplate-api.roles` to `permission` claims. Each permission is also an ASP.NET Core policy of the same name, so endpoints use `Policies(Permission.X)` for coarse checks. Resource-based rules, such as "may update own profile, or anyone's with users:write", live in use cases via `ICurrentUser` and return `Result.Forbidden()`, which maps to 403. `GET /users/me` returns the caller's permissions for the UI (`CurrentUserService`, `permissionGuard`), but the API always enforces them itself. In functional tests, grant permissions with `factory.CreateAuthenticatedClient(sub, Permission.UsersRead, ...)`; the test handler emits a real `resource_access` claim. The dev realm has an `admin`/`admin` account (temporary password) with the `admin` role.

The realm file is imported only while Keycloak's data volume is empty. After changing `apptemplate-realm.json`, delete that volume, or apply the change in the admin console.

### Email

`IEmailSender` (Core) is implemented by `Infrastructure/Email/MimeKitEmailSender` over MailKit, configured by `Mailserver:Hostname`/`Port` (`MailserverConfiguration`). The AppHost points it at Mailpit. Nothing sends email yet; call it from a use case, or from a handler of a domain event such as `UserProvisioned`. Functional tests replace it with `FakeEmailSender` (`factory.EmailSender`).

### Domain events

Entities register domain events (`RegisterDomainEvent`); `Infrastructure/Data/EventDispatchInterceptor` publishes them through Mediator right after `SaveChanges` succeeds. They are in-process and best-effort: a failing handler is logged and never fails the request, and nothing retries it. Work that must reliably follow a database change needs a transactional outbox, which this template leaves out (see `docs/content/design-decisions.md`, Transactions and side effects).

### Tests

Four test projects under `backend/tests/`, all xUnit (see ADR 006 for the reasoning):

- **`AppTemplate.UnitTests`** - Core/UseCases in isolation, `IRepository<T>` substituted with NSubstitute. No I/O.
- **`AppTemplate.FunctionalTests`** - full HTTP → FastEndpoints → Mediator → EF Core → Postgres pipeline via `WebApplicationFactory<Program>`. `PostgresTestDatabase` starts one real Postgres container per test run (Testcontainers), and each `AppTemplateWebApplicationFactory` gets a fresh database in it, created by the real migrations - so **Docker is required** (Podman works via `DOCKER_HOST` with `TESTCONTAINERS_RYUK_DISABLED=true`). Mark every test class that uses the factory `[Trait(TestCategories.Name, TestCategories.RequiresDocker)]`: CI's Windows/macOS jobs run `--filter "Category!=RequiresDocker"`, since they can't run Linux containers.
- **`AppTemplate.BackendForFrontend.Tests`** - the backend for frontend, in-process. No Docker.
- **`AppTemplate.ArchitectureTests`** - enforces ADR 001: allowed `ProjectReference`s per `src` project (a new project must be added to the allow-list), forbidden type dependencies per layer (NetArchTest), endpoints under `Web.Features`, no MVC controllers, handlers only in UseCases, and `<UseCase>Command`/`<UseCase>Query` naming. Run it with `dotnet test tests/AppTemplate.ArchitectureTests`.

Every route requires an authenticated user unless it calls `AllowAnonymous()`: there is an authorization fallback policy, and `EndpointAuthorizationTests` (functional) pins the list of anonymous routes, so update it when you deliberately make an endpoint public.

### Cross-cutting concerns

Strongly-typed IDs and simple domain primitives (`UserId`, `UserName`) use [Vogen](https://github.com/SteveDunn/Vogen) source-generated value objects with a `Validate` method enforcing invariants at construction. EF Core conversions for them are registered centrally in `Infrastructure/Data/Configurations/VogenEfCoreConverters.cs` - add new value objects there, not per-entity. New entity IDs also need a `HasValueGenerator<VogenIdValueGenerator<...>>()` call in that entity's `IEntityTypeConfiguration` (see `UserConfiguration.cs`).

Health probes (`ServiceDefaults/Extensions.cs`, every environment): `/alive` (process) and `/health` (readiness: Postgres, tag `ready`).

Logging is Serilog only, configured for every host by `AddServiceDefaults()` (`ServiceDefaults/Logging/`): console plus OTLP export (Aspire dashboard), one request log line per request (`UseDefaultRequestLogging`), and redaction of classified, sensitively named and pattern-matched values before either sink. `LogRedactionTests` checks the serialized output of both sinks. Metrics and traces (ASP.NET Core, HttpClient, runtime, Npgsql, EF Core) are exported by ServiceDefaults too.

Rate limiting (`Web/Configurations/RateLimitingConfigurations.cs`) is a global limiter, partitioned by validated `sub` or by client address; add a named policy there for a sensitive endpoint. `X-Forwarded-For` is honored only from `ForwardedHeaders:KnownProxies` (loopback in Development). Every request has a 30-second timeout (`RequestTimeoutConfigurations.cs`, 504 problem details). Don't use FastEndpoints' `Throttle`; see `docs/content/best-practices.md`.

Postgres uses `EFCore.NamingConventions`' snake_case convention, so raw SQL must use snake_case column names, not the C# property names. List query services always read with `FromSqlRaw` (see `ListUsersQueryService`): every mapped column, input only as `{n}` parameters, and ordering, paging and projection in LINQ on top.

### Aspire orchestration (`backend/src/AppTemplate.AppHost/AppHost.cs`)

The AppHost wires up these resources for local dev only (not a production deployment mechanism):
- a containerized Postgres with a persistent data volume
- Keycloak on the fixed port 8080, with the realm imported
- Mailpit, a development SMTP catcher for Keycloak's and the API's emails
- the Web API
- the backend for frontend on the fixed port `https://localhost:7100`; the realm's redirect URIs depend on this port
- the Angular frontend, via Aspire's JavaScript app hosting (`AddJavaScriptApp` + `WithNpm`, running `npm ci`/`npm start`)

Open the app at `https://localhost:7100`. Connection strings and service URLs are wired by Aspire (`WithReference`/`WaitFor`), not hardcoded in `appsettings.json`.

### Frontend (`frontend/src/app/`)

Standalone Angular components, organized as `core/`, `shared/`, and `features/<feature>/` (`auth`, `home`, `profile`, `users`). It holds no tokens:
- `AuthService` asks `/backend-for-frontend/user` whether a session exists.
- Login, register and logout are full-page redirects (`BROWSER_REDIRECT`).
- `core/auth/auth-interceptor.ts` adds `X-CSRF: 1`.
- `authGuard` protects routes, and `permissionGuard` hides pages the user has no permission for.

Relative `api/...` URLs (`API_BASE` in `core/api/api-paths.ts`) reach the Web API through the backend for frontend. There is no `proxy.conf.ts` and no CORS.

### CI (`.github/workflows/`)

`backend-build.yml` and `frontend-build.yml` both run format-check → (frontend also lints) → build/install → test on pushes to `main`/`develop` and on PRs touching their respective directory (backend across a Windows/Linux/macOS matrix, restoring with `--locked-mode`). After changing a NuGet package version, run `dotnet restore` and commit the regenerated `packages.lock.json` files, or backend CI fails. `AppTemplate.AppHost` deliberately has no lock file. Every action is pinned to a commit SHA with a `# vX.Y.Z` comment, and Dependabot maintains them. Keep that form when adding a `uses:` line. Both build workflows run on every PR, but skip the build when their directory is untouched. Their non-matrix gate jobs (`Frontend CI`, `Backend CI`) are what the `main`/`develop` ruleset requires, together with the PR Conventions jobs. Never require a path-filtered workflow or a matrix job name directly, since a check that never reports blocks the PR forever (`.github/workflows/README.md` has the details). `pr-conventions.yml` fails a PR whose branch name or target breaks the Gitflow rules below, or that has a commit message that isn't a Conventional Commit. `secret-scan.yml` runs gitleaks (a pinned, checksum-verified CLI release, not the licensed action) over the full git history on every PR; never commit real credentials, and if it flags one, rotate it before anything else. `hugo-docs.yml` deploys `docs/` to GitHub Pages on pushes to `main`. See `.github/workflows/README.md` for details on each.

Line endings are LF throughout (`backend/.gitattributes` pins `eol=lf`, `backend/.editorconfig` sets `end_of_line = lf`) - this was deliberately fixed from an original CRLF setup that passed on Windows runners but broke csharpier's format check on Linux/macOS.

## Git workflow

### Branching: Gitflow

This project follows [Gitflow](https://www.atlassian.com/git/tutorials/comparing-workflows/gitflow-workflow):

- `main`: released, production-ready history only. Never commit to it directly.
- `develop`: the integration branch for the next release.
- `feature/<short-kebab-name>` (e.g. `feature/password-reset`): new work. Branch it from `develop` and open its PR back into `develop`.
- `release/<version>`: branched from `develop` to stabilize a release. Merge it into `main` (tagged) and back into `develop`.
- `hotfix/<short-name>`: branched from `main` for urgent production fixes. Merge it into `main` (tagged) and back into `develop`.

Never commit on `main` or `develop` directly. Start a correctly-prefixed branch first.

### Commits: Conventional Commits

Every commit message follows [Conventional Commits 1.0.0](https://www.conventionalcommits.org/en/v1.0.0/):

```
<type>[optional scope][!]: <description>

[optional body]

[optional footer(s)]
```

- Types: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `build`, `ci`, `chore`, `revert`.
- Scopes are optional and name the area touched: `backend`, `frontend`, `auth`, `apphost`, `docs`, `ci`, and so on.
- The description is imperative, lowercase, and has no trailing period.
- Mark breaking changes with `!` after the type/scope and a `BREAKING CHANGE:` footer.
- Prefer several small, focused commits, each of which builds, over one large commit.
