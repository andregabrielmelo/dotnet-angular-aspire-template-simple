---
title: "How the project fits together"
weight: 10
---

# How the project fits together

A map of the repository for your first hour in it: where things live, what happens to a request, what the names mean, and where to look when you need to change something. Paths are relative to the repository root.

## The repository

| Path | What's in it |
|---|---|
| `backend/` | The .NET solution (`AppTemplate.slnx`): the API, the Aspire host and the tests |
| `frontend/` | The Angular app |
| `docs/` | This documentation site (Hugo) |
| `scripts/` | `rename-template.sh` (run once, when starting a project) and `export-openapi.sh` (after changing the API) |
| `.github/` | CI workflows, Dependabot, and path-specific instructions for agents |
| `AGENTS.md`, `CLAUDE.md` | Rules and commands for coding agents. Worth reading for people too |

### Backend projects (`backend/src/`)

Dependencies point inward: each project may use the ones listed above it, never the ones below. `AGENTS.md` §2 has the exact rules, and `AppTemplate.ArchitectureTests` enforces them.

| Project | Holds | Example |
|---|---|---|
| `AppTemplate.SharedKernel` | Base types every layer uses: `EntityBase`, `IRepository<T>`, domain event plumbing, the persistence exceptions use cases catch | `IRepository.cs` |
| `AppTemplate.Core` | The domain: aggregates, value objects, specifications, domain events, and interfaces ("ports") for things outside the process | `Aggregates/UserAggregate/User.cs`, `Interfaces/IEmailSender.cs` |
| `AppTemplate.UseCases` | What the application does: one folder per command or query, with its handler. Also list query-service interfaces, `ICurrentUser` and `Permission` | `Users/Update/UpdateUserHandler.cs` |
| `AppTemplate.Infrastructure` | How it's done: EF Core and Postgres, repository and query-service implementations, email over SMTP | `Data/Queries/ListUsersQueryService.cs` |
| `AppTemplate.Web` | The HTTP API: endpoints, validation, authentication, error responses, and the composition root (`Program.cs`, `Configurations/`) | `Features/UserFeatures/UpdateEndpoint.cs` |
| `AppTemplate.ServiceDefaults` | Shared by every host: OpenTelemetry (logs, traces, metrics), health checks, service discovery | `Extensions.cs` |
| `AppTemplate.AppHost` | Local development only: starts Postgres, Mailpit (`Development` only), the API and Angular together | `AppHost.cs` |

### Tests (`backend/tests/`)

| Project | Tests | Needs |
|---|---|---|
| `AppTemplate.UnitTests` | Domain rules and handlers, with fakes (NSubstitute) | nothing |
| `AppTemplate.FunctionalTests` | Real HTTP calls through the whole API, against a real Postgres, including sign-in, refresh-token rotation and the CSRF checks | Docker or Podman |
| `AppTemplate.ArchitectureTests` | The layering and naming rules | nothing |

### Frontend (`frontend/src/app/`)

| Folder | Holds |
|---|---|
| `core/` | App-wide plumbing: `api/` (generated API types, base paths), `auth/` (session, current user, guards, the CSRF interceptor) |
| `features/<feature>/` | Everything one feature owns: pages, its `<feature>.service.ts` (HTTP calls), its `<feature>.model.ts` (API types) |
| `shared/` | Not there yet. Create it when two features need the same component or pipe |

## What happens to a request

Saving your name on the profile page sends `PUT /api/users/7` with `{ "name": "Ada" }`. Here's its path, with the file that handles each step:

