# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

The agent-neutral rules (layer boundaries, endpoint and use case conventions, testing, the done-means checklist) live in [`AGENTS.md`](AGENTS.md), with path-specific rules in `.github/instructions/`. Follow them; this file adds commands and a detailed architecture tour, and must not contradict them.

## What this is

A GitHub **template repository** for starting new full-stack projects: a Clean Architecture .NET backend, an Angular frontend, and .NET Aspire orchestrating both locally. It is not a sample app - the `User` feature is a deliberate end-to-end reference slice (Core → UseCases → Infrastructure → Web) to copy the shape of when adding a real feature, then delete once no longer needed as a reference.

It is the **simple** variant of [dotnet-angular-aspire-template](https://github.com/andregabrielmelo/dotnet-angular-aspire-template): it keeps what most projects use (cookie sign-in with ASP.NET Core Identity, accounts with email confirmation and password reset, roles and permissions as smart enums, rate limiting, caching with HybridCache and Redis, email, logging, health checks, tests) and leaves out Keycloak and the backend for frontend, background jobs, the transactional outbox, auditing, output caching, file storage, ETags and API versioning. When a task needs one of those, copy it from the full template, with its tests and ADR.

Every project, namespace, folder, the Postgres database name, and the cookie names are currently named `AppTemplate` / `apptemplate`. When someone actually uses this template for a new project, they run `scripts/rename-template.sh YourProjectName` (a plain find-and-replace, not a `dotnet new` template engine) as their first step - keep that in mind if a task looks like it wants a "real" project name; the placeholder is intentional.

Step-by-step guides live in `docs/content/guides/`: `project-map.md` (layout, a request's path, vocabulary), `adding-a-feature.md` (a complete, tested `Project` example through every layer, including the frontend and tests), `cookbook.md` (permissions, public endpoints, email, domain events, settings, external services, schema changes) and `deploying.md` (production: reverse proxy, settings, migrations, probes, telemetry, backups). Follow them when building something.

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

`IntegrationTests/Data/MigrationDrift` fails when the model has changes no migration captures, or when the migrated schema differs from the model (column types, lengths, defaults, constraints, indexes).

Migrations apply automatically on startup only in the `Development` environment (`DatabaseConfigurations.StartDatabase`, called from `Program.cs`); elsewhere they're a no-op unless `Database:ApplyMigrationsOnStartup` is set. Seed data (`SeedData`) is only ever added in Development.

There is no pre-commit hook: run `dotnet csharpier format .` before committing (CI runs `dotnet csharpier check .`). Format again after generating code, for example EF migrations.

The build runs the recommended .NET analyzers and fails on any analyzer warning (`Directory.Build.props`: `AnalysisLevel` `latest-recommended`, `CodeAnalysisTreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, `GenerateDocumentationFile`). `.editorconfig` makes CA2200 (`throw ex;`) and IDE0005 (unused usings) errors, turns off the naming rules that fight the template's conventions (CA1707, CA1711, CA1716, CA1720, CA1725) with a comment each, and leaves `Migrations/` unanalyzed. `dotnet build` shows the findings. Fix them; suppress only in `.editorconfig` with a reason, or with a justified `[SuppressMessage]`. The compiled XML comments also feed the OpenAPI document, so changing a documented request field changes `backend/openapi/v1.json`.

The API contract is committed: after changing an endpoint, request or response, run `scripts/export-openapi.sh` (from the repo root; writes `backend/openapi/v1.json` without needing any services) and then `npm run api:generate` in `frontend/` (writes `src/app/core/api/api-types.ts`). CI fails if either file is stale.

### Frontend (Angular)

```bash
npm ci
npm start                                          # ng serve, proxying /api to the Web API (proxy.conf.mjs)
npm run build
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
- **`AppTemplate.Core`** - the domain model: aggregates (`Aggregates/UserAggregate/`, the user's profile), value objects, specifications, domain events (`UserRegisteredDomainEvent`), and the ports Infrastructure implements against external resources (`Core/Interfaces`, such as `IEmailSender`). Near-zero external dependencies (`Ardalis.Specification`, `Ardalis.Result`, `Vogen`).
- **`AppTemplate.UseCases`** - CQRS commands/queries dispatched via `Mediator`, (martinothamar/Mediator, source-generated - **not** MediatR). Depends on Core only; data access goes through `IRepository<T>` (`SharedKernel/IRepository.cs`, an `Ardalis.Specification` repository) and query-service interfaces defined here, implemented in Infrastructure. Accounts go through `Interfaces/IIdentityService`, and the request's user through `Interfaces/ICurrentUser`; domain event handlers that need those ports live here too (`<Feature>/Handlers/`); one that needs only Core lives next to its aggregate (`Core/Aggregates/<Aggregate>/Handlers/`).
- **`AppTemplate.Infrastructure`** - EF Core + Npgsql (`Data/ApplicationDatabaseContext.cs`, an `IdentityDbContext`), ASP.NET Core Identity (`Identity/`: `ApplicationUser`, `IdentityService`), caching (HybridCache and its Redis layer, registered in `InfrastructureServiceExtensions`; `Caching/CachingSettings`), email (MailKit), repository/query-service implementations. Anything talking to the outside world lives here.
- **`AppTemplate.Web`** - the ASP.NET Core entry point and composition root (`Program.cs`, `Configurations/`). One [FastEndpoints](https://fast-endpoints.com/) class per endpoint under `Endpoints/<Feature>/` (namespace `AppTemplate.Web.Endpoints.<Feature>`), following the REPR pattern (Request-Endpoint-Response), with FluentValidation validators colocated in the same file as their endpoint. Mediator's `LoggingBehavior` pipeline behavior is in `Behaviors/`. Endpoints map `Ardalis.Result` only through `Extensions/ResultExtensions.cs` (`ToOkResult` and siblings), which writes RFC 9457 problem details; request validation errors are FastEndpoints' default `ErrorResponse`, with the same `errors` field map (ADR 009).

### Authentication

Sessions are ASP.NET Core Identity's application cookie (ADR 015), and Identity also manages the accounts (ADR 013). There are no JWTs, no refresh tokens, and no external identity provider:

- **Accounts are Identity's.** `ApplicationUser : IdentityUser<int>` (Infrastructure) holds the email (also its user name), password hash, lockout, security stamp and roles, and points at the domain `User` profile (`ProfileId`, unique, cascade delete). The profile's `UserId` is what the rest of the app uses. Identity is registered with `AddIdentity` (`InfrastructureServiceExtensions`), which makes its cookie the default scheme. UseCases reach it through `UseCases/Interfaces/IIdentityService` (`IdentityService`, registered only behind the interface): create a user, add it to roles, check a password, sign in and out, get a user's account, send and use confirmation and reset codes, change a password. Web never references `Microsoft.AspNetCore.Identity` (`LayerDependencyTests`).
- **The cookie:** Identity's application cookie with its default name (`.AspNetCore.Identity.Application`) and lifetime (14 days, sliding), HttpOnly; `ConfigureApplicationCookie` adds SameSite=Strict and always Secure. It is protected with the Data Protection keys in Postgres (`data_protection_keys`), so sessions survive restarts. Its `OnRedirectToLogin`/`OnRedirectToAccessDenied` events answer 401/403 instead of redirecting.
- **Claims:** `ApplicationClaimsPrincipalFactory` (Identity's claims factory) adds `user_id` (the profile's `UserId`) and one `permissions` claim per permission of the user's roles to Identity's own (account id, email, stamp, `role`). The names are `UseCases/Authorization/AppClaimTypes`. `ICurrentUser` (`Web/Services/CurrentUser`) reads them.
- `POST /auth/login` (`Web/Endpoints/Authentication/`) checks the password with lockout (5 failures, 5 minutes): bad credentials and lockout are the same 401; the right password on an unconfirmed email is 403. Then `SignInAsync` sets the cookie (204). `POST /auth/logout` (authenticated) deletes it (`SignOutAsync`); a copied cookie stays valid until it expires or the stamp changes.
- `POST /auth/register` creates the profile and its account in one `SaveChanges` (`IIdentityService.CreateUserAsync`, through the account's `Profile` navigation), then adds the account to `Member` (`AddToRolesAsync`), and returns 201 without signing in; `SendConfirmationOnUserRegistered` (UseCases) emails a confirmation link, which doubles as the welcome. Sign-in requires a confirmed email (`RequireConfirmedEmail`). Passwords: 8 to 128 characters, no composition rules (`UseCases/Constants/PasswordConstants`).
- Account flows (ADR 013), all `POST /auth/...`: `confirm-email`, `resend-confirmation` (always 202), `forgot-password` (always 202) and `reset-password` are anonymous and use the Base64Url codes in the emailed links (`AccountEmailSender`, Identity's `IEmailSender<TUser>`; links open `AccountLinks:ClientUrl`, validated at startup). `change-password` is authenticated, returns 204 and reissues this session's cookie (`RefreshSignInAsync`). Confirmation and reset links last a day (Identity's default token provider). Identity's own failures (a taken email, a wrong current password or reset code, a password Identity rejects) become `Result.Error`, a generic 500 (`IdentityResultExtensions.ToApplicationResult`); request validation still answers 400 first.
- **Ending sessions:** Identity's `SecurityStampValidator` checks each cookie's security stamp every 30 minutes (its default `ValidationInterval`) and rebuilds its claims. A password change or reset ends the other sessions within that interval; a role change reaches them within it.
- **CSRF: antiforgery tokens** (`Web/Configurations/AntiforgeryConfigurations.cs`). Every unsafe request (not GET/HEAD/OPTIONS/TRACE) from a signed-in session needs an ASP.NET Core antiforgery token in `X-XSRF-TOKEN`, or gets a 400 problem (`UseSessionAntiforgery`, after authentication). `GET /auth/antiforgery-token` (anonymous) sets the HttpOnly `apptemplate_antiforgery` cookie and the readable `XSRF-TOKEN` cookie, which Angular's HttpClient echoes by itself. Tokens are bound to the user, so the SPA fetches one at startup and after signing in. Anonymous requests aren't checked. The cookie being SameSite=Strict and the API having no CORS are the other two layers.
- FastEndpoints endpoints are authenticated unless marked `AllowAnonymous()`; there is no authorization fallback policy. FastEndpoints 8.3 maps `GET /_test_url_cache_` (every route and endpoint type) outside its pipeline, so it is anonymous until FastEndpoints 8.4+ (ADR 012). `GET /users/me` returns the caller's profile by `user_id`.
- No 2FA, external sign-in or email change. Identity supports them; see ADR 013.

The seeded `admin@example.com` / `Admin123!` account has the `Admin` role (Development only; seed accounts are confirmed with `MarkEmailConfirmedAsync`, which sends nothing). Functional tests sign in for real: `TestSession` is a cookie jar on `https://localhost` that drives login (which then fetches an antiforgery token), register, change password and logout, and sends `X-XSRF-TOKEN` on unsafe requests as Angular does; `factory.CreateUserAsync(role)` creates a confirmed account (a `Role`, `Member` by default), `factory.CreateSignedInSessionAsync(role)` also signs it in, and `factory.EmailSender.LastLinkQueryTo(email)` follows an emailed link.

Authorization (ADR 011): permissions and roles are [Ardalis.SmartEnum](https://github.com/ardalis/SmartEnum)s in `UseCases/Authorization`. `Permission` (`UsersRead`, `UsersWrite`, `UsersDelete`; the `Value` is the claim, such as `users:read`, and converts to a string implicitly, but write `.Value` in text) and `Role` (`Admin` with every permission, `Member` with none; the `Name` is the Identity role name, the `Value` its seeded id). The account's Identity roles (seeded from `Role.List` by `IdentityRoleConfiguration`) map to permissions through `Role.PermissionsFor`, which the claims factory writes into the session, so a role change reaches the user at the next stamp check. Endpoints use FastEndpoints' `Permissions(Permission.X)` for coarse checks. Resource-based rules, such as "may update own profile, or anyone's with users:write", live in use cases via `ICurrentUser` (`Id`, `Permissions`) and return `Result.Forbidden()`, which maps to 403. `GET /users/me` returns the caller's permissions for the UI (`CurrentUserService`, `permissionGuard`), but the API always enforces them itself.

### Email

`IEmailSender` (Core) is implemented by `Infrastructure/Email/MimeKitEmailSender` over MailKit, configured by `Mailserver:Hostname`/`Port`/`FromAddress` (`MailserverConfiguration`). The AppHost points it at Mailpit in Development. `SendConfirmationOnUserRegistered` sends the confirmation email, and `IdentityService` the reset email (both through `AccountEmailSender`); read them in Mailpit's web UI. Never log the links or codes. Functional tests replace it with `FakeEmailSender` (`factory.EmailSender`).

### Domain events

Entities register domain events (`RegisterDomainEvent`); `Infrastructure/Data/EventDispatchInterceptor` publishes them through Mediator right after `SaveChanges` succeeds. A use case can also publish one itself after saving, as registration does with `IMediator.Publish` (`UserRegisteredDomainEvent` carries the id, which is only assigned when the row is added); a failing handler then fails that request. Events dispatched after `SaveChanges` are in-process and best-effort: `MediatorDomainEventDispatcher` logs a failing handler, which never fails the request, and nothing retries it. Work that must reliably follow a database change needs a transactional outbox, which this template leaves out (see `docs/content/design-decisions.md`, Transactions and side effects).

### Tests

Five test projects under `backend/tests/`, following ardalis/CleanArchitecture's conventions: xUnit v3, Shouldly, NSubstitute, one class per member or behavior named `<Type><Member>` (`UserCreate`, `LoginHandlerHandle`, `AuthLoginPost`), and `TestContext.Current.CancellationToken` (see ADR 006 for the reasoning):

- **`AppTemplate.UnitTests`** - Core/UseCases in isolation, `IRepository<T>` substituted with NSubstitute. No I/O.
- **`AppTemplate.IntegrationTests`** - Infrastructure against a real Postgres: EF repositories, `IdentityService` (`Identity/`: account creation, lockout, codes), `ListUsersQueryService` and `Data/MigrationDrift`. `BaseEfRepoTestFixture` gives each class a fresh database built by the real migrations.
- **`AppTemplate.FunctionalTests`** - full HTTP → FastEndpoints → Mediator → EF Core → Postgres pipeline via `CustomWebApplicationFactory<Program>`, one class per route and verb in `ApiEndpoints/` (`UserGetById`, `AuthLoginPost`), with `Ardalis.HttpClientTestExtensions`. FastEndpoints' static state means test classes run one at a time (`xunit.runner.json`).
- **`AppTemplate.ArchitectureTests`** - enforces ADR 001: allowed `ProjectReference`s per `src` project (a new project must be added to the allow-list), forbidden type dependencies per layer (NetArchTest), endpoints in a feature namespace under `AppTemplate.Web`, no MVC controllers, handlers only in UseCases, and `<UseCase>Command`/`<UseCase>Query` naming. Run it with `dotnet test tests/AppTemplate.ArchitectureTests`.
- **`AppTemplate.AspireTests`** - one smoke test (`AppHostStartup`) that starts the real AppHost with Aspire.Hosting.Testing and waits for the `web` resource to be healthy, then calls `/alive` and `/health`: it catches broken resource references, settings, service discovery and health checks. Containers get a session lifetime and no volumes there, so it never touches a developer's persistent Postgres and Redis data. Needs Docker (Podman: also `ASPIRE_CONTAINER_RUNTIME=podman`) and a trusted development certificate (CI's Linux job runs `dotnet dev-certs https --trust`); about a minute. Like the AppHost, it has no lock file.

`tests/Shared/PostgresTestDatabase.cs` (compiled into the integration and functional projects) starts one real Postgres container per test run (Testcontainers), so **Docker is required** for those two (Podman works via `DOCKER_HOST` with `TESTCONTAINERS_RYUK_DISABLED=true`). Mark every test class that needs it `[Trait(TestCategories.Name, TestCategories.RequiresDocker)]`: CI's Windows/macOS jobs run `--filter "Category!=RequiresDocker"`, since they can't run Linux containers.

`EndpointAuthorization` (functional) pins the list of anonymous routes (login, register, `GET /auth/antiforgery-token` and the account email endpoints), so update it when you deliberately make an endpoint public. Tests that move the clock (`factory.Time`, a `FakeTimeProvider`, which the cookie and the stamp validator use) get their own factory, as `SessionStampValidation` does.

### Cross-cutting concerns

Strongly-typed IDs and simple domain primitives (`UserId`, `UserName`) use [Vogen](https://github.com/SteveDunn/Vogen) source-generated value objects with a `Validate` method enforcing invariants at construction. EF Core conversions for them are registered centrally in `Infrastructure/Data/Configurations/VogenEfCoreConverters.cs` - add new value objects there, not per-entity. Entity IDs are database identities: `ValueGeneratedOnAdd().HasVogenConversion()` in the entity's `IEntityTypeConfiguration` (see `UserConfiguration.cs`), and the ID's `[ValueObject<int>]` uses `deserializationStrictness: DeserializationStrictness.AllowAnything`, because EF's temporary keys are negative.

Caching (ADR 017): queries opt in by implementing `ICacheable` (`UseCases/Caching`: a `CacheKey` and `CacheTags`), and `CachingBehavior` (`Web/Behaviors`, after `LoggingBehavior`) caches their successful results in HybridCache; failures (such as `NotFound`) are never cached. `GetUserQuery` (`GetUserQuery-{id}`), `GetCurrentUserQuery` (`GetCurrentUserQuery-{id}`) and `ListUsersQuery` (`ListUsersQuery-{page}-{perPage}`, defaults filled in) do; their handlers have no cache code. A key is `nameof` the query plus everything its result depends on. HybridCache is used directly, no port of our own. Entries are tagged per user (`CacheTags.User(id)`, `users:{id}`) and list pages with `CacheTags.UserList` (`users:list`); update and delete call `HybridCache.RemoveByTagAsync([CacheTags.User(user.Id), CacheTags.UserList])` after saving, and registration drops `CacheTags.UserList` after publishing. Test helpers that create users outside a use case (`factory.CreateUserAsync`) drop the list tag too. On a hit the handler doesn't run, so never mark a query cacheable whose handler authorizes per caller, and never cache what depends on the caller under a key without it. Whole `Result<T>`s are cached, so results must round-trip through System.Text.Json (`CachedResultRoundTrip`). Setup is in Infrastructure (`AddInfrastructureServices`, `InfrastructureServiceExtensions.RedisConnectionName`): L1 in-process, L2 Redis when Aspire provides the `cache` connection (otherwise L1 only), lifetimes by cache profile: a query overrides `ICacheable.CacheProfile` (a smart enum: `None` by default, `Short`, `Long`), and the `Caching` settings hold each profile's lifetimes (`None`: `Expiration` 10 minutes / `LocalExpiration` 1 minute; others under `Caching:Profiles:{Name}`, `Short` 1 minute / 15 seconds, `Long` 1 hour / 5 minutes; `CachingSettings`, every profile validated at startup). A new profile is an enum value plus its defaults in `CachingSettings.Profiles`. Redis is optional for reads: HybridCache logs and skips a failing L2 (falling back to L1 and the handler), the client fails fast while disconnected (`BacklogPolicy.FailFast`) and reconnects by itself, and Aspire's Redis health check is off, so Redis never fails `/health`. Writes are not: with Redis down, `RemoveByTagAsync` throws, so a registration, update or delete saves and then answers 500 (a deliberate exception to failing open, ADR 017). `Caching/UserCaching` and `Caching/RedisOutage` (functional, the latter with a Testcontainers Redis) cover hits, invalidation (by id, as the current user, and the list) and an outage (reads fast, writes saved but 500). Tests that expect a cache hit right after a removal advance `factory.Time` first: HybridCache reads that frozen clock, and an entry stored at the instant of its tag's removal counts as removed.

Health probes (`ServiceDefaults/Extensions.cs`, `MapDefaultEndpoints`, Development only, as Aspire's template does): `/alive` (checks tagged `live`) and `/health` (every check, including Postgres). Map them elsewhere deliberately; see `docs/content/best-practices.md`.

Logging: `Web/Configurations/LoggerConfigs.cs` adds Serilog for the console (levels in the `Serilog` section), and `AddServiceDefaults()` adds OpenTelemetry's logging provider, which exports the same logs to the Aspire dashboard (levels in `Logging`). Nothing is redacted, so never log request objects or DTOs: `LoggingBehavior` logs request names only, because commands carry passwords and tokens. Metrics and traces (ASP.NET Core, HttpClient, runtime, Npgsql, EF Core) are exported by ServiceDefaults too.

Rate limiting (ADR 014, `Configurations/RateLimitingConfigurations.cs`, `RateLimiting` settings) is ASP.NET Core's limiter, never FastEndpoints' `Throttle` (which trusts a client header): a global fixed-window limiter partitioned by the session's `user_id` (`user:`, 600/min) or the client address (`ip:`, 120/min), and the named `authentication` policy (`RateLimitingConfigurations.AuthenticationPolicy`, 5/min per address) on login, register and the anonymous account endpoints, applied with `Options(x => x.RequireRateLimiting(...))`. Give any new anonymous, email-sending or expensive endpoint that policy or one of its own. A rejection is a 429 problem with `Retry-After`. The client address comes from `X-Forwarded-For` only when the peer is in `ForwardedHeaders:KnownProxies` (the API lists loopback in development, `ForwardedHeaders:KnownProxies` in `AppTemplate.Web/appsettings.Development.json`, for the Angular proxy); with none listed, nothing is forwarded. `UseRateLimiter` runs after `UseAuthentication`.

The API sets no response security headers of its own: the host that serves the app and `/api` in production does (CSP, `nosniff`, HSTS; best-practices.md, Production deployment). The Angular dev server sends the SPA's CSP in development (`angular.json`).

Postgres uses `EFCore.NamingConventions`' snake_case convention, so raw SQL must use snake_case column names, not the C# property names. List query services always read with `FromSqlRaw` (see `ListUsersQueryService`): every mapped column, input only as `{n}` parameters, and ordering, paging and projection in LINQ on top.

### Aspire orchestration (`backend/src/AppTemplate.AppHost/AppHost.cs`)

The AppHost wires up these resources for local dev only (not a production deployment mechanism):
- a containerized Postgres with a persistent data volume
- Redis (`cache`), HybridCache's distributed layer: a persistent container with a data volume, plus Redis Insight to browse it (ADR 017)
- Mailpit, a development SMTP catcher for the API's emails (Development only)
- the Web API, HTTPS only (its `https` launch profile, `https://localhost:7243`), with `AccountLinks__ClientUrl` set to the Angular `https` endpoint
- the Angular frontend, via Aspire's JavaScript app hosting (`AddJavaScriptApp` + `WithNpm`, running `npm ci`/`npm start`), with a reference to the API. It serves HTTPS with the ASP.NET Core development certificate: `WithHttpsDeveloperCertificate` + `WithHttpsCertificateConfiguration` pass `--ssl --ssl-cert --ssl-key` to `ng serve`, and `WithDeveloperCertificateTrust(true)` lets the proxy verify the API's certificate (ADR 016; experimental, `ASPIRECERTIFICATES001`)

Every request is HTTPS, development included (ADR 016). Each developer trusts the development certificate once: `dotnet dev-certs https --trust` (in WSL it also trusts it in Windows), or `aspire certs trust`. The AppHost has only its `https` profile. Cookies are always `Secure`.

Open the app at the `angular` resource's URL in the Aspire dashboard. Connection strings and service URLs are wired by Aspire (`WithReference`/`WaitFor`), not hardcoded in `appsettings.json`. The AppHost passes the API only what depends on the composition, each explicitly: connection strings (`WithReference`), `AccountLinks__ClientUrl` (the Angular endpoint) and Mailpit's `Mailserver__Hostname`/`Port` in development (`WithEnvironment`), and outside Development the deployment's values as parameters (`mailserver-hostname`, `mailserver-port`, `mailserver-from-address`; `AddParameter`). Never forward a whole configuration section. The API owns everything else: `appsettings.json` holds log levels, and `appsettings.Development.json` its development defaults (`Mailserver:FromAddress`, loopback in `ForwardedHeaders:KnownProxies`), which also apply when the API runs without the AppHost. Outside Aspire, set the rest as environment variables. Infrastructure registers the context with Aspire's EF client integration (`EnrichNpgsqlDbContext`), which turns on EF's retrying execution strategy: code that opens its own transaction must run it inside `Database.CreateExecutionStrategy().ExecuteAsync(...)`; a single `SaveChanges` needs nothing, since EF wraps it in a transaction itself. After pulling a schema change that rewrote `InitialCreate` (as removing Keycloak did), delete the Postgres data volume.

Outside the `Development` environment the AppHost doesn't start Mailpit: it expects the SMTP server as the `mailserver-hostname` and `mailserver-port` parameters. The API's health probes aren't mapped, so `WithHttpHealthCheck("/health")` never turns healthy there.

### Frontend (`frontend/src/app/`)

Standalone Angular components, organized as `core/`, `shared/`, and `features/<feature>/` (`auth`, `home`, `profile`, `users`):
- `AuthService` (`features/auth/auth-service.ts`) holds no token: the browser keeps the HttpOnly session cookie and sends it with every same-origin request, in every tab. Whether the user is signed in is whether `CurrentUserService` has loaded a profile (`GET api/users/me`; `ensureSession()`). `login()` posts, then fetches a new antiforgery token (`fetchAntiforgeryToken()`, also run at startup by `provideAppInitializer` in `app.config.ts`); `logout()` posts `api/auth/logout` and forgets the user even if the request fails. `withXsrfConfiguration` makes HttpClient send the `XSRF-TOKEN` cookie as `X-XSRF-TOKEN` on unsafe same-origin requests.
- `core/auth/auth-interceptor.ts` adds nothing: on a 401 from the API while signed in (the session expired or was ended elsewhere) it forgets the user and sends them to `/auth/login?returnUrl=...`. It leaves `api/auth/*` alone.
- `authGuard` protects routes (sending the user to `/auth/login?returnUrl=...`), and `permissionGuard` hides pages the user has no permission for.

Relative `api/...` URLs (`API_BASE` in `core/api/api-paths.ts`) reach the Web API through the dev server proxy (`frontend/proxy.conf.mjs`, which strips `/api` and targets `services__web__https__0`, verifying its certificate). There is no CORS: in production, serve the app and `/api` from one origin. `docs/content/guides/frontend.md` covers this and how pages show the API's errors (status to message, `errors` by field on 400, 401 left to the interceptor).

### CI (`.github/workflows/`)

`backend-build.yml` and `frontend-build.yml` both run format-check → (frontend also lints) → build/install → test on pushes to `main`/`develop` and on PRs touching their respective directory (backend across a Windows/Linux/macOS matrix, restoring with `--locked-mode`). After changing a NuGet package version, run `dotnet restore` and commit the regenerated `packages.lock.json` files, or backend CI fails. `AppTemplate.AppHost` deliberately has no lock file. Every action is pinned to a commit SHA with a `# vX.Y.Z` comment. Dependabot opens weekly, grouped PRs into `develop` for NuGet, npm, the actions and the .NET SDK, which `global.json` (repository root) pins for every `dotnet` command and for CI. Keep that form when adding a `uses:` line. Both build workflows run on every PR, but skip the build when their directory is untouched. Their non-matrix gate jobs (`Frontend CI`, `Backend CI`) are what the `main`/`develop` ruleset requires, together with the PR Conventions jobs. Never require a path-filtered workflow or a matrix job name directly, since a check that never reports blocks the PR forever (`.github/workflows/README.md` has the details). `pr-conventions.yml` fails a PR whose branch name or target breaks the Gitflow rules below, or that has a commit message that isn't a Conventional Commit. `secret-scan.yml` runs gitleaks (a pinned, checksum-verified CLI release, not the licensed action) over the full git history on every PR; never commit real credentials, and if it flags one, rotate it before anything else. `hugo-docs.yml` deploys `docs/` to GitHub Pages on pushes to `main`. See `.github/workflows/README.md` for details on each.

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
