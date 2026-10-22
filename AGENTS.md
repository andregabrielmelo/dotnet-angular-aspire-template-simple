# Coding agent instructions

The canonical rules for any coding agent (Claude Code, GitHub Copilot, Cursor, Codex and so on) working in this repository. Agent-specific files such as `CLAUDE.md` add tool-specific detail and point here. They must not contradict this file.

Commands, project layout and the full architecture overview are in [`CLAUDE.md`](CLAUDE.md). The reasons behind the design are in [`docs/content/design-decisions.md`](docs/content/design-decisions.md) and the [ADRs](docs/content/architecture-decisions/). Path-specific rules are in [`.github/instructions/`](.github/instructions/).

## 1. Before changing anything

- Read the ADR that covers the area you are touching before introducing a library, a pattern or a new abstraction. If your change contradicts one, stop and say so. Don't silently invent a competing pattern.
- When the ADRs don't settle a structural question, check the sources in [Further reading](docs/content/further-reading.md) (the two templates this project builds on and the blogs it leans on) before inventing an answer, and say which one you followed.
- For *how* to build things, follow the guides in [`docs/content/guides/`](docs/content/guides/): the [project map](docs/content/guides/project-map.md) (layout, a request's path, vocabulary), [adding a feature](docs/content/guides/adding-a-feature.md) (a complete worked example through every layer) and [common tasks](docs/content/guides/cookbook.md). This file holds the rules they follow.
- Search before assuming something is missing. The template already has authentication, permissions, email and Postgres-backed tests.
- This is the simple variant of [dotnet-angular-aspire-template](https://github.com/andregabrielmelo/dotnet-angular-aspire-template). When a task needs background jobs, a transactional outbox, auditing, caching, file storage, ETags or API versioning, copy the shape from there (code, tests and ADR) instead of designing it from scratch.
- Copy the shape of the `User` feature: `Core/Aggregates/UserAggregate`, then `UseCases/Users/<UseCase>/`, then `Web/Features/UserFeatures/`. It is the executable reference for every layer.
- Look at the related tests, DI registrations (`Web/Configurations/`, `Infrastructure/InfrastructureServiceExtensions.cs`) and configuration before creating something new.
- Keep changes small and focused. Don't refactor unrelated code while implementing a feature.

## 2. Architecture boundaries ([ADR 001](docs/content/architecture-decisions/adr-001-clean-architecture-layering.md))

Dependencies point inward:

| Project | Contains | May depend on |
|---|---|---|
| `AppTemplate.SharedKernel` | Stable base types: `EntityBase`, domain event interfaces and dispatch, `IRepository<T>` | nothing in the solution |
| `AppTemplate.Core` | Aggregates, Vogen value objects, domain events, specifications; ports to external resources in `Core/Interfaces` (`IEmailSender`, ...) | SharedKernel |
| `AppTemplate.UseCases` | Mediator commands, queries and handlers; pipeline behaviors (`UseCases/Behaviors`); list query-service interfaces (`IListUsersQueryService`) and request context (`ICurrentUser`) | Core, SharedKernel |
| `AppTemplate.Infrastructure` | EF Core and Npgsql, repository and query-service implementations, MailKit | Core, UseCases, SharedKernel |
| `AppTemplate.Web` | FastEndpoints endpoints, request validation, HTTP mapping, middleware, composition root | everything above |
| `AppTemplate.BackendForFrontend` | OpenID Connect session, YARP proxy to Web | ServiceDefaults only |

- Core never references UseCases, Infrastructure, Web, EF Core or ASP.NET Core.
- UseCases never references Infrastructure, Web or EF Core. Data access goes through `IRepository<T>` or a query-service interface defined in UseCases.
- A dependency on an external resource (SMTP, an HTTP API, or our own Postgres beyond `IRepository<T>`) is an Infrastructure class behind an interface in `Core/Interfaces`; the doc comment names the implementation and its consumers. List query services, which return use-case DTOs, and `ICurrentUser`, which Web implements from the request, stay in UseCases.
- Don't move a class to another layer to avoid writing the right abstraction.
- Don't add projects, base classes, generic frameworks or DDD patterns without a concrete use and a clear benefit.
- Before adding an interface, specification, domain event or pipeline behavior, check [When To Abstract](docs/content/design-decisions.md#when-to-abstract). Optional capabilities stay opt-in ([Supported vs Required](docs/content/design-decisions.md#supported-vs-required)).

## 3. API endpoints ([ADR 003](docs/content/architecture-decisions/adr-003-fastendpoints-mediator.md))

- FastEndpoints with the REPR pattern only. No MVC controllers and no Minimal API handlers for feature endpoints.
- One endpoint class per HTTP operation, under `Web/Features/<Feature>Features/`. The request DTO, the FluentValidation `Validator<T>` and the `Mapper` live in the same file as the endpoint (see `GetByIdEndpoint.cs`).
- Endpoints are thin: translate the request into a command or query, `await mediator.Send(..., cancellationToken)`, and map the `Result` with `Web/Extensions/ResultExtensions.cs` (`ToOkResult`, `ToCreatedResult`, `ToNoContentResult`, `ToAcceptedResult`). The return type is `Results<Ok<T>, ProblemHttpResult>` (or `Created`, `NoContent`, `Accepted`). No business logic, EF Core or external SDKs in endpoints.
- Every error is an RFC 9457 problem details response with a `traceId` ([ADR 009](docs/content/architecture-decisions/adr-009-problem-details-error-contract.md)). Never build an error response by hand or `switch` on `ResultStatus` in an endpoint.
- Request and response types are dedicated DTOs. Never expose an entity, aggregate or value object.
- Every endpoint is authenticated by default, and a fallback policy covers routes that declare nothing. Declare either `AllowAnonymous()` (deliberately) or `Policies(Permission.X)` explicitly ([ADR 008](docs/content/architecture-decisions/adr-008-permission-based-authorization.md)). A new anonymous route must be added to `EndpointAuthorizationTests`.
- Never put Data Annotations attributes (`[Required]` and so on) on request DTOs. FastEndpoints ignores them; use the validator.
- Declare `Summary`, `Tags` and `Description(... .Produces...)` so the OpenAPI document stays accurate.
- A sensitive or expensive operation gets a named rate-limit policy, added in `RateLimitingConfigurations` and applied with `Options(x => x.RequireRateLimiting(name))`. Don't use FastEndpoints' `Throttle(...)`: it keys on the raw `X-Forwarded-For` header, which any client can set, and its 429 isn't a problem details response.
- An endpoint that waits on an external service longer than the default 30-second request timeout gets a named timeout policy in `RequestTimeoutConfigurations`, applied with `Options(x => x.WithRequestTimeout(name))`.

## 4. Use cases

- Use [Mediator](https://github.com/martinothamar/Mediator) (source-generated). Never MediatR, and never a second dispatch mechanism.
- One folder per use case: `UseCases/<Feature>/<UseCase>/`. The command or query record has its own file (`UpdateUserCommand.cs`) next to its handler (`UpdateUserHandler.cs`).
- Handlers return `Ardalis.Result`. Use `Result.NotFound()`, `Result.Forbidden()`, `Result.Conflict()` or `Result.Invalid(...)` for expected outcomes; their messages reach the caller. `Result.Error()` and `Result.Unavailable()` become a generic 500 or 503, so log the details in the handler. Throw only for unexpected failures.
- Resource-based authorization ("may update their own profile, or anyone's with `users:write`") lives in the handler, through `ICurrentUser`, and returns `Result.Forbidden()`.
- **Repository or query service?**
  - Use `IRepository<T>` with an `Ardalis.Specification` for aggregate-oriented writes: load, change through domain methods, save.
  - Use a query-service interface defined in UseCases and implemented in Infrastructure for read models, projections, joins, pagination and hand-written SQL (`IListUsersQueryService`).
  - Lookups that are not followed by a mutation call `.AsNoTracking()` in their specification.
  - Don't add a repository method for every query, and don't load whole aggregates for read-only endpoints.
  - The full decision table is in [When To Abstract](docs/content/design-decisions.md#when-to-abstract).

## 5. Domain model ([ADR 005](docs/content/architecture-decisions/adr-005-vogen-strongly-typed-ids.md))

- Domain logic is independent of HTTP, EF Core, Angular and providers.
- Aggregates protect their own invariants. No public setters that bypass them.
- IDs and primitives with a real invariant are Vogen value objects with a `Validate` method. Register EF conversions centrally in `Infrastructure/Data/Configurations/VogenEfCoreConverters.cs`. New entity IDs also need `HasValueGenerator<VogenIdValueGenerator<...>>()` in their `IEntityTypeConfiguration`.
- Plain DTOs and projections (`UserDto`, `UserRecord`) stay plain. Don't wrap everything.
- Domain events are for meaningful domain occurrences (`UserProvisioned`). They are dispatched in-process after `SaveChanges` succeeds (`EventDispatchInterceptor`); a failing handler is logged and never fails the request. They are not a reliable delivery mechanism: work that must happen needs a transactional outbox (see [Transactions and side effects](docs/content/design-decisions.md#transactions-and-side-effects)).

## 6. Persistence ([ADR 004](docs/content/architecture-decisions/adr-004-postgresql.md))

- EF Core, Npgsql, SQL and provider configuration stay in Infrastructure.
- Postgres uses snake_case names (`EFCore.NamingConventions`). Raw SQL must use snake_case columns, and must be parameterized. Never interpolate input into SQL.
- Async I/O everywhere, passing the request's `CancellationToken`. Avoid N+1 queries, unbounded result sets and unnecessary materialization. List endpoints page with a stable order.
- Schema changes need a migration (`dotnet ef migrations add ... -o Data/Migrations`, see CLAUDE.md). Review the generated code, then run `dotnet csharpier format .`. A model change without a migration fails CI (`MigrationDriftTests`), and so does a migration whose schema doesn't match the model. Adding a non-null column to an existing table makes EF add a `defaultValue` to fill the existing rows: drop that default in the same migration unless the model declares it.

## 7. Configuration and dependency injection

- Register options before using them: `AddOptions<T>().Bind(...)` with explicit validation rules and `ValidateOnStart()`. `ValidateOnStart` alone makes nothing required; the rules do.
- Never invent configuration at the call site: no `GetSection(...).Get<T>() ?? new T()`, no `configuration["Key"] ?? "literal"`. Read options through `IOptions<T>` when the service or framework options are built (`AddOptions<TFramework>().Configure<IOptions<T>>(...)`, DI factories), not at registration. A required plain value uses `GetRequiredValue` (ServiceDefaults), read at registration so a missing value fails startup, not the first request.
- Three kinds of settings: **required** (validated as present), **optional feature** (empty means off; when on, every setting is validated so a partial configuration fails), and **safe defaults** (defaults live in the options class, never in `appsettings*.json`, which keeps only values we set).
- A new options type joins the explicit list in its composition root's tests (`OptionsRegistrationTests` for Web). Why: `docs/content/best-practices.md`, Options.
- Never hardcode secrets, credentials or environment-specific URLs. Local services are wired by Aspire (`WithReference`/`WaitFor` in `AppHost.cs`); secrets are AppHost parameters or user secrets.
- Never inject a scoped service into a singleton.
- Document any new configuration key, environment variable or AppHost parameter.
- A configuration file for a third-party service we run lists every option of the pinned version: options in effect are set explicitly, even to their default, and unused ones stay commented out with their default and when to use them. Re-check the file whenever the version is bumped. Why: `docs/content/best-practices.md`, Third-party configuration files.

## 8. Health checks

- Postgres gates readiness (`/health`, tag `ready`). A new required dependency joins it; an optional one must not gate readiness, and the code that uses it fails open. Never tag a dependency `live`. Policy: `docs/content/best-practices.md`, Health checks.

## 9. Testing ([ADR 006](docs/content/architecture-decisions/adr-006-testing-strategy.md))

- **Unit tests** (`AppTemplate.UnitTests`) cover domain rules and handler behavior with NSubstitute and no I/O.
- **Functional tests** (`AppTemplate.FunctionalTests`) cover HTTP contracts, validation, authorization, status codes and serialization through `AppTemplateWebApplicationFactory`, against a real Postgres (Testcontainers):
  - Mark every class that uses the factory with `[Trait(TestCategories.Name, TestCategories.RequiresDocker)]`.
  - Authenticate with `factory.CreateAuthenticatedClient(sub, Permission.X, ...)`.
  - Emails go to `factory.EmailSender`, a fake that records them.
- **Backend for frontend tests** (`AppTemplate.BackendForFrontend.Tests`) cover session endpoints and proxy rules.
- **Architecture tests** (`AppTemplate.ArchitectureTests`) enforce section 2 and the placement rules in sections 3 and 4. If one fails, fix the code, not the rule. Only change a rule together with the ADR that justifies it.
- Tests follow Arrange-Act-Assert, one behavior per test. Change tests whenever observable behavior changes.

## 10. Frontend (`frontend/`)

See [`.github/instructions/frontend.instructions.md`](.github/instructions/frontend.instructions.md). In short:
- Standalone components in `core/`, `shared/` and `features/<feature>/`.
- HTTP calls live in feature services, not components.
- Guards are UX, not authorization.
- The SPA runs under a strict CSP: no inline scripts, inline event handlers or `eval`. New external origins go into the CSP directive, never `'unsafe-inline'` scripts (see `docs/content/best-practices.md`, Security headers).
- The browser never holds tokens ([ADR 007](docs/content/architecture-decisions/adr-007-authentication-backend-for-frontend-keycloak.md)).

## 11. Code style

- C#: `backend/.editorconfig`, CSharpier, central package versions in `backend/Directory.Packages.props`, and LF line endings.
- After changing a package version, run `dotnet restore` and commit the regenerated `packages.lock.json` files.
- Inject `ILogger<T>` and use message templates (`"{UserId} created"`), never string interpolation: interpolated text bypasses redaction. Never log tokens, passwords or full request bodies.
- Mark personal data and secrets with `[PersonalData]`/`[SecretData]` (SharedKernel) on the type or property (`[property: PersonalData]` on a record parameter), and log objects with `{@Object}`. The logging pipeline redacts classified values in both the console and the OpenTelemetry export. Never log a DTO's `ToString()` (`{Response}` without `@`): it prints every property and nothing gets redacted. See `docs/content/best-practices.md`, Logging.
- Comments explain *why*, not *what*. Don't add a dependency when the framework or an existing library already solves the problem.

## 12. Git workflow

- Gitflow: `feature/<kebab-name>` from `develop` and back into `develop`. Never commit to `main` or `develop` directly.
- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/): `<type>(<scope>)!: <imperative description>`. Prefer several small commits that each build.
- `pr-conventions.yml` enforces both rules on every PR.

## 13. Done means

Before calling a change finished, run what applies and report the actual results. Never claim a check passed without running it.

```bash
# backend/
dotnet csharpier check .
dotnet build AppTemplate.slnx
dotnet test AppTemplate.slnx   # needs Docker (or Podman via DOCKER_HOST)

# frontend/
npm run format:check               # npm run format to fix
npm run lint
npm run build
npm run csp:check
npm run test
```

If you changed an endpoint, request or response, also run `scripts/export-openapi.sh` and `npm run api:generate`, and commit `backend/openapi/v1.json` and `frontend/src/app/core/api/api-types.ts`. CI fails when either is stale.

Then:
- Read the diff for unrelated changes, secrets and generated artifacts.
- Update the docs: CLAUDE.md, this file, `docs/content/`, and an ADR for any significant decision.
- Report what changed, the trade-offs, and the commands you ran with their results.