1. **Angular.** `ProfilePage` calls `UsersService.update` (`features/users/users.service.ts`). `auth-interceptor.ts` adds the in-memory access token as `Authorization: Bearer`; if the API answers 401 because it expired, the interceptor refreshes it once (`AuthService.refresh`, using the refresh cookie) and retries.
2. **Dev server proxy.** `frontend/proxy.conf.mjs` strips `/api` and forwards `PUT /users/7` to the API, adding `X-Forwarded-For`.
3. **API middleware** (`Web/Configurations/MiddlewareConfigurations.cs`, in this order): forwarded headers, error handling, JWT authentication, the rate limiter, authorization (permission policies).
4. **Endpoint.** FastEndpoints routes to `UpdateEndpoint` (`Web/Features/UserFeatures/UpdateEndpoint.cs`), binds `UpdateUserRequest`, and runs `UpdateUserValidator`. Invalid input stops here with a 400. Otherwise the endpoint turns the request into an `UpdateUserCommand` and sends it.
5. **Mediator.** `LoggingBehavior` (`UseCases/Behaviors/`) logs the command's name (never its contents) and times it, then calls the handler.
6. **Handler.** `UpdateUserHandler` loads the user through `IRepository<User>` and checks, with `ICurrentUser`, that you may change it. It changes the user through a domain method (`User.UpdateName`), saves, and returns `Result<UserDto>`.
7. **Database.** `EntityFrameworkRepository` (Infrastructure) saves with EF Core to Postgres. After a successful save, `EventDispatchInterceptor` publishes any domain events the entity raised.
8. **Back out.** The endpoint maps the `Result` with `ResultExtensions.ToOkResult`. Success becomes `200` with an `UpdateUserResponse`; `NotFound`, `Forbidden` or `Invalid` becomes a problem details response with a `traceId` ([ADR 009]({{< relref "architecture-decisions/adr-009-problem-details-error-contract" >}})).

To see this for yourself, run the app, open the Aspire dashboard's **Traces** tab, and open the trace for one request.

## Vocabulary

