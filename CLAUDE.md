# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

The agent-neutral rules (layer boundaries, endpoint and use case conventions, testing, the done-means checklist) live in [`AGENTS.md`](AGENTS.md), with path-specific rules in `.github/instructions/`. Follow them; this file adds commands and a detailed architecture tour, and must not contradict them.

## What this is

A GitHub **template repository** for starting new full-stack projects: a Clean Architecture .NET backend, an Angular frontend, and .NET Aspire orchestrating both locally. It is not a sample app - the `User` feature is a deliberate end-to-end reference slice (Core → UseCases → Infrastructure → Web) to copy the shape of when adding a real feature, then delete once no longer needed as a reference.

It is the **simple** variant of [dotnet-angular-aspire-template](https://github.com/andregabrielmelo/dotnet-angular-aspire-template): it keeps what most projects use (sign-in with API-issued access and refresh tokens, permissions, email, logging, health checks, rate limiting, tests) and leaves out Keycloak and the backend for frontend, background jobs, the transactional outbox, auditing, caching, file storage, ETags and API versioning. When a task needs one of those, copy it from the full template, with its tests and ADR.

Every project, namespace, folder, the Postgres database name, and the JWT issuer/audience are currently named `AppTemplate` / `apptemplate`. When someone actually uses this template for a new project, they run `scripts/rename-template.sh YourProjectName` (a plain find-and-replace, not a `dotnet new` template engine) as their first step - keep that in mind if a task looks like it wants a "real" project name; the placeholder is intentional.

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
npm start                                          # ng serve, proxying /api to the Web API (proxy.conf.mjs)
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

### Backend: Clean Architecture, five projects under `backend/src/` (plus AppHost and ServiceDefaults)

Dependencies point inward; nothing below depends on something above it in this list:

- **`AppTemplate.SharedKernel`** - base types shared by every layer: `EntityBase`/`EntityBase<TId>`, domain event interfaces + dispatch, and the persistence exceptions use cases react to (`UniqueConstraintViolationException`, `ConcurrencyConflictException`). Kept in-repo (not a separate NuGet package) since this template is meant to be self-contained.
- **`AppTemplate.Core`** - the domain model: aggregates (`Aggregates/UserAggregate/`, `Aggregates/RefreshTokenAggregate/`), value objects, specifications, domain events (`UserRegisteredDomainEvent`), and the ports Infrastructure implements against external resources (`Core/Interfaces`, such as `IEmailSender`). Near-zero external dependencies (`Ardalis.Specification`, `Ardalis.Result`, `Vogen`).
- **`AppTemplate.UseCases`** - CQRS commands/queries dispatched via `Mediator`, with the `LoggingBehavior` pipeline behavior in `UseCases/Behaviors/` (martinothamar/Mediator, source-generated - **not** MediatR). Depends on Core only; data access goes through `IRepository<T>` (`SharedKernel/IRepository.cs`, an `Ardalis.Specification` repository) and query-service interfaces defined here, implemented in Infrastructure.
- **`AppTemplate.Infrastructure`** - EF Core + Npgsql (`Data/ApplicationDatabaseContext.cs`), email (MailKit), password hashing (`Identity/AspNetPasswordHasher`), repository/query-service implementations. Anything talking to the outside world lives here.
- **`AppTemplate.Web`** - the ASP.NET Core entry point and composition root (`Program.cs`, `Configurations/`). One [FastEndpoints](https://fast-endpoints.com/) class per endpoint under `Features/<Feature>Features/`, following the REPR pattern (Request-Endpoint-Response), with FluentValidation validators colocated in the same file as their endpoint. Endpoints map `Ardalis.Result` only through `Extensions/ResultExtensions.cs` (`ToOkResult` and siblings), and every error, including validation, 401/403, unmatched routes and exceptions, is an RFC 9457 problem details response with a `traceId` (ADR 009: the built-in `AddProblemDetails()`, plus the validation shape built in `MiddlewareConfigurations` and `ResultExtensions`).

### Authentication

The API issues its own tokens (ADR 010); there is no external identity provider:

- `POST /auth/register` and `POST /auth/login` (`Web/Features/AuthFeatures/`) check the password (`IPasswordHasher` in UseCases, ASP.NET Core's `PasswordHasher` in Infrastructure) and return a 15-minute **access token**, an HS256 JWT signed with `Jwt:SigningKey` (`Web/Authentication/JwtTokenIssuer`). Its claims are `sub` (the `UserId`), `email`, `name`, `jti`, `iat` and one `permission` claim per permission.
- They also set the **refresh token**, an HttpOnly, SameSite=Strict cookie (`apptemplate_refresh`, path `Authentication:RefreshCookiePath`, `/api/auth` as the browser sees it), stored only as a SHA-256 hash (`RefreshToken` aggregate). Every sign-in starts a token family; `POST /auth/refresh` rotates the token within its family (7 days sliding, 30 days absolute). Rotation is atomic: an `xmin` row version makes a concurrent second rotation fail, and a token rotated in the last 10 seconds answers 409 (another tab refreshed) rather than counting as reuse. Replaying an older rotated token revokes its family only. `POST /auth/logout` revokes the family.
- Refresh and logout are authenticated by the cookie, so they also require an `Origin` from `Authentication:AllowedOrigins` and the antiforgery header `X-XSRF-TOKEN` (cookie from `GET /auth/antiforgery`; `Web/Authentication/AuthCookies`). Login and register have a stricter rate limit policy (`RateLimitingConfigurations.AuthenticationPolicy`).
- Web validates access tokens with plain `AddJwtBearer` (`Configurations/AuthenticationConfigurations.cs`): issuer, audience, signing key, `HS256` only and lifetime, all explicit, `MapInboundClaims = false` (so read `sub`/`email`/`name`). FastEndpoints endpoints are authenticated unless marked `AllowAnonymous()`. `GET /users/me` returns the caller's profile by `sub`.
- `SessionFactory` (UseCases) is the one place a session is built, for login, registration and refresh alike; use cases get times from `TimeProvider`.
- No password reset, email verification, lockout or 2FA: copy Keycloak from the full template when a project needs them.

Development signs tokens with a key the AppHost generates once and keeps in its user secrets (the `jwt-signing-key` parameter); elsewhere supply that parameter (base64 of at least 32 random bytes). The seeded `admin@example.com` / `Admin123!` account has the `Admin` role (Development only). Functional tests authenticate with `TestAuthHandler` via the `X-Test-User` header (`factory.CreateAuthenticatedClient(userId, permissions...)`, `factory.CreateUserAsync()`); requests without it go through the real JWT handler, which `BrowserSession` drives with real sign-ins, cookies and antiforgery.

Authorization (ADR 011): the API's permissions (`users:read`, `users:write`, `users:delete`) are defined in `UseCases/Authorization/Permission.cs`, granted by the user's role (`User.Role`, mapped by `RolePermissions`: `Admin` has all, `Member` none) and written into the access token, so a role change reaches the user at their next refresh. Each permission is also an ASP.NET Core policy of the same name, so endpoints use `Policies(Permission.X)` for coarse checks. Resource-based rules, such as "may update own profile, or anyone's with users:write", live in use cases via `ICurrentUser` (`Id`, `Permissions`) and return `Result.Forbidden()`, which maps to 403. `GET /users/me` returns the caller's permissions for the UI (`CurrentUserService`, `permissionGuard`), but the API always enforces them itself.

### Email

`IEmailSender` (Core) is implemented by `Infrastructure/Email/MimeKitEmailSender` over MailKit, configured by `Mailserver:Hostname`/`Port`/`FromAddress` (`MailserverConfiguration`). The AppHost points it at Mailpit in Development. `UserRegisteredDomainEventHandler` sends the welcome email; read it in Mailpit's web UI. Functional tests replace it with `FakeEmailSender` (`factory.EmailSender`).

### Domain events

Entities register domain events (`RegisterDomainEvent`); `Infrastructure/Data/EventDispatchInterceptor` publishes them through Mediator right after `SaveChanges` succeeds. A use case can also dispatch them itself after saving, as registration does (`UserRegisteredDomainEvent` carries the id, which is only assigned when the row is added). They are in-process and best-effort: `MediatorDomainEventDispatcher` logs a failing handler, which never fails the request, and nothing retries it. Work that must reliably follow a database change needs a transactional outbox, which this template leaves out (see `docs/content/design-decisions.md`, Transactions and side effects).

### Tests

Three test projects under `backend/tests/`, all xUnit (see ADR 006 for the reasoning):

- **`AppTemplate.UnitTests`** - Core/UseCases in isolation, `IRepository<T>` substituted with NSubstitute. No I/O.
- **`AppTemplate.FunctionalTests`** - full HTTP → FastEndpoints → Mediator → EF Core → Postgres pipeline via `WebApplicationFactory<Program>`. `PostgresTestDatabase` starts one real Postgres container per test run (Testcontainers), and each `AppTemplateWebApplicationFactory` gets a fresh database in it, created by the real migrations - so **Docker is required** (Podman works via `DOCKER_HOST` with `TESTCONTAINERS_RYUK_DISABLED=true`). Mark every test class that uses the factory `[Trait(TestCategories.Name, TestCategories.RequiresDocker)]`: CI's Windows/macOS jobs run `--filter "Category!=RequiresDocker"`, since they can't run Linux containers.
- **`AppTemplate.ArchitectureTests`** - enforces ADR 001: allowed `ProjectReference`s per `src` project (a new project must be added to the allow-list), forbidden type dependencies per layer (NetArchTest), endpoints under `Web.Features`, no MVC controllers, handlers only in UseCases, and `<UseCase>Command`/`<UseCase>Query` naming. Run it with `dotnet test tests/AppTemplate.ArchitectureTests`.

Every route requires an authenticated user unless it calls `AllowAnonymous()`: there is an authorization fallback policy, and `EndpointAuthorizationTests` (functional) pins the list of anonymous routes (the health probes and `/auth/*`), so update it when you deliberately make an endpoint public. Tests that move the session clock (`factory.Time`, a `FakeTimeProvider`) get their own factory, as `AuthSessionLifetimeTests` does.

### Cross-cutting concerns

Strongly-typed IDs and simple domain primitives (`UserId`, `UserName`) use [Vogen](https://github.com/SteveDunn/Vogen) source-generated value objects with a `Validate` method enforcing invariants at construction. EF Core conversions for them are registered centrally in `Infrastructure/Data/Configurations/VogenEfCoreConverters.cs` - add new value objects there, not per-entity. New entity IDs also need a `HasValueGenerator<VogenIdValueGenerator<...>>()` call in that entity's `IEntityTypeConfiguration` (see `UserConfiguration.cs`).

Health probes (`ServiceDefaults/Extensions.cs`, every environment): `/alive` (process) and `/health` (readiness: Postgres, tag `ready`).

Logging: `Web/Configurations/LoggerConfigs.cs` adds Serilog for the console (levels in the `Serilog` section), and `AddServiceDefaults()` adds OpenTelemetry's logging provider, which exports the same logs to the Aspire dashboard (levels in `Logging`). Nothing is redacted, so never log request objects or DTOs: `LoggingBehavior` logs request names only, because commands carry passwords and tokens. Metrics and traces (ASP.NET Core, HttpClient, runtime, Npgsql, EF Core) are exported by ServiceDefaults too.

Rate limiting (`Web/Configurations/RateLimitingConfigurations.cs`) is a global limiter, partitioned by validated `sub` or by client address, plus the `authentication` policy (5 attempts a minute per client) on login and registration; add a named policy there for another sensitive endpoint. `X-Forwarded-For` is honored only from `ForwardedHeaders:KnownProxies` (loopback in Development, where the Angular dev server proxies the API). Don't use FastEndpoints' `Throttle`; see `docs/content/best-practices.md`. The API sets no response security headers: the host that serves the app in production does (CSP, `nosniff`, HSTS; see best-practices.md, Production deployment).

Postgres uses `EFCore.NamingConventions`' snake_case convention, so raw SQL must use snake_case column names, not the C# property names. List query services always read with `FromSqlRaw` (see `ListUsersQueryService`): every mapped column, input only as `{n}` parameters, and ordering, paging and projection in LINQ on top.

### Aspire orchestration (`backend/src/AppTemplate.AppHost/AppHost.cs`)

The AppHost wires up these resources for local dev only (not a production deployment mechanism):
- a containerized Postgres with a persistent data volume
- Mailpit, a development SMTP catcher for the API's emails (Development only)
- the Web API, with `Jwt__SigningKey` from the `jwt-signing-key` parameter and `Authentication__AllowedOrigins__0` set to the Angular endpoint
- the Angular frontend, via Aspire's JavaScript app hosting (`AddJavaScriptApp` + `WithNpm`, running `npm ci`/`npm start`), with a reference to the API

Open the app at the `angular` resource's URL in the Aspire dashboard. Connection strings and service URLs are wired by Aspire (`WithReference`/`WaitFor`), not hardcoded in `appsettings.json`. After pulling a schema change that rewrote `InitialCreate` (as removing Keycloak did), delete the Postgres data volume.

Outside the `Development` environment the AppHost doesn't start Mailpit: it expects the SMTP server as the `mailserver-hostname` and `mailserver-port` parameters, and the signing key as the `jwt-signing-key` parameter.

### Frontend (`frontend/src/app/`)

Standalone Angular components, organized as `core/`, `shared/`, and `features/<feature>/` (`auth`, `home`, `profile`, `users`). It never stores a token:
- `AuthService` keeps the access token in memory (a signal) and signs in and registers against `api/auth/*`. `ensureSession()` gets a new access token from the refresh cookie after a reload; refreshes are shared within a tab, serialized across tabs (`REFRESH_LOCK`, Web Locks) and retried once after a 409. `logout()` clears the local session even if the request fails.
- `core/auth/auth-interceptor.ts` adds `Authorization: Bearer` to API requests, and on a 401 refreshes once and retries. It leaves `api/auth/*` alone, so a failing refresh never triggers another. Angular's own XSRF support sends `X-XSRF-TOKEN`.
- `authGuard` protects routes (sending the user to `/auth?returnUrl=...`), and `permissionGuard` hides pages the user has no permission for.

Relative `api/...` URLs (`API_BASE` in `core/api/api-paths.ts`) reach the Web API through the dev server proxy (`frontend/proxy.conf.mjs`, which strips `/api` and targets `services__web__http__0`). There is no CORS: in production, serve the app and `/api` from one origin.

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
