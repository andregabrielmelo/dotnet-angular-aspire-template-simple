# Coding agent instructions

The canonical rules for any coding agent (Claude Code, GitHub Copilot, Cursor, Codex and so on) working in this repository. Agent-specific files such as `CLAUDE.md` add tool-specific detail and point here. They must not contradict this file.

Commands, project layout and the full architecture overview are in [`CLAUDE.md`](CLAUDE.md). The reasons behind the design are in [`docs/content/design-decisions.md`](docs/content/design-decisions.md) and the [ADRs](docs/content/architecture-decisions/). Path-specific rules are in [`.github/instructions/`](.github/instructions/).

## 1. Before changing anything

- Read the ADR that covers the area you are touching before introducing a library, a pattern or a new abstraction. If your change contradicts one, stop and say so. Don't silently invent a competing pattern.
- When the ADRs don't settle a structural question, check the sources in [Further reading](docs/content/further-reading.md) (the two templates this project builds on and the blogs it leans on) before inventing an answer, and say which one you followed.
- For *how* to build things, follow the guides in [`docs/content/guides/`](docs/content/guides/): the [project map](docs/content/guides/project-map.md) (layout, a request's path, vocabulary), [adding a feature](docs/content/guides/adding-a-feature.md) (a complete worked example through every layer) and [common tasks](docs/content/guides/cookbook.md). This file holds the rules they follow.
- Search before assuming something is missing. The template already has authentication, permissions, email and Postgres-backed tests.
- This is the simple variant of [dotnet-angular-aspire-template](https://github.com/andregabrielmelo/dotnet-angular-aspire-template). When a task needs background jobs, a transactional outbox, auditing, output caching, file storage, ETags or API versioning, copy the shape from there (code, tests and ADR) instead of designing it from scratch.
- Copy the shape of the `User` feature: `Core/Aggregates/UserAggregate`, then `UseCases/Users/<UseCase>/`, then `Web/Endpoints/Users/`. It is the executable reference for every layer.
- Look at the related tests, DI registrations (`Web/Configurations/`, `Infrastructure/InfrastructureServiceExtensions.cs`) and configuration before creating something new.
- Keep changes small and focused. Don't refactor unrelated code while implementing a feature.

## 2. Architecture boundaries ([ADR 001](docs/content/architecture-decisions/adr-001-clean-architecture-layering.md))

Dependencies point inward:

| Project | Contains | May depend on |
|---|---|---|
| `AppTemplate.SharedKernel` | Stable base types: `EntityBase`, domain event interfaces and dispatch, `IRepository<T>` | nothing in the solution |
| `AppTemplate.Core` | Aggregates, Vogen value objects, domain events, specifications; domain event handlers that need only Core, next to their aggregate (`Core/Aggregates/<Aggregate>/Handlers/`); ports for external resources in `Core/Interfaces` (`IEmailSender`) | SharedKernel |
| `AppTemplate.UseCases` | Mediator commands, queries and their handlers; domain event handlers that need a UseCases port (`UseCases/<Feature>/Handlers/`); list query-service interfaces (`IListUsersQueryService`), the accounts port and the request context (`UseCases/Interfaces`: `IIdentityService`, `ICurrentUser`), the `Role`/`Permission` smart enums, and the caching contract (`ICacheable`, `CacheTags`, `CacheProfile`) | Core, SharedKernel |
| `AppTemplate.Infrastructure` | EF Core and Npgsql, repository and query-service implementations, MailKit, ASP.NET Core Identity (`ApplicationUser`, `IdentityService`) | Core, UseCases, SharedKernel |
| `AppTemplate.Web` | FastEndpoints endpoints, request validation, HTTP mapping, middleware, Mediator pipeline behaviors (`Web/Behaviors`: logging, and caching `ICacheable` queries), the antiforgery check, composition root | everything above |

- Core never references UseCases, Infrastructure, Web, EF Core or ASP.NET Core.
- Only Infrastructure uses ASP.NET Core Identity (`UserManager`, `SignInManager`, `ApplicationUser`); everything else goes through `IIdentityService` ([ADR 013](docs/content/architecture-decisions/adr-013-user-management-aspnetcore-identity.md)).
- UseCases never references Infrastructure, Web or EF Core. Data access goes through `IRepository<T>` or a query-service interface defined in UseCases.
- A dependency on an external resource (SMTP, an HTTP API, or our own Postgres beyond `IRepository<T>`) is an Infrastructure class behind an interface in `Core/Interfaces`; the doc comment names the implementation and its consumers. List query services, which return use-case DTOs, and `IIdentityService`, which returns them too, stay in UseCases; `ICurrentUser` (Web implements it from the request) is in Core.
- Don't move a class to another layer to avoid writing the right abstraction.
- Don't add projects, base classes, generic frameworks or DDD patterns without a concrete use and a clear benefit.
- Before adding an interface, specification, domain event or pipeline behavior, check [When To Abstract](docs/content/design-decisions.md#when-to-abstract). Optional capabilities stay opt-in ([Supported vs Required](docs/content/design-decisions.md#supported-vs-required)).

## 3. API endpoints ([ADR 003](docs/content/architecture-decisions/adr-003-fastendpoints-mediator.md))

- FastEndpoints with the REPR pattern only. No MVC controllers and no Minimal API handlers for feature endpoints.
- One endpoint class per HTTP operation, under `Web/Endpoints/<Feature>/`, in the namespace `AppTemplate.Web.Endpoints.<Feature>`. The request DTO, the FluentValidation `Validator<T>` and the `Mapper` live in the same file as the endpoint (see `GetByIdEndpoint.cs`).
- Endpoints are thin: translate the request into a command or query, `await mediator.Send(..., cancellationToken)`, and map the `Result` with `Web/Extensions/ResultExtensions.cs` (`ToOkResult`, `ToCreatedResult`, `ToNoContentResult`, `ToAcceptedResult`). The return type is `Results<Ok<T>, ProblemHttpResult>` (or `Created`, `NoContent`, `Accepted`). No business logic, EF Core or external SDKs in endpoints.
- Use case failures become RFC 9457 problem details through `ResultExtensions`; request validation failures are FastEndpoints' `ErrorResponse`, with the same `errors` field map ([ADR 009](docs/content/architecture-decisions/adr-009-problem-details-error-contract.md)). Never build an error response by hand or `switch` on `ResultStatus` in an endpoint.
- Request and response types are dedicated DTOs. Never expose an entity, aggregate or value object.
- Every FastEndpoints endpoint is authenticated by default; there is no fallback policy, so never map a route outside FastEndpoints. Declare either `AllowAnonymous()` (deliberately) or `Permissions(Permission.X)` explicitly ([ADR 011](docs/content/architecture-decisions/adr-011-role-based-permissions.md)). A new anonymous route must be added to `EndpointAuthorization` (functional tests).
- Never put Data Annotations attributes (`[Required]` and so on) on request DTOs. FastEndpoints ignores them; use the validator.
- Declare `Summary`, `Tags` and `Description(... .Produces...)` so the OpenAPI document stays accurate.
- Sessions are ASP.NET Core Identity's cookie ([ADR 015](docs/content/architecture-decisions/adr-015-cookie-sessions-aspnetcore-identity.md)): sign in and out only through `IIdentityService` (`SignInAsync`, `RefreshSignInAsync`, `SignOutAsync`), and add claims only in `ApplicationClaimsPrincipalFactory`, named in `AppClaimTypes`. Never issue tokens of your own or put a credential in a response body. A signed-in session's unsafe requests need the antiforgery token (`UseSessionAntiforgery`); never exempt an endpoint from it, and never add CORS.
- Accounts are ASP.NET Core Identity's ([ADR 013](docs/content/architecture-decisions/adr-013-user-management-aspnetcore-identity.md)). Use Identity for anything about credentials (passwords, codes, lockout, roles, the security stamp) through `IIdentityService`; never hash, compare or store passwords or codes yourself. Endpoints that send account emails or take their codes are anonymous, answer the same whether or not the email has an account, and never log the links or codes. Roles and permissions are the `Role` and `Permission` smart enums ([ADR 011](docs/content/architecture-decisions/adr-011-role-based-permissions.md)); never write a role name or permission claim as a string literal.
- Rate limiting is ASP.NET Core's limiter ([ADR 014](docs/content/architecture-decisions/adr-014-rate-limiting-and-security-headers.md)). Give every anonymous, email-sending or expensive endpoint the `authentication` policy (`Options(x => x.RequireRateLimiting(RateLimitingConfigurations.AuthenticationPolicy))`) or a named policy of its own. Never use FastEndpoints' `Throttle(...)`: it keys on the raw `X-Forwarded-For` header, which any client can set. Never partition by a header the client chooses.

## 4. Use cases

- Use [Mediator](https://github.com/martinothamar/Mediator) (source-generated). Never MediatR, and never a second dispatch mechanism.
- One folder per use case: commands in `UseCases/<Feature>/Commands/<UseCase>/`, queries in `UseCases/<Feature>/Queries/<UseCase>/`; domain event handlers go in `UseCases/<Feature>/Handlers/` when they need a UseCases port (`SendConfirmationOnUserRegistered` needs `IIdentityService`), or next to their aggregate in `Core/Aggregates/<Aggregate>/Handlers/` when Core is all they need (`UserNameUpdatedEventHandler`).
- Guard method arguments and invariants with `Ardalis.GuardClauses` (`Guard.Against.Null`, `NullOrWhiteSpace`, `InvalidInput`, `NotFound`) in SharedKernel, Core and Infrastructure. Value objects keep Vogen's `Validate`, and expected failures stay `Result`s. The command or query record has its own file (`UpdateUserCommand.cs`) next to its handler (`UpdateUserHandler.cs`).
- Handlers return `Ardalis.Result`. Use `Result.NotFound()`, `Result.Forbidden()`, `Result.Conflict()` or `Result.Invalid(...)` for expected outcomes; their messages reach the caller. `Result.Error()` and `Result.Unavailable()` become a generic 500 or 503, so log the details in the handler. Throw only for unexpected failures.
- Resource-based authorization ("may update their own profile, or anyone's with `users:write`") lives in the handler, through `ICurrentUser`, and returns `Result.Forbidden()`.
- **Repository or query service?**
  - Use `IRepository<T>` with an `Ardalis.Specification` for aggregate-oriented writes: load, change through domain methods, save.
  - Use a query-service interface defined in UseCases and implemented in Infrastructure for read models, projections, joins, pagination and hand-written SQL (`IListUsersQueryService`).
  - List query services read with `FromSqlRaw` (see `ListUsersQueryService`): the SQL selects every mapped column by its snake_case name and filters with `WHERE`, input goes only through `{n}` placeholders, and ordering, paging, the DTO projection and the count are composed in LINQ on top.
  - Lookups that are not followed by a mutation call `.AsNoTracking()` in their specification.
  - Don't add a repository method for every query, and don't load whole aggregates for read-only endpoints.
  - The full decision table is in [When To Abstract](docs/content/design-decisions.md#when-to-abstract).

## 5. Domain model ([ADR 005](docs/content/architecture-decisions/adr-005-vogen-strongly-typed-ids.md))

- Domain logic is independent of HTTP, EF Core, Angular and providers.
- Aggregates protect their own invariants. No public setters that bypass them.
- IDs and primitives with a real invariant are Vogen value objects with a `Validate` method. Register EF conversions centrally in `Infrastructure/Data/Configurations/VogenEfCoreConverters.cs`. Entity IDs are database identities: `ValueGeneratedOnAdd().HasVogenConversion()` in their `IEntityTypeConfiguration`, with `deserializationStrictness: DeserializationStrictness.AllowAnything` on the ID, since EF's temporary keys are negative. The domain `User` is the profile; credentials belong to its Identity account, never on the aggregate.
- Plain DTOs and projections (`UserDto`, `UserRecord`) stay plain. Don't wrap everything.
- Domain events are for meaningful domain occurrences (`UserRegisteredDomainEvent`). They are dispatched in-process after `SaveChanges` succeeds (`EventDispatchInterceptor`), or by the use case right after saving when the event needs a database-assigned id (`RegisterUserHandler`, through `IMediator.Publish`). After `SaveChanges`, a failing handler is logged and never fails the request; one the use case publishes itself fails it. They are not a reliable delivery mechanism: work that must happen needs a transactional outbox (see [Transactions and side effects](docs/content/design-decisions.md#transactions-and-side-effects)).

## 6. Persistence ([ADR 004](docs/content/architecture-decisions/adr-004-postgresql.md))

- EF Core, Npgsql, SQL and provider configuration stay in Infrastructure.
- Postgres uses snake_case names (`EFCore.NamingConventions`). Raw SQL must use snake_case columns, and must be parameterized. Never interpolate input into SQL.
- Async I/O everywhere, passing the request's `CancellationToken`. Avoid N+1 queries, unbounded result sets and unnecessary materialization. List endpoints page with a stable order.
- Schema changes need a migration (`dotnet ef migrations add ... -o Data/Migrations`, see CLAUDE.md). Review the generated code, then run `dotnet csharpier format .`. A model change without a migration fails CI (`MigrationDrift`, integration tests), and so does a migration whose schema doesn't match the model. Adding a non-null column to an existing table makes EF add a `defaultValue` to fill the existing rows: drop that default in the same migration unless the model declares it.

## 7. Configuration and dependency injection

- Register options before using them: `AddOptions<T>().Bind(...)` with explicit validation rules and `ValidateOnStart()`. `ValidateOnStart` alone makes nothing required; the rules do.
- Never invent configuration at the call site: no `GetSection(...).Get<T>() ?? new T()`, no `configuration["Key"] ?? "literal"`. Read options through `IOptions<T>` when the service or framework options are built (`AddOptions<TFramework>().Configure<IOptions<T>>(...)`, DI factories), not at registration. A required value is validated with `ValidateOnStart`, so a missing value fails startup, not the first request.
- Three kinds of settings: **required** (validated as present), **optional feature** (empty means off; when on, every setting is validated so a partial configuration fails), and **safe defaults** (defaults live in the options class, never in `appsettings*.json`, which keeps only values we set).
- A new options type joins the explicit list in its composition root's tests (`OptionsRegistration` in the functional tests for Web). Why: `docs/content/best-practices.md`, Options.
- Never hardcode secrets, credentials or environment-specific URLs. Local services are wired by Aspire (`WithReference`/`WaitFor` in `AppHost.cs`); secrets are AppHost parameters or user secrets.
- The AppHost passes an app only what depends on the composition, each value explicitly: `WithReference` for resources, `WithEnvironment` for addresses it knows (another resource's endpoint), `AddParameter` for deployment values and secrets. Never forward a whole configuration section. An app's own defaults, development ones included, live in its own `appsettings.json`/`appsettings.Development.json`.
- Never inject a scoped service into a singleton.
- Document any new configuration key, environment variable or AppHost parameter.
- A configuration file for a third-party service we run lists every option of the pinned version: options in effect are set explicitly, even to their default, and unused ones stay commented out with their default and when to use them. Re-check the file whenever the version is bumped. Why: `docs/content/best-practices.md`, Third-party configuration files.

## 8. Health checks

- Postgres gates readiness (`/health`, tag `ready`). A new required dependency joins it; an optional one must not gate readiness, and the code that uses it fails open. One deliberate exception: with Redis down, cached reads fall back, but a write that must invalidate cache entries saves and then answers 500, because `RemoveByTagAsync` needs Redis (ADR 017). Never tag a dependency `live`. Policy: `docs/content/best-practices.md`, Health checks.

## 9. Testing ([ADR 006](docs/content/architecture-decisions/adr-006-testing-strategy.md))

- Follow ardalis/CleanArchitecture's test conventions: xUnit v3, Shouldly, NSubstitute, one class per member or behavior named `<Type><Member>` (`UserCreate`, `LoginHandlerHandle`, `UserGetById`), methods named for the behavior, and `TestContext.Current.CancellationToken`.
- **Unit tests** (`AppTemplate.UnitTests`) cover domain rules and handler behavior with NSubstitute and no I/O.
- **Integration tests** (`AppTemplate.IntegrationTests`) cover Infrastructure against a real Postgres (Testcontainers): repositories, constraints, query services and migrations, on `BaseEfRepoTestFixture`.
- **Functional tests** (`AppTemplate.FunctionalTests`) cover HTTP contracts, validation, authorization, status codes and serialization through `CustomWebApplicationFactory<Program>`, against a real Postgres, one class per route and verb in `ApiEndpoints/`:
  - Mark every class that needs Postgres with `[Trait(TestCategories.Name, TestCategories.RequiresDocker)]`.
  - Sign in for real: `factory.CreateSignedInSessionAsync(role)` (a `Role`, `Member` by default) returns the user and a `TestSession` whose `Client` is a cookie jar holding the session, and sends the antiforgery token as the SPA does; `TestSession` also drives login, register, change password and logout. `factory.CreateUserAsync(role)` creates an account. There is no test authentication handler.
  - Assert status codes with `Ardalis.HttpClientTestExtensions` (`GetAndEnsureNotFoundAsync`, ...). Emails go to `factory.EmailSender`, a fake that records them.
- **Architecture tests** (`AppTemplate.ArchitectureTests`) enforce section 2 and the placement rules in sections 3 and 4. If one fails, fix the code, not the rule. Only change a rule together with the ADR that justifies it.
- **The Aspire smoke test** (`AppTemplate.AspireTests`) starts the real AppHost and waits for the API to be healthy. Keep it to that one startup path; it needs Docker and a trusted development certificate. When you add a resource with a persistent lifetime or a data volume, keep the test's throwaway override working (`UseThrowawayContainers`).
- Tests follow Arrange-Act-Assert, one behavior per test. Change tests whenever observable behavior changes.

## 10. Frontend (`frontend/`)

See [`.github/instructions/frontend.instructions.md`](.github/instructions/frontend.instructions.md). In short:
- Standalone components in `core/`, `shared/` and `features/<feature>/`.
- HTTP calls live in feature services, not components.
- Guards are UX, not authorization.
- The SPA must run under a strict CSP, sent by the production host: no inline scripts, inline event handlers or `eval`. New external origins go into the CSP directive, never `'unsafe-inline'` scripts (see `docs/content/best-practices.md`, Production deployment).
- The session is an HttpOnly cookie the app never reads ([ADR 015](docs/content/architecture-decisions/adr-015-cookie-sessions-aspnetcore-identity.md)); `AuthService` only knows whether the API returns a profile. Never store a credential in `localStorage`, call `POST /auth/token` or add an `Authorization` header (bearer tokens are for non-browser clients, [ADR 018](docs/content/architecture-decisions/adr-018-bearer-tokens-for-non-browser-clients.md)), or turn off HttpClient's XSRF support: it sends the antiforgery token. After anything that changes who is signed in, fetch a new token (`fetchAntiforgeryToken()`).

## 11. Code style

- C#: `backend/.editorconfig`, CSharpier, central package versions in `backend/Directory.Packages.props`, and LF line endings.
- **Code analysis fails the build.** `backend/Directory.Build.props` turns on the recommended .NET analyzers (`AnalysisLevel` `latest-recommended`) and makes their warnings errors. `.editorconfig` also makes `throw ex;` (CA2200) and unused usings (IDE0005) errors. Fix the code; don't silence the rule. Suppress only where the rule is wrong for the case: in `.editorconfig` with a comment saying why, or with `[SuppressMessage(..., Justification = "...")]`. Never use a bare `#pragma warning disable`.
- Log through `ILoggerAdapter<T>` (`Core/Interfaces`), in every layer, with PascalCase placeholders. Only Infrastructure's `LoggerAdapter<T>` and Web's logging setup use `Microsoft.Extensions.Logging` (`LayerDependencyTests`); SharedKernel's `MediatorDomainEventDispatcher` keeps `ILogger`, since it can't see Core. CA1848 and CA1873 are suggestions for that reason. Compare strings with an explicit `StringComparison` (CA1310); pass `CultureInfo.InvariantCulture` for anything parsed back or stored (CA1305 is a suggestion).
- After changing a package version, run `dotnet restore` and commit the regenerated `packages.lock.json` files.
- Inject `ILoggerAdapter<T>` and use message templates (`"{UserId} created"`), never string interpolation. Log ids and names, never tokens, passwords, personal data, request bodies, commands or DTOs (`{@Command}` or a record's `ToString()` prints every property): nothing redacts logs. See `docs/content/best-practices.md`, Logging.
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
npm run test
```

If you changed an endpoint, request or response, also run `scripts/export-openapi.sh` and `npm run api:generate`, and commit `backend/openapi/v1.json` and `frontend/src/app/core/api/api-types.ts`. CI fails when either is stale.

Then:
- Read the diff for unrelated changes, secrets and generated artifacts.
- Update the docs: CLAUDE.md, this file, `docs/content/`, and an ADR for any significant decision.
- Report what changed, the trade-offs, and the commands you ran with their results.