| Term | Here it means | Example |
|---|---|---|
| **Endpoint** | One class per HTTP operation (FastEndpoints, REPR: Request, Endpoint, Response). This replaces MVC controllers; there are none | `GetByIdEndpoint` |
| **Request / response** | Plain classes for what goes over the wire. Never an entity | `UpdateUserRequest`, `UserRecord` |
| **Validator** | Checks a request's shape (required, length, format), in the endpoint's file | `UpdateUserValidator` |
| **Mapper** | Turns a use case's DTO into the response | `GetUserByIdMapper` |
| **Command** | A request to change something, as a record. Returns `Result` or `Result<T>` | `UpdateUserCommand` |
| **Query** | A request to read something, as a record. Changes nothing | `GetUserQuery`, `ListUsersQuery` |
| **Handler** | The code that carries out one command or query. Endpoints send through Mediator; they never call a handler directly | `UpdateUserHandler` |
| **`Result`** | What a handler returns instead of throwing: `Success`, `NotFound`, `Forbidden`, `Invalid`, `Conflict`... `ResultExtensions` maps each one to an HTTP status | `Result<UserDto>.NotFound()` |
| **DTO** | What a handler returns: plain data, never the entity | `UserDto` |
| **Aggregate** | An entity that guards its own rules. Changed only through its methods, saved and loaded as a unit | `User` |
| **Value object** | A small type with a rule built in, so an invalid value can't exist (Vogen) | `UserName`, `EmailAddress` |
| **Repository** | `IRepository<T>`: load, add, update and delete aggregates. Use it when you're going to change something | `IRepository<User>` |
| **Specification** | A named, reusable query for the repository | `UserByIdSpecification` |
| **Query service** | An interface in UseCases, implemented in Infrastructure, that reads straight into DTOs. Use it for lists, projections and joins | `IListUsersQueryService` |
| **`PagedResult<T>`** | A page of items plus the page number, page size, total count and total pages | `ListUsersHandler` |
| **Domain event** | Something that happened in the domain, published in-process after the save | `UserRegisteredDomainEvent` |
| **Port** | An interface in `Core/Interfaces` for something outside the process. Infrastructure implements it | `IEmailSender` |
| **Permission / policy** | Something a role grants, such as `users:read`, carried in the access token and also an ASP.NET Core policy of the same name | `Policies(Permission.UsersRead)` |
| **`ICurrentUser`** | The caller: their `Id` (the token's `sub`) and permissions. Use it for "only the owner may..." rules | `UpdateUserHandler` |
| **Feature service** | The Angular class that makes a feature's HTTP calls. Components never use `HttpClient` directly | `UsersService` |

## To change X, look in Y

| To change... | Look in |
|---|---|
| The sign-in and registration pages | `frontend/src/app/features/auth/` |
| Sign-in, registration, refresh and sign-out | `Web/Features/AuthFeatures/`, `UseCases/Authentication/`, `UseCases/Users/Register/` |
| Tokens: how they're issued and validated, their lifetimes | `Web/Authentication/JwtTokenIssuer.cs`, `Web/Configurations/AuthenticationConfigurations.cs`, the `Jwt` settings |
| The refresh cookie, Origin and antiforgery checks | `Web/Authentication/AuthCookies.cs`, the `Authentication` settings |
| What's proxied to the API | `frontend/proxy.conf.mjs` |
| Permissions, and which role has them | `UseCases/Authorization/Permission.cs`, `RolePermissions.cs`, `frontend/src/app/core/auth/permissions.ts` |
| Error responses | `Web/Extensions/ResultExtensions.cs`, `Web/Configurations/MiddlewareConfigurations.cs` |
| Logging | `Web/Configurations/LoggerConfigs.cs`, the `Serilog` and `Logging` settings |
| Rate limits | `Web/Configurations/RateLimitingConfigurations.cs` |
| Settings and their validation | `Web/Configurations/OptionConfigurations.cs`, and the options class next to what uses it |
| The database schema | `Infrastructure/Data/Configurations/`, then a migration in `Infrastructure/Data/Migrations/` |
| Seed data (Development only) | `Infrastructure/Data/SeedData.cs` |
| Email | `Infrastructure/Email/`, the `Mailserver` settings |
| Health checks | `ServiceDefaults/Extensions.cs`, `Infrastructure/InfrastructureServiceExtensions.cs` |
| Which services run locally | `AppHost/AppHost.cs` |
| The API contract the frontend uses | `backend/openapi/v1.json` and `frontend/src/app/core/api/api-types.ts`, both generated |
| Frontend routes | `frontend/src/app/app.routes.ts` |
| CI | `.github/workflows/` (see its `README.md`) |

## Naming

| Thing | Name and place |
|---|---|
| Aggregate | `Core/Aggregates/<Name>Aggregate/<Name>.cs`, with `<Name>Id.cs` and its value objects next to it |
| Specification | `Core/Aggregates/<Name>Aggregate/Specifications/<Name>By<What>Specification.cs` |
| Command or query | `UseCases/<Plural>/<UseCase>/<UseCase>Command.cs` or `...Query.cs`, plus `<UseCase>Handler.cs` |
| DTO | `UseCases/<Plural>/<Name>Dto.cs` |
| List query service | `UseCases/<Plural>/List/IList<Plural>QueryService.cs`, implemented in `Infrastructure/Data/Queries/` |
| Endpoint | `Web/Features/<Name>Features/<Verb>Endpoint.cs`, with its request, validator and mapper in the same file |
| EF configuration | `Infrastructure/Data/Configurations/<Name>Configuration.cs` |
| Tests | `UnitTests/Core/<Name>Aggregate/`, `UnitTests/UseCases/<Plural>/<UseCase>HandlerTests.cs`, `FunctionalTests/<Name>Features/<Name>EndpointsTests.cs` |
| Frontend | `features/<plural>/<plural>.service.ts`, `<plural>.model.ts`, `<plural>-page.ts` |

The architecture tests check some of this: endpoints live under `Web.Features`, handlers only in UseCases, and command and query names end in `Command` or `Query`.
